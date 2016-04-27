using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using LibUsbDotNet;
using LibUsbDotNet.Main;
using LibUsbDotNet.DeviceNotify;
using LibUsbDotNet.LudnMonoLibUsb;
using C=LabNation.Common;

namespace LabNation.DeviceInterface.Hardware
{
    //class that provides raw HW access to the device
    internal class InterfaceManagerLibUsb: InterfaceManager<InterfaceManagerLibUsb>
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
				foreach(ISmartScopeUsbInterface iface in interfaces.Values)
					onConnect(iface, true);
                return;
            }
            foreach (int PID in PIDs)
            {
				UsbDevice scopeUsbDevice;
				List<UsbRegistry> devReg = UsbDevice.AllDevices.Where(x => x.Pid == PID && x.Vid == VID).ToList();
				foreach (UsbRegistry r in devReg) {
					C.Logger.Debug ("Dev found: " + r.DeviceInterfaceGuids);
					r.Open (out scopeUsbDevice);
					if (scopeUsbDevice != null) {
						try {
							DeviceFound ((MonoUsbDevice)scopeUsbDevice);
						} catch (Exception e) {
							C.Logger.Error ("Device was found but failed to register: " + e.Message);
						}
						break;
					}
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

	    private void DeviceFound(MonoUsbDevice scopeUsbDevice)
        {
            string serial = null;
			string location = null;
            try
            {
                SmartScopeUsbInterfaceLibUsb f = new SmartScopeUsbInterfaceLibUsb(scopeUsbDevice);
                //FIXME: should use ScopeUsbDevice.serial but not set with smartscope
                serial = scopeUsbDevice.Info.SerialString;
				location = string.Format("{0}.{1}", scopeUsbDevice.BusNumber, scopeUsbDevice.DeviceAddress);
                if (interfaces.ContainsKey(location)) {
                    Common.Logger.Warn("Can't re-register device with this location " + location);
                    throw new ScopeIOException("This device was already registered. This is a bug");
                }
				C.Logger.Warn("Device found with serial [" + serial + "] at location [" + location + "]");
                interfaces.Add(location, f);

                if (onConnect != null)
                    onConnect(f, true);
            }
            catch (ScopeIOException e)
            {
				C.Logger.Error("Error while opening device: " + e.Message);
                if (serial != null)
                    interfaces.Remove(location);
            }
        }

        //called at init, and each time a system event occurs
        private void OnDeviceNotifyEvent(object sender, DeviceNotifyEventArgs e)
        {
			LibUsbDotNet.DeviceNotify.Linux.LinuxUsbDeviceNotifyInfo notInfo = e.Device as LibUsbDotNet.DeviceNotify.Linux.LinuxUsbDeviceNotifyInfo;
			C.Logger.Debug ("Looking for dev at bus/addr {0}/{1}", notInfo.BusNumber, notInfo.DeviceAddress);

            switch (e.EventType)
            {
				case EventType.DeviceArrival:
					C.Logger.Debug ("LibUSB device arrival");
					if (e.Device == null || e.Device.IdVendor != VID || !PIDs.Contains (e.Device.IdProduct)) {
						C.Logger.Info ("Not taking this device, PID/VID not a smartscope");
						return;
					}
					UsbRegDeviceList devreg = UsbDevice.AllDevices;
					for(int i = 0; i < devreg.Count ; i ++)
					{
						if (devreg[i].Device is MonoUsbDevice) {
							MonoUsbDevice r = (MonoUsbDevice)devreg[i].Device;
							if (r.BusNumber == notInfo.BusNumber && r.DeviceAddress == notInfo.DeviceAddress) {
								DeviceFound (r);
								return;
							}
						} 
					}
                    break;
                case EventType.DeviceRemoveComplete:
					C.Logger.Debug(String.Format("LibUSB device removal [VID:{0},PID:{1},Bus:{2},Addr:{3}]", e.Device.IdVendor, e.Device.IdProduct, notInfo.BusNumber, notInfo.DeviceAddress));
                    if (e.Device != null && e.Device.IdVendor == VID && PIDs.Contains(e.Device.IdProduct))
                    {
						RemoveDevice(string.Format("{0}.{1}", notInfo.BusNumber, notInfo.DeviceAddress));
                    }
                    break;
                default:
				C.Logger.Debug("LibUSB unhandled device event [" + Enum.GetName(typeof(EventType), e.EventType) + "]"); 
                    break;
            }
        }

        private void RemoveDevice(object location)
        {
            C.Logger.Warn("Removing device at location [" + location + "]");
            if (!interfaces.ContainsKey(location)) {
                C.Logger.Warn("OMG this device is not registered?!");
                return;
            }

            if (onConnect != null)
                onConnect(interfaces[location], false);

            interfaces[location].Destroy();
            interfaces.Remove(location);

        }
    }
}
