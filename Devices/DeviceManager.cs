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
    public enum WaveSource
    {
        FILE,
        GENERATOR,
        AUDIO,
        SMARTSCOPE_USB,
        SMARTSCOPE_NETWORK
    }

    public delegate void InterfaceStatusChangeHandler(DeviceManager devManager, Dictionary<string, WaveSource> connectedList);

    public class DeviceManager
    {
        InterfaceStatusChangeHandler interfaceChangeHandler;
        DeviceConnectHandler deviceConnectHandler;
        private IDevice mainDevice = null;
        Thread pollThread;
        private Dictionary<string, WaveSource> connectedList = new Dictionary<string, WaveSource>(); //list of all connected devices, serial and type provided
        private Dictionary<string, ISmartScopeUsbInterface> interfaceList = new Dictionary<string, ISmartScopeUsbInterface>(); //list of all detected interfaces. Meaning hardware-only
        private Dictionary<string, IScope> deviceList = new Dictionary<string, IScope>(); // list of all devices which have actually been created.

        public Dictionary<string, WaveSource> ConnectedList { get { return this.connectedList; } }

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
            Context context,
#endif
            InterfaceStatusChangeHandler interfaceChangeHandler, DeviceConnectHandler deviceConnectHandler
            )
        {
#if ANDROID
            this.context = context;
#endif
            this.deviceConnectHandler = deviceConnectHandler;
            this.interfaceChangeHandler = interfaceChangeHandler;

            /* Register always-present devices */
            connectedList.Add(DummyScope.FakeSerial, WaveSource.GENERATOR);
            //FIXME: android should add audio-scope here!!!
        }

        public void Start(bool async = true)
        {
            pollThread = new Thread(PollUponStart);
            pollThread.Name = "Devicemanager Startup poll";
            InterfaceManagerZeroConf.Instance.onConnect += OnDeviceConnect;
#if ANDROID
            InterfaceManagerXamarin.context = this.context;
            InterfaceManagerXamarin.Instance.onConnect += OnDeviceConnect;
#elif WINUSB
            InterfaceManagerWinUsb.Instance.onConnect += OnDeviceConnect;
#elif IOS
			//Nothing for the moment
#else
            InterfaceManagerLibUsb.Instance.onConnect += OnDeviceConnect;
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

        private void OnDeviceConnect(ISmartScopeUsbInterface hardwareInterface, bool connected)
        {
            string serial = hardwareInterface.Serial;
            if(connected) {                
                connectedList[serial] = WaveSource.SMARTSCOPE_USB;
                interfaceList[serial] = hardwareInterface;

                #if WINDOWS
                lastSmartScopeDetectedThroughWinUsb = DateTime.Now;
                Logger.Debug(String.Format("Update winusb detection time to {0}", lastSmartScopeDetectedThroughWinUsb));
				#endif

                Logger.Debug("DeviceManager: calling connectHandler after new Connect event");
            }
            else{
                if (connectedList.ContainsKey(serial))
                    connectedList.Remove(serial);
                if (interfaceList.ContainsKey(serial))
                    interfaceList.Remove(serial);
                if (deviceList.ContainsKey(serial))
                {                    
                    //need to dispose smartscope here: when it's being unplugged
                    Logger.Debug("DeviceManager: disposing device");
                    if (mainDevice is SmartScope)
                        (mainDevice as SmartScope).Dispose();

                    deviceList.Remove(serial);
                }

                #if WINDOWS
                lastSmartScopeDetectedThroughWinUsb = null;
				#endif

                Logger.Debug("DeviceManager: calling connectHandler after new Disconnect event");
            }

            if (interfaceChangeHandler != null)
                interfaceChangeHandler(this, connectedList);
        }

        public void SwitchMainDevice(string serial)
        {
            if (!connectedList.ContainsKey(serial))
                return;

            //when changing device -> first fire previous device
            if (mainDevice != null && mainDevice.Serial != serial)
            {
                if (deviceConnectHandler != null)
                    deviceConnectHandler(mainDevice, false);               
            }

            //activate new device
            if (serial == DummyScope.FakeSerial)
                mainDevice = new DummyScope();
            //FIXME: need to add support for AudioScope, FromFileScope
            //else if (serial == AudioScope.FakeSerial)
            //  mainDevice = new AudioScope();
            else //real SmartScope
            {
                //need to make sure a smartscope is created only once from an interface
                if (!deviceList.ContainsKey(serial))
                    deviceList[serial] = new SmartScope(interfaceList[serial]);

                mainDevice = deviceList[serial];
            }

            if (deviceConnectHandler != null)
                deviceConnectHandler(mainDevice, true);
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
