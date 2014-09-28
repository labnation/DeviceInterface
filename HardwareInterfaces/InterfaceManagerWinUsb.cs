using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using MadWizard.WinUSBNet;
using System.Windows.Forms;
using C=Common;
using Common;

namespace ECore.HardwareInterfaces
{
    //class that provides raw HW access to the device
    internal class InterfaceManagerWinUsb : InterfaceManager<InterfaceManagerWinUsb>
    {
        Form winUsbForm;
        USBNotifier notifier;

        protected override void Initialize()
        {
            winUsbForm = new Form();
            notifier = new USBNotifier(winUsbForm, guid);
            winUsbForm.Name = "WinUSB form";
            notifier.Arrival += OnDeviceArrival;
            notifier.Removal += OnDeviceRemoval;
            winUsbForm.Size = new System.Drawing.Size(0, 0);
            winUsbForm.WindowState = FormWindowState.Minimized;
            winUsbForm.ShowInTaskbar = false;
            winUsbForm.Enabled = false;
            winUsbForm.Show();
            USBDeviceInfo[] usbDeviceList = USBDevice.GetDevices(guid);
			C.Logger.Debug("Total number of Win USB devices attached: " + usbDeviceList.Length.ToString());
            foreach (USBDeviceInfo device in usbDeviceList)
            {
                string sAdd = string.Format("Vid:0x{0:X4} Pid:0x{1:X4} (dsc:{2}) - path:{3}",
                                            device.VID,
                                            device.PID,
                                            device.DeviceDescription, device.DevicePath);

				C.Logger.Debug(sAdd);
            }
        }

        public override void PollDevice()
        {
            if (interfaces.Count > 0 && onConnect != null)
            {
                onConnect(interfaces.First().Value, true);
                return;
            }

            foreach (int PID in PIDs)
            {
                USBDeviceInfo[] devs = USBDevice.GetDevices(guid);
                foreach(var dev in devs)
                {
                    if (PIDs.Contains(dev.PID) && VID == dev.VID)
                    {
                        Thread.Sleep(10);
                        try
                        {
                            USBDevice d = new USBDevice(dev);
                            if (DeviceFound(d))
                                return;
                        }
                        catch (USBException e)
                        {
                            Logger.Warn("Though a device was found, we failed to capture it: " + e.Message);
                            return;
                        }

                        
                    }
                }
            }
        }

        //called at init, and each time a system event occurs
        private void OnDeviceArrival(Object sender, USBEvent e)
        {
            USBDevice d = new USBDevice(e.DevicePath);
            DeviceFound(d);
        }

        private void OnDeviceRemoval(Object sender, USBEvent e)
        {
            RemoveDevice(e.DevicePath);
        }

        private bool DeviceFound(USBDevice dev)
        {
            string serial = null;
            try
            {
                SmartScopeUsbInterfaceWinUsb f = new SmartScopeUsbInterfaceWinUsb(dev);
                //FIXME: should use ScopeUsbDevice.serial but not set with smartscope
                serial = dev.Descriptor.SerialNumber;
                if (serial == "" || serial == null)
                    throw new ScopeIOException("This device doesn't have a serial number, can't work with that");
                if (interfaces.ContainsKey(serial))
                    throw new ScopeIOException("This device was already registered. This is a bug");
                C.Logger.Debug("Device found with serial [" + serial + "]");
                interfaces.Add(dev.Descriptor.PathName, f);

                if (onConnect != null)
                    onConnect(f, true);
                return true;
            }
            catch (ScopeIOException e)
            {
                C.Logger.Error("Error while trying to connect to device event handler: " + e.Message);
                if (serial != null)
                    interfaces.Remove(dev.Descriptor.PathName);
                return false;
            }
        }

        private void RemoveDevice(object devicePath)
        {
            C.Logger.Debug("Removing device on path [" + devicePath + "]");
            if (!interfaces.ContainsKey(devicePath))
                return;

            if (onConnect != null)
                onConnect(interfaces[devicePath], false);

            interfaces[devicePath].Destroy();
            interfaces.Remove(devicePath);

        }
    }
}
