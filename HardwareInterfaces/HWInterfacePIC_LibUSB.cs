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
using Common;
//#endif

namespace ECore.HardwareInterfaces
{
    public delegate void OnDeviceConnect(EDeviceHWInterface hardwareInterface, bool connected);

    //class that provides raw HW access to the device
    public static class HWInterfacePIC_LibUSB
    {   
        //needed for plug-unplug events
        public static OnDeviceConnect onConnect;
        public static IDeviceNotifier UsbDeviceNotifier;
        private static bool initialized = false;
        private static int VID = 0x04D8;
        private static int[] PIDs = new int[] {0x0052, 0xF4B5};
        private static Dictionary<string, ScopeUsbInterface> interfaces = new Dictionary<string,ScopeUsbInterface>();

        public static void Initialize()
        {
            if (initialized) return;
            initialized = true;

#if __IOS__ || ANDROID
#else
            UsbDeviceNotifier = DeviceNotifier.OpenDeviceNotifier();
            UsbDeviceNotifier.OnDeviceNotify += OnDeviceNotifyEvent;            
#endif
            UsbRegDeviceList usbDeviceList = UsbDevice.AllDevices;
            Logger.Debug("Total number of USB devices attached: " + usbDeviceList.Count.ToString());
            foreach (UsbRegistry device in usbDeviceList)
            {
                string sAdd = string.Format("Vid:0x{0:X4} Pid:0x{1:X4} (rev:{2}) - {3}",
                                            device.Vid,
                                            device.Pid,
                                            (ushort)device.Rev,
                                            device[SPDRP.DeviceDesc]);

                Logger.Debug(sAdd);
            }
            foreach (int PID in PIDs)
            {
                UsbDeviceFinder scopeUsbFinder = new UsbDeviceFinder(VID, PID);
                UsbDevice scopeUsbDevice = UsbDevice.OpenUsbDevice(scopeUsbFinder);
                if (scopeUsbDevice != null)
                {
                    DeviceFound(scopeUsbDevice);
                    break;
                }
            }
        }

        private static void DeviceFound(UsbDevice scopeUsbDevice)
        {
            ScopeUsbInterface f = new ScopeUsbInterface(scopeUsbDevice);
            //FIXME: should use ScopeUsbDevice.serial but not set with smartscope
            string serial = scopeUsbDevice.Info.SerialString;
            if(serial == "")
                throw new Exception("This device doesn't have a serial number, can't work with that");
            if(interfaces.ContainsKey(serial))
                throw new Exception("This device was already registered. This is a bug");
            interfaces.Add(serial, f);
            try
            {
                if (onConnect != null)
                    onConnect(f, true);
            }
            catch (Exception e)
            {
                Logger.Error("Error while calling OnConnect event handler: " + e.Message);
                interfaces.Remove(serial);
                f.Dispose();
            }

        }

        internal static void RemoveDevice(ScopeUsbInterface f)
        {
            if (onConnect != null)
                onConnect(f, false);
            interfaces.Remove(f.GetSerial());
            f.Dispose();
        }

        public static void AddConnectHandler(OnDeviceConnect c)
        {
            onConnect += c;
        }

        //called at init, and each time a system event occurs
        private static void OnDeviceNotifyEvent(object sender, DeviceNotifyEventArgs e)
        {
            switch (e.EventType)
            {
                case EventType.DeviceArrival:
                    Logger.Debug("LibUSB device arrival");
                    if (e.Device == null || e.Device.IdVendor != VID || !PIDs.Contains(e.Device.IdProduct))
                    {
                        Logger.Info("Not taking this device, PID/VID not a smartscope");
                        return;
                    }

                    UsbDeviceFinder usbFinder = new UsbDeviceFinder(e.Device.IdVendor, e.Device.IdProduct);
                    UsbDevice usbDevice = UsbDevice.OpenUsbDevice(usbFinder);
                    if(usbDevice != null)
                        DeviceFound(usbDevice);
                    break;
                case EventType.DeviceRemoveComplete:
                    if (!interfaces.ContainsKey(e.Device.SerialNumber))
                        return;

                    Logger.Debug(String.Format("LibUSB device removal [VID:{0},PID:{1}]", e.Device.IdVendor, e.Device.IdProduct)); 
                    if (onConnect != null)
                        onConnect(interfaces[e.Device.SerialNumber], false);
                    
                    interfaces[e.Device.SerialNumber].Dispose();
                    interfaces.Remove(e.Device.SerialNumber);

                    break;
                default:
                    Logger.Debug("LibUSB unhandled device event [" + Enum.GetName(typeof(EventType), e.EventType) + "]"); 
                    break;
            }
        }
    }
}
