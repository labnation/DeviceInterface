using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Common;
using Android.Hardware.Usb;
using Android.Content;

namespace ECore.HardwareInterfaces
{
    class InterfaceManagerXamarin: InterfaceManager<InterfaceManagerXamarin>
    {
        UsbManager usbManager;
        public Context context;

        protected override void Initialize()
        {
        }

        override public void PollDevice()
        {
            usbManager = (UsbManager)context.GetSystemService(Android.Content.Context.UsbService);
            IDictionary<string, UsbDevice> usbDeviceList = usbManager.DeviceList;
            Logger.Debug("Total number of USB devices attached: " + usbDeviceList.Count.ToString());

            UsbDevice smartScope = null;
            for (int i = 0; i < usbDeviceList.Count; i++)
            {
                UsbDevice usbDevice = usbDeviceList.ElementAt(i).Value;

                Logger.Debug(string.Format("Vid:0x{0:X4} Pid:0x{1:X4} - {2}",
                    usbDevice.VendorId,
                    usbDevice.ProductId,
                    usbDevice.DeviceName));

                if ((usbDevice.VendorId == VID) && (PIDs.Contains(usbDevice.ProductId)))
                {
                    Logger.Info("SmartScope connected!");
                    smartScope = usbDevice;
                    break;
                }
            }

            //if device is attached
            if (smartScope != null)
            {
                Logger.Debug("Device attached to USB port");
                try {
                    ISmartScopeUsbInterface i = new SmartScopeUsbInterfaceXamarin(context, usbManager, smartScope);
                    interfaces.Add(i.GetSerial(), i);
                } catch (Exception e) {
                    Logger.Error("Something went wrong initialising the device " + e.Message);
                }
            }
            else
            {
                Logger.Debug("No device found");
            }
        }

    }
}
