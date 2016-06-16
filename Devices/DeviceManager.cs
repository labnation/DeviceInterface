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
    public class DeviceManager
    {
        DeviceConnectHandler connectHandler;
        public IDevice device { get; private set; }
        public IDevice fallbackDevice { get; private set; }
        Thread pollThread;

        //FIXME: only purpose of following properties is to allow LabView/Matlab polling. Not yet integrating etherscope etc, as this is still under full construction
        public bool SmartScopeConnected { get { return device is SmartScope; } }
        public IScope MainDevice
        {
            get
            {
                if (device is IScope)
                    return device as IScope;
                else
                    return null;
            }
        }
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

        //FIXME: empty constructor, only purpose is to allow LabView/Matlab polling. Not yet integrating etherscope etc, as this is still under full construction
        public DeviceManager()
        {
            fallbackDevice = new DummyScope();
            Start();
        }

        public DeviceManager(
#if ANDROID
            Context context,
#endif
DeviceConnectHandler connectHandler
            )
        {
#if ANDROID
            this.context = context;
#endif
            this.connectHandler = connectHandler;

            /* Register fallback device */
            fallbackDevice = new DummyScope();
        }

        public void Start(bool async = true)
        {
            pollThread = new Thread(PollUponStart);
            pollThread.Name = "Devicemanager Startup poll";
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
            if (pollThread != null)
                pollThread.Join(100);
#if ANDROID
            //Nothing to do here, just keeping same ifdef structure as above
#elif WINDOWS
            BadDriver = false;
            running = false;
            if (badDriverDetectionThread != null)
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
            if (connected)
            {
                if (device == null)
                {
                    device = new SmartScope(hardwareInterface);
                    if (connectHandler != null)
                        connectHandler(fallbackDevice, false);

#if WINDOWS
                    lastSmartScopeDetectedThroughWinUsb = DateTime.Now;
                    Logger.Debug(String.Format("Update winusb detection time to {0}", lastSmartScopeDetectedThroughWinUsb));
#endif

                    if (connectHandler != null)
                        connectHandler(device, true);
                }
            }
            else
            {
                if (device is SmartScope)
                {
                    Logger.Debug("DeviceManager: Calling connect handler");
                    if (connectHandler != null)
                        connectHandler(device, false);
                    Logger.Debug("DeviceManager: disposing device");
                    (device as SmartScope).Dispose();
                    device = null;
#if WINDOWS
                    lastSmartScopeDetectedThroughWinUsb = null;
#endif
                    Logger.Debug("DeviceManager: calling connect for fallback device");
                    if (connectHandler != null)
                        connectHandler(fallbackDevice, true);
                }
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