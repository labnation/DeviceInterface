using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.HardwareInterfaces;
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
#else
            InterfaceManagerLibUsb.Instance.onConnect += OnDeviceConnect;
            InterfaceManagerLibUsb.Instance.PollDevice();
#endif
        }

        private void OnDeviceConnect(ISmartScopeUsbInterface hardwareInterface, bool connected)
        {
            if(connected) {
                if(device == null)
                {
                    device = new SmartScope(hardwareInterface);
                    connectHandler(fallbackDevice, false);
                    connectHandler(device, true);
                }
            }
            else 
            {
                if (device is SmartScope)
                {
                    connectHandler(device, false);
                    (device as SmartScope).Dispose();
                    device = null;
                    connectHandler(fallbackDevice, true);
                }
            }
        }
    }
}
