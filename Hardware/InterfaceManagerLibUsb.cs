using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using LibUsbDotNet;
using LibUsbDotNet.Main;
using LibUsbDotNet.DeviceNotify;
using C=LabNation.Common;

namespace LabNation.DeviceInterface.Hardware
{
    //class that provides raw HW access to the device
    internal class InterfaceManagerLibUsb: InterfaceManager<InterfaceManagerLibUsb, SmartScopeInterfaceLibUsb>
    {   
        #if IOS
        object pollLock = new object();
        bool pollThreadRunning;
        Thread pollThread;
        const int POLL_INTERVAL=1000;
        #else
        IDeviceNotifier UsbDeviceNotifier;
        #endif

        protected override void Initialize()
        {
            #if IOS
            startPollThread();
            #else
            UsbDeviceNotifier = DeviceNotifier.OpenDeviceNotifier();
            UsbDeviceNotifier.OnDeviceNotify += OnDeviceNotifyEvent;   
            #endif
        }

        #if IOS
        private void startPollThread()
        {
            pollThread = new Thread(new ThreadStart(pollThreadStart));
            pollThread.Name = "USB poll thread";
            pollThreadRunning = true;
            pollThread.Start();
        }

        private void pollThreadStart ()
		{
			while (pollThreadRunning) {
				Common.Logger.Warn ("Polling USB");

				UsbRegDeviceList usbDeviceList = UsbDevice.AllDevices;
				var r = usbDeviceList.Where (x => VID == x.Vid && PIDs.Contains (x.Pid));

				foreach (var inti in interfaces) {
					if (inti.Value.Destroyed) {
						Common.Logger.Debug("Found a interface (" + inti.Key + ") which was destroyed");
						RemoveDevice (inti.Key);
					}
				}

				C.Logger.Warn ("Filtered list conatins " + r.Count () + " devs - got " + interfaces.Count + " ifs so far");
				if (r.Count () > 0) 
				{
					C.Logger.Warn("Removing all devices");
					RemoveAllDevices();
					C.Logger.Warn("Calling poll");
					PollDevice ();
					C.Logger.Warn("Done");
				}
                Thread.Sleep(POLL_INTERVAL);
            }
        }

        private void RemoveAllDevices()
        {
            Common.Logger.Warn("Removing all devices");
            object[] ifKeys = interfaces.Keys.ToArray();

            for(int i = 0; i < ifKeys.Length; i++)
            {
                RemoveDevice(ifKeys[i]);
            }
        }

        #endif

        public override void PollDevice()
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

		public void Destroy()
		{
            #if IOS
            pollThreadRunning = false;
            pollThread.Join(1000);
            #else
			UsbDeviceNotifier.Enabled = false;
            #endif
			UsbDevice.Exit();
		}

	    private void DeviceFound(LibUsbDotNet.UsbDevice scopeUsbDevice)
        {
            string serial = null;
            try
            {
                SmartScopeInterfaceLibUsb f = new SmartScopeInterfaceLibUsb(scopeUsbDevice);
                //FIXME: should use ScopeUsbDevice.serial but not set with smartscope
                serial = scopeUsbDevice.Info.SerialString;
                if (serial == "" || serial == null)
                    throw new ScopeIOException("This device doesn't have a serial number, can't work with that");
                if (interfaces.ContainsKey(serial)) {
                    Common.Logger.Warn("Can't re-register device with this serial " + serial);
                    throw new ScopeIOException("This device was already registered. This is a bug");
                }
                C.Logger.Info("Device found with serial [" + serial + "]");
                interfaces.Add(serial, f);

                if (onConnect != null)
                    onConnect(f, true);
            }
            catch (ScopeIOException e)
            {
				C.Logger.Error("Error while opening device: " + e.Message);
                if (serial != null)
                    interfaces.Remove(serial);
            }
        }

        //called at init, and each time a system event occurs
        private void OnDeviceNotifyEvent(object sender, DeviceNotifyEventArgs e)
        {
            switch (e.EventType)
            {
                case EventType.DeviceArrival:
					C.Logger.Debug("LibUSB device arrival");
                    if (e.Device == null || e.Device.IdVendor != VID || !PIDs.Contains(e.Device.IdProduct))
                    {
						C.Logger.Debug("Not taking this device, PID/VID not a smartscope");
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

        private void RemoveDevice(object serial)
        {
            C.Logger.Info("Removing device with serial [" + serial + "]");
            if (!interfaces.ContainsKey(serial)) {
                C.Logger.Warn("OMG this device is not registered?!");
                return;
            }

            if (onConnect != null)
                onConnect(interfaces[serial], false);

            interfaces[serial].Destroy();
            interfaces.Remove(serial);

        }
    }
}
