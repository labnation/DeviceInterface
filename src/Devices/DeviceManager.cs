using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using LabNation.DeviceInterface.Hardware;
using LabNation.Common;
#if ANDROID
using Android.Content;
#endif

namespace LabNation.DeviceInterface.Devices
{
    public delegate void InterfaceChangeHandler(DeviceManager devManager, List<IHardwareInterface> connectedList);

    public class DeviceManager
    {
        public event InterfaceChangeHandler InterfaceChanged;
        public event DeviceConnectHandler DeviceConnected;
        private IDevice activeDevice = null;
        public IDevice ActiveDevice { get { return activeDevice; } }
        Thread pollThread;
        private List<IHardwareInterface> connectedList = new List<IHardwareInterface>(); //list of all connected devices, serial and type provided

        public List<IHardwareInterface> ConnectedList { get { return this.connectedList; } }
        private Dictionary<Type, Type> InterfaceActivators = new Dictionary<Type, Type>() {
            { typeof(DummyInterface), typeof(DummyScope) },
            { typeof(SmartScopeInterfaceUsb), typeof(SmartScope) },
            { typeof(SmartScopeInterfaceEthernet), typeof(SmartScope) }
        };

#if WINDOWS
        Thread badDriverDetectionThread;
        bool running = true;
        DateTime? lastSmartScopeDetectedThroughWinUsb;
        DateTime? lastSmartScopeDetectedThroughVidPid;
        int WinUsbDetectionWindow = 3000;
        public bool BadDriver { get; private set; }
        int SmartScopeVid = 0x04D8;
        int SmartScopePid = 0xF4B5;
#endif

#if ANDROID
        Context context;
#endif


        public DeviceManager(
#if ANDROID
            Context context
#endif
)
            : this(
#if ANDROID
            context,
#endif
null, null) { }

        public DeviceManager(
#if ANDROID
            Context context,
#endif
            DeviceConnectHandler deviceConnectHandler
)
            : this(
#if ANDROID
            context,
#endif
null, deviceConnectHandler) { }

        public DeviceManager(
#if ANDROID
            Context context,
#endif
            InterfaceChangeHandler interfaceChangeHandler, DeviceConnectHandler deviceConnectHandler, 
            Dictionary<Type, Type> interfaceActivatorOverride = null
            )
        {
#if ANDROID
            this.context = context;
#endif
            this.DeviceConnected = deviceConnectHandler;
            this.InterfaceChanged = interfaceChangeHandler;
            if (interfaceActivatorOverride != null)
            {
                foreach (var kvp in interfaceActivatorOverride)
                {
                    if (kvp.Key.GetInterfaces().Contains(typeof(IHardwareInterface))) 
                    {
                        if (kvp.Value == null)
                            this.InterfaceActivators[kvp.Key] = null;
                        else if(kvp.Value == null || kvp.Value.GetInterfaces().Contains(typeof(IDevice)))
                            this.InterfaceActivators[kvp.Key] = kvp.Value;
                    }
                }
            }
        }

        public void Start(bool async = true)
        {
            pollThread = new Thread(PollUponStart);
            pollThread.Name = "Devicemanager Startup poll";

			//disable because of the crash by Wait
#if ANDROID
			InterfaceManagerServiceDiscovery.context = context;
			InterfaceManagerServiceDiscovery.Instance.onConnect += OnInterfaceChanged;
#elif IOS
			InterfaceManagerApple.Instance.onConnect += OnInterfaceChanged;
#else
            InterfaceManagerZeroConf.Instance.onConnect += OnInterfaceChanged;
#endif

#if ANDROID
            InterfaceManagerXamarin.context = this.context;
            InterfaceManagerXamarin.Instance.onConnect += OnInterfaceChanged;
#elif WINUSB
            InterfaceManagerWinUsb.Instance.onConnect += OnInterfaceChanged;
#elif IOS
			//Nothing for the moment
#else
            InterfaceManagerLibUsb.Instance.onConnect += OnInterfaceChanged;
#endif

            OnInterfaceChanged(DummyInterface.Generator, true);
            //FIXME: android should add audio-scope here!!!

            pollThread.Start();

            if (!async)
                pollThread.Join();
        }

        private void PollUponStart()
        {
#if ANDROID
            InterfaceManagerXamarin.Instance.PollDevice();
#elif WINUSB
            InterfaceManagerWinUsb.Instance.PollDevice();
            badDriverDetectionThread = new Thread(SearchDeviceFromVidPidThread);
            badDriverDetectionThread.Name = "Bad WINUSB driver detection";
            BadDriver = false;
            badDriverDetectionThread.Start();
#elif !IOS
            InterfaceManagerLibUsb.Instance.PollDevice();
#endif
        }

        public void Stop()
        {
            if (activeDevice != null && activeDevice is IScope)
                (activeDevice as IScope).DataSourceScope.Stop();
            
            if(pollThread != null)
                pollThread.Join(100);
#if ANDROID
			InterfaceManagerServiceDiscovery.Instance.Destroy();
#elif IOS
			InterfaceManagerApple.Instance.Destroy();
#else
            InterfaceManagerZeroConf.Instance.Destroy();
#endif

#if ANDROID
            //Nothing to do here, just keeping same ifdef structure as above
#elif WINDOWS
            BadDriver = false;
            running = false;
            if(badDriverDetectionThread != null)
                badDriverDetectionThread.Join(100);
#elif IOS
			//Nothing for the moment
#else
            //Linux, MacOS
            InterfaceManagerLibUsb.Instance.Destroy();
#endif
        }

        private void OnInterfaceChanged(IHardwareInterface hardwareInterface, bool connected)
        {
            if(connected) {
                connectedList.Add(hardwareInterface);

                #if WINDOWS
                lastSmartScopeDetectedThroughWinUsb = DateTime.Now;
				#endif
            }
            else
            {
                if (connectedList.Contains(hardwareInterface))
                    connectedList.Remove(hardwareInterface);
                
                #if WINDOWS
                lastSmartScopeDetectedThroughWinUsb = null;
				#endif
            }

            /* If application handles interface preferences, pass it the updated
             * list of connected interfaces for it to decide who to call SetActiveDevice
             * with
             */
            if (InterfaceChanged != null)
                InterfaceChanged(this, connectedList);
            /* Else activate the lastly connected interface */
            else
                SetActiveDevice(connectedList.Last());
        }

        public void SetActiveDevice(IHardwareInterface iface)
        {
            if (!connectedList.Contains(iface))
                return;

            // Don't handle a second activation if the interface
            // has already been activated
            if (activeDevice != null && activeDevice.HardwareInterface == iface)
                return;

            // Activate new device
            Type DeviceType = null;
            Type ifaceType = iface.GetType();
            foreach (Type t in this.InterfaceActivators.Keys)
            {
                if (ifaceType == t || ifaceType.IsSubclassOf(t) || ifaceType.GetInterfaces().Contains(t))
                {
                    DeviceType = InterfaceActivators[t];
                    break;
                }
            }

            if (DeviceType == null)
            {
                Logger.Error("Unsupported interface type " + iface.GetType().FullName);
                return;
            }

            try
            {
                IDevice newDevice = (IDevice)Activator.CreateInstance(DeviceType, iface);
                
                if (activeDevice != null)
                {
                    if (DeviceConnected != null)
                        DeviceConnected(activeDevice, false);
                    if (activeDevice is IDisposable)
                        (activeDevice as IDisposable).Dispose();
                }

                activeDevice = newDevice;
                if (DeviceConnected != null)
                    DeviceConnected(activeDevice, true);
            }
            catch(Exception e)
            {
                Logger.Error("Failed to create device: " + e.Message);
            }
        }
		public void Pause()
		{
			if (activeDevice is IScope)
				((IScope)activeDevice).Pause();
#if ANDROID
			InterfaceManagerServiceDiscovery.Instance.Pause();
#endif
		}

		public void Resume()
		{
			if (activeDevice is IScope)
				((IScope)activeDevice).Resume();
#if ANDROID
			InterfaceManagerServiceDiscovery.Instance.Resume();
#endif

		}

#if WINDOWS
        public void WinUsbPoll()
        {
            InterfaceManagerWinUsb.Instance.PollDevice();
        }

        private void SearchDeviceFromVidPidThread()
        {
            while (running)
            {
                Thread.Sleep(500);
                //Abort this thread once a device is found through WinUSB
                if (lastSmartScopeDetectedThroughWinUsb != null)
                {
                    Logger.Debug("Good winusb driver!");
                    BadDriver = false;
                    running = false;
                    return;
                }

                //Try to find a device through the system management stuff
                string serial = null;
                if (LabNation.Common.Utils.TestUsbDeviceFound(SmartScopeVid, SmartScopePid, out serial))
                {
                    lastSmartScopeDetectedThroughVidPid = DateTime.Now;
                    Logger.Debug(String.Format("Update vidpid detection time to {0}", lastSmartScopeDetectedThroughVidPid));
                }

                //A device was found using VID/PID at least <WinUsbDetectionWindow>ms ago
                if (lastSmartScopeDetectedThroughVidPid != null)
                {
                    //Wait <WinUsbDetectionWindow>ms for corresponding WinUSB detection
                    do
                    {
                        //If a device came in through WinUSB, stop this detection
                        if (lastSmartScopeDetectedThroughWinUsb != null)
                        {
                            Logger.Debug("Good winusb driver!");
                            BadDriver = false;
                            running = false;
                            return;
                        }
                        Thread.Sleep(100);
                    } while ((DateTime.Now - lastSmartScopeDetectedThroughVidPid.Value).TotalMilliseconds < WinUsbDetectionWindow);

                    //If no winusb device came in during the detection window, flag a bad driver
                    BadDriver = true;
                    Logger.Warn("Bad WINUSB driver");
                }
            }
        }
#endif
    }
}
