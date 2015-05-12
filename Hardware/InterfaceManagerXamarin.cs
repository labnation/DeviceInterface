using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.Common;
using Android.Hardware.Usb;
using Android.Content;
using Android.App;

namespace LabNation.DeviceInterface.Hardware
{
    class InterfaceManagerXamarin: InterfaceManager<InterfaceManagerXamarin>
    {

        UsbManager usbManager;
        private const string ACTION_USB_PERMISSION = "com.lab-nation.smartscope.USB_PERMISSION";
        public static Context context;

        internal class UsbBroadcastReceiver : BroadcastReceiver {

            public delegate void DeviceDelegate(UsbDevice u);
            public DeviceDelegate addDevice;
            public DeviceDelegate removeDevice;
            public DeviceDelegate filterDevice;

            public override void OnReceive(Context c, Intent i)
            {
                if(ACTION_USB_PERMISSION.Equals(i.Action)) 
                {
                    UsbDevice device = (UsbDevice)i.GetParcelableExtra(UsbManager.ExtraDevice);
                    if (i.GetBooleanExtra(UsbManager.ExtraPermissionGranted, false)) {
                        if(device != null){
                            //call method to set up device communication
                            try {
                                addDevice(device);
                            } catch(Exception e) {
                                Logger.Error("Failed to initialize device " + e.Message);
                            }
                        }
                    } 
                    else {
                        Logger.Debug("Permission denied");
                    }
                } else if(i.Action.Equals(UsbManager.ActionUsbDeviceAttached)) {
                    UsbDevice device = (UsbDevice)i.GetParcelableExtra(UsbManager.ExtraDevice);
                    filterDevice(device);
                } else if(i.Action.Equals(UsbManager.ActionUsbDeviceDetached)) {
                    UsbDevice device = (UsbDevice)i.GetParcelableExtra(UsbManager.ExtraDevice);
                    removeDevice(device);
                }
            }
        }
        UsbBroadcastReceiver usbBroadcastReceiver = new UsbBroadcastReceiver();

        protected override void Initialize()
        {
            usbManager = (UsbManager)context.GetSystemService(Android.Content.Context.UsbService);
            usbBroadcastReceiver = new UsbBroadcastReceiver();
            usbBroadcastReceiver.addDevice = AddDevice;
            usbBroadcastReceiver.filterDevice = FilterDevice;
            usbBroadcastReceiver.removeDevice = RemoveDevice;

            IntentFilter f = new IntentFilter(ACTION_USB_PERMISSION);
            context.RegisterReceiver(usbBroadcastReceiver, f);
            context.RegisterReceiver(usbBroadcastReceiver, new IntentFilter(UsbManager.ActionUsbDeviceAttached));
            context.RegisterReceiver(usbBroadcastReceiver, new IntentFilter(UsbManager.ActionUsbDeviceDetached));

        }

        private void AddDevice(UsbDevice d)
        {
            if(!usbManager.HasPermission(d))
                return;
            SmartScopeUsbInterfaceXamarin i = new SmartScopeUsbInterfaceXamarin(context, usbManager, d);
            interfaces.Add(d.DeviceName, i);
            onConnect(i, true);
        }

        private void FilterDevice(UsbDevice usbDevice)
        {
            Logger.Debug(string.Format("Vid:0x{0:X4} Pid:0x{1:X4} - {2}",
                usbDevice.VendorId,
                usbDevice.ProductId,
                usbDevice.DeviceName));

            if ((usbDevice.VendorId == VID) && (PIDs.Contains(usbDevice.ProductId)))
            {
                if (usbManager.HasPermission(usbDevice)) {
                    AddDevice(usbDevice);
                } else {
                    PendingIntent pi = PendingIntent.GetBroadcast(context, 0, new Intent(ACTION_USB_PERMISSION), 0);
                    usbManager.RequestPermission(usbDevice, pi);
                }
            }
        }

        private void RemoveDevice(UsbDevice d)
        {
            if(interfaces.ContainsKey(d.DeviceName)) {
                onConnect(interfaces[d.DeviceName], false);
                interfaces.Remove(d.DeviceName);
            }
        }

        override public void PollDevice()
        {
            IDictionary<string, UsbDevice> usbDeviceList = usbManager.DeviceList;
            Logger.Debug("Total number of USB devices attached: " + usbDeviceList.Count.ToString());

            for (int i = 0; i < usbDeviceList.Count; i++)
            {
                UsbDevice usbDevice = usbDeviceList.ElementAt(i).Value;

                FilterDevice(usbDevice);
            }
        }

    }
}
