using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using ECore.HardwareInterfaces;
using Common;
#if ANDROID
using Android.Content;
#endif

namespace ECore.Devices
{
    public class DeviceManager
    {
        DeviceConnectHandler connectHandler;
        IDevice device;
        IDevice fallbackDevice;
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
            DeviceConnectHandler connectHandler
            )
        {
#if ANDROID
            this.context = context;
#endif
            this.connectHandler = connectHandler;

            /* Register fallback device */
            fallbackDevice = new DummyScope();
            connectHandler(fallbackDevice, true);

#if ANDROID
            InterfaceManagerXamarin.context = this.context;
            InterfaceManagerXamarin.Instance.onConnect += OnDeviceConnect;
            InterfaceManagerXamarin.Instance.PollDevice();
#elif WINUSB
            InterfaceManagerWinUsb.Instance.onConnect += OnDeviceConnect;
            InterfaceManagerWinUsb.Instance.PollDevice();
            badDriverDetectionThread = new Thread(SearchDeviceFromVidPidThread);
            badDriverDetectionThread.Name = "Bad WINUSB driver detection";
            BadDriver = false;
            badDriverDetectionThread.Start();
#else
            InterfaceManagerLibUsb.Instance.onConnect += OnDeviceConnect;
            #if !IOS
            InterfaceManagerLibUsb.Instance.PollDevice();
            #endif
#endif
        }

        public void Stop()
        {
#if WINDOWS
            BadDriver = false;
            running = false;
            badDriverDetectionThread.Join(100);
#endif
        }

        private void OnDeviceConnect(ISmartScopeUsbInterface hardwareInterface, bool connected)
        {
            if(connected) {
                if(device == null)
                {
                    device = new SmartScope(hardwareInterface);
                    connectHandler(fallbackDevice, false);
					#if WINDOWS
                    lastSmartScopeDetectedThroughWinUsb = DateTime.Now;
                    Logger.Debug(String.Format("Update winusb detection time to {0}", lastSmartScopeDetectedThroughWinUsb));
					#endif
                    connectHandler(device, true);
                }
            }
            else 
            {
                if (device is SmartScope)
                {
                	Logger.Debug("DeviceManager: Calling connect handler");
                    connectHandler(device, false);
					Logger.Debug("DeviceManager: disposing device");
                    (device as SmartScope).Dispose();
                    device = null;
					#if WINDOWS
                    lastSmartScopeDetectedThroughWinUsb = null;
					#endif
					Logger.Debug("DeviceManager: calling connect for fallback device");
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
                if (Common.Utils.TestUsbDeviceFound(SmartScopeVid, SmartScopePid, out serial))
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
