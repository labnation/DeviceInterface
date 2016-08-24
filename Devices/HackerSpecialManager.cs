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
    public class HackerSpecialManager
    {
        public event DeviceConnectHandler DeviceConnected;
        private IDevice device = null;
        public IDevice Device { get { return device; } }
        
        Thread pollThread;

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


        public HackerSpecialManager(
#if ANDROID
            Context context
#endif
)
            : this(
#if ANDROID
            context,
#endif
            null) { }

        public HackerSpecialManager(
#if ANDROID
            Context context
#endif
            DeviceConnectHandler deviceConnectHandler
)
        {
#if ANDROID
            this.context = context;
#endif
            this.DeviceConnected = deviceConnectHandler;
        }

        public void Start(bool async = true)
        {
            pollThread = new Thread(PollUponStart);
            pollThread.Name = "Devicemanager Startup poll";

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
            string serial = hardwareInterface.Serial;
            if (!(hardwareInterface is ISmartScopeInterface))
            {
                Logger.Info("Ignoring hardwareinterface since not ISmartScopeInterface but " + hardwareInterface.GetType().ToString());
                return;
            }
            ISmartScopeInterface ssIface = hardwareInterface as ISmartScopeInterface;
            if(connected) {
                this.device = new HackerSpecial(ssIface);

                #if WINDOWS
                lastSmartScopeDetectedThroughWinUsb = DateTime.Now;
                Logger.Debug(String.Format("Update winusb detection time to {0}", lastSmartScopeDetectedThroughWinUsb));
				#endif

                if (this.DeviceConnected != null)
                    DeviceConnected(this.device, true);
            }
            else
            {
                if (this.device != null && this.device.HardwareInterface == hardwareInterface)
                {
                    if (this.DeviceConnected != null)
                        DeviceConnected(this.device, false);
                }
                #if WINDOWS
                lastSmartScopeDetectedThroughWinUsb = null;
				#endif
            }
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
