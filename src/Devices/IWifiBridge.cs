using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LabNation.DeviceInterface.Hardware
{
    public interface IWifiBridge : IHardwareInterface
    {
        string GetAccessPoints();
        string SetAccessPoint(string ssid, string bssid, string enc, string key);
        void Reset();
        void Reboot();
    }
}
