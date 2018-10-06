using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LabNation.DeviceInterface.Hardware
{
    public struct AccessPointInfo
    {
        public string SSID;
        public string BSSID;
        public int Strength;
        public bool TKIP;
        public bool CCMP;
        public string Authentication;
    }

    public interface IWifiBridge : IHardwareInterface
    {
        List<AccessPointInfo> GetAccessPoints();
        string SetAccessPoint(string ssid, string bssid, string enc, string key);
        void Reset();
        void Reboot();
    }
}
