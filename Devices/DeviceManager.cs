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
        private IScope mainDevice = null;
        public IScope MainDevice { get { return mainDevice; } }
        public bool SmartScopeConnected { get { return mainDevice is SmartScope; } }
        Thread pollThread;
        private List<IHardwareInterface> connectedList = new List<IHardwareInterface>(); //list of all connected devices, serial and type provided
        private List<IScope> deviceList = new List<IScope>(); // list of all devices which have actually been created.

        public List<IHardwareInterface> ConnectedList { get { return this.connectedList; } }

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
null, null) { Start();  }

        public DeviceManager(
#if ANDROID
            Context context,
#endif
            InterfaceChangeHandler interfaceChangeHandler, DeviceConnectHandler deviceConnectHandler
            )
        {
#if ANDROID
            this.context = context;
#endif
            this.DeviceConnected = deviceConnectHandler;
            this.InterfaceChanged = interfaceChangeHandler;

            connectedList.Add(DummyInterface.Generator);
            //FIXME: android should add audio-scope here!!!
        }

        public void Start(bool async = true)
        {
            pollThread = new Thread(PollUponStart);
            pollThread.Name = "Devicemanager Startup poll";

            //disable because of the crash by Wait
            //InterfaceManagerZeroConf.Instance.onConnect += OnHardwareConnect;
#if ANDROID
            InterfaceManagerXamarin.context = this.context;
            InterfaceManagerXamarin.Instance.onConnect += OnHardwareConnect;
#elif WINUSB
            InterfaceManagerWinUsb.Instance.onConnect += OnHardwareConnect;
#elif IOS
			//Nothing for the moment
#else
            InterfaceManagerLibUsb.Instance.onConnect += OnHardwareConnect;
#endif

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
            //need to stop the ZeroConf polling thread
            InterfaceManagerZeroConf.Instance.Destroy();

            if(pollThread != null)
                pollThread.Join(100);
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

        private void OnHardwareConnect(IHardwareInterface hardwareInterface, bool connected)
        {
            if(connected) {
                connectedList.Add(hardwareInterface);

                #if WINDOWS
                lastSmartScopeDetectedThroughWinUsb = DateTime.Now;
                Logger.Debug(String.Format("Update winusb detection time to {0}", lastSmartScopeDetectedThroughWinUsb));
				#endif

                Logger.Debug("DeviceManager: calling connectHandler after new Connect event");
            }
            else
            {
                if (connectedList.Contains(hardwareInterface))
                    connectedList.Remove(hardwareInterface);
                IScope device = deviceList.Where(x => x.HardwareInterface == hardwareInterface).FirstOrDefault();
                if (device != null)
                {                    
                    //need to dispose smartscope here: when it's being unplugged
                    Logger.Debug("DeviceManager: disposing device");
                    if (device is SmartScope)
                        (device as SmartScope).Dispose();

                    deviceList.Remove(device);
                }

                #if WINDOWS
                lastSmartScopeDetectedThroughWinUsb = null;
				#endif

                Logger.Debug("DeviceManager: calling connectHandler after new Disconnect event");
            }

            if (InterfaceChanged != null)
                InterfaceChanged(this, connectedList);
            else
            {
                //in case no event handlers are specified: connect real smartscope if none was active yet, or switch to dummymode
                if (connected && !(mainDevice is SmartScope))
                {
                    //at this point, no real smartscope was attached, and a USB or ethernet scope was detected
                    SwitchMainDevice(hardwareInterface);
                }
                else
                {
                    SwitchMainDevice(null);
                }
            }
        }

        public void SwitchMainDevice(IHardwareInterface iface)
        {
            if (!connectedList.Contains(iface))
                return;

            //when changing device -> first fire previous device
            if (mainDevice != null && mainDevice.HardwareInterface != iface)
            {
                if (DeviceConnected != null)
                    DeviceConnected(mainDevice, false);               
            }

            //activate new device
            if (iface is DummyInterface)
                mainDevice = new DummyScope(iface as DummyInterface);
            else if(iface is ISmartScopeInterface) //real SmartScope
            {
                //need to make sure a smartscope is created only once from an interface
                if (deviceList.Where(x => x.HardwareInterface == iface).Count() == 0)
                    deviceList.Add(new SmartScope(iface as ISmartScopeInterface));

                mainDevice = deviceList.Where(x => x.HardwareInterface == iface).First();
            }

            if (DeviceConnected != null)
                DeviceConnected(mainDevice, true);
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
