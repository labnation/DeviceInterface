using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.Hardware;

namespace LabNation.DeviceInterface.Devices
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
        Version Version { get; }
        List<AccessPointInfo> GetAccessPoints();
        void SetAccessPoint(string ssid, string bssid, string enc, string key);
        void Reset();
        void Reboot();
    }
}
