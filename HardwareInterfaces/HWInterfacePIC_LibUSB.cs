using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
//#if IPHONE
//#else
using LibUsbDotNet;
using LibUsbDotNet.Main;
using LibUsbDotNet.DeviceNotify;
using C=Common;
//#endif

namespace ECore.HardwareInterfaces
{
    delegate void OnDeviceConnect(ScopeUsbInterface hardwareInterface, bool connected);

    //class that provides raw HW access to the device
    internal static class HWInterfacePIC_LibUSB
    {   
        //needed for plug-unplug events
        static OnDeviceConnect onConnect;
        static IDeviceNotifier UsbDeviceNotifier;
        static bool initialized = false;
        static int VID = 0x04D8;
        static int[] PIDs = new int[] {0x0052, 0xF4B5};
        static Dictionary<string, ScopeUsbInterface> interfaces = new Dictionary<string,ScopeUsbInterface>();

        internal static void Initialize()
        {
            if (initialized) return;
            initialized = true;

#if __IOS__ || ANDROID
#else
            UsbDeviceNotifier = DeviceNotifier.OpenDeviceNotifier();
            UsbDeviceNotifier.OnDeviceNotify += OnDeviceNotifyEvent;            
#endif
            UsbRegDeviceList usbDeviceList = UsbDevice.AllDevices;
			C.Logger.Debug("Total number of USB devices attached: " + usbDeviceList.Count.ToString());
            foreach (UsbRegistry device in usbDeviceList)
            {
                string sAdd = string.Format("Vid:0x{0:X4} Pid:0x{1:X4} (rev:{2}) - {3}",
                                            device.Vid,
                                            device.Pid,
                                            (ushort)device.Rev,
                                            device[SPDRP.DeviceDesc]);

				C.Logger.Debug(sAdd);
            }
        }

        internal static void PollDevice()
        {
            if (interfaces.Count > 0 && onConnect != null)
            {
                onConnect(interfaces.First().Value, true);
                return;
            }
            foreach (int PID in PIDs)
            {
                UsbDeviceFinder scopeUsbFinder = new UsbDeviceFinder(VID, PID);
                UsbDevice scopeUsbDevice = UsbDevice.OpenUsbDevice(scopeUsbFinder);
                if (scopeUsbDevice != null)
                {
                    try
                    {
                        DeviceFound(scopeUsbDevice);
                    }
                    catch (Exception e)
                    {
						C.Logger.Error("Device was found but failed to register: " + e.Message);
                    }
                    break;
                }
            }
        }

	internal static void DeviceFound(LibUsbDotNet.UsbDevice scopeUsbDevice)
        {
            string serial = null;
            try
            {
                ScopeUsbInterface f = new ScopeUsbInterface(scopeUsbDevice);
                //FIXME: should use ScopeUsbDevice.serial but not set with smartscope
                serial = scopeUsbDevice.Info.SerialString;
                if (serial == "" || serial == null)
                    throw new ScopeIOException("This device doesn't have a serial number, can't work with that");
                if (interfaces.ContainsKey(serial))
                    throw new ScopeIOException("This device was already registered. This is a bug");
                C.Logger.Debug("Device found with serial [" + serial + "]");
                interfaces.Add(serial, f);

                if (onConnect != null)
                    onConnect(f, true);
            }
            catch (ScopeIOException e)
            {
				C.Logger.Error("Error while trying to connect to device event handler: " + e.Message);
                if (serial != null)
                    interfaces.Remove(serial);
            }
        }

        internal static void AddConnectHandler(OnDeviceConnect c)
        {
            onConnect += c;
        }
        internal static void RemoveConnectHandler(OnDeviceConnect c)
        {
            onConnect -= c;
        }

        //called at init, and each time a system event occurs
        private static void OnDeviceNotifyEvent(object sender, DeviceNotifyEventArgs e)
        {
            switch (e.EventType)
            {
                case EventType.DeviceArrival:
				C.Logger.Debug("LibUSB device arrival");
                    if (e.Device == null || e.Device.IdVendor != VID || !PIDs.Contains(e.Device.IdProduct))
                    {
					C.Logger.Info("Not taking this device, PID/VID not a smartscope");
                        return;
                    }

                    UsbDeviceFinder usbFinder = new UsbDeviceFinder(e.Device.IdVendor, e.Device.IdProduct);
                    UsbDevice usbDevice = UsbDevice.OpenUsbDevice(usbFinder);
                    if(usbDevice != null)
                        DeviceFound(usbDevice);
                    break;
                case EventType.DeviceRemoveComplete:
                    C.Logger.Debug(String.Format("LibUSB device removal [VID:{0},PID:{1}]", e.Device.IdVendor, e.Device.IdProduct));
                    if (e.Device != null && e.Device.IdVendor == VID && PIDs.Contains(e.Device.IdProduct))
                    {
                        //Cos sometimes we fail to get the serial
                        if(e.Device.SerialNumber == "" || e.Device.SerialNumber == null)
                            RemoveDevice(interfaces.First().Key);
                        else
                            RemoveDevice(e.Device.SerialNumber);
                    }
                    break;
                default:
				C.Logger.Debug("LibUSB unhandled device event [" + Enum.GetName(typeof(EventType), e.EventType) + "]"); 
                    break;
            }
        }

        internal static void RemoveDevice(string serial)
        {
            C.Logger.Debug("Removing device with serial [" + serial + "]");
            if (!interfaces.ContainsKey(serial))
                return;

            if (onConnect != null)
                onConnect(interfaces[serial], false);

            interfaces[serial].Destroy();
            interfaces.Remove(serial);

        }
    }
}
