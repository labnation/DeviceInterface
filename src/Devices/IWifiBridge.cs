using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.Hardware;

namespace LabNation.DeviceInterface.Devices
{
    [Flags]
    public enum ApCapabilities
    {
        //From iw source:scan.c https://git.kernel.org/pub/scm/linux/kernel/git/jberg/iw.git 
        ESS = 1 << 0,
        IBSS = 1 << 1,
        CF_POLLABLE = 1 << 2,
        CF_POLL_REQUEST = 1 << 3,
        PRIVACY = 1 << 4,
        SHORT_PREAMBLE = 1 << 5,
        PBCC = 1 << 6,
        CHANNEL_AGILITY = 1 << 7,
        SPECTRUM_MGMT = 1 << 8,
        QOS = 1 << 9,
        SHORT_SLOT_TIME = 1 << 10,
        APSD = 1 << 11,
        RADIO_MEASURE = 1 << 12,
        DSSS_OFDM = 1 << 13,
        DEL_BACK = 1 << 14,
        IMM_BACK = 1 << 15,
    }   
    public struct AccessPointInfo
    {
        public string SSID;
        public string BSSID;
        public int Strength;
        public ApCapabilities capabilities;
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
