using LabNation.DeviceInterface.Hardware;
using System;
using System.Collections.Generic;

namespace LabNation.DeviceInterface.Devices
{
    public class WifiBridge : IWifiBridge
    {
        private SmartScopeInterfaceEthernet iface;

        public WifiBridge(SmartScopeInterfaceEthernet iface)
        {
            this.iface = iface;
        }

        public List<AccessPointInfo> GetAccessPoints()
        {
            return ParseAccessPoints(this.iface.GetAccessPoints());
        }
        public void SetAccessPoint(string ssid, string bssid, string enc, string key)
        {
            this.iface.SetAccessPoint(ssid, bssid, enc, key);
        }
        public void Reset()
        {
            this.iface.BridgeReset();
        }
        public void Reboot()
        {
            this.iface.BridgeReboot();
        }

        public string Serial
        {
            get
            {
                //TODO: Implement
                return "NOT IMPLEMENTED YET";
            }
        }

        private List<AccessPointInfo> ParseAccessPoints(string rawAnswer)
        {
            string[] lines = rawAnswer.Split(new[] { "\n" }, StringSplitOptions.None);

            int lineID = 0;
            List<AccessPointInfo> aps = new List<AccessPointInfo>();
            AccessPointInfo ap = new AccessPointInfo();
            while (lineID < lines.Length)
            {
                string line = lines[lineID++];

                //new AP
                if (line.IndexOf("BSS ") == 0)
                {
                    if (ap.SSID != null)
                        aps.Add(ap);
                    ap = new AccessPointInfo();
                    ap.BSSID = line.Split(new string[] { " ", "(" }, StringSplitOptions.RemoveEmptyEntries)[1];
                    ap.TKIP = false; //to be sure
                    ap.CCMP = false; //to be sure
                }
                else
                {
                    //for each AP: browse
                    line = line.Replace("\t", "");
                    line = line.Trim();

                    if (line.IndexOf("SSID: ") == 0)
                        ap.SSID = line.Substring("SSID: ".Length);
                    else if (line.IndexOf("signal: ") == 0)
                    {
                        string signal = line.Substring("signal: ".Length);
                        string[] splits = signal.Split(new string[] { " ", "." }, StringSplitOptions.RemoveEmptyEntries);
                        ap.Strength = int.Parse(splits[0]);
                    }
                    else if (line.IndexOf("* Authentication suites: ") == 0)
                    {
                        ap.Authentication = line.Substring("* Authentication suites: ".Length);
                    }
                    else if (line.IndexOf("* Pairwise ciphers: ") == 0)
                    {
                        if (line.IndexOf("CCMP") > 0) ap.CCMP = true;
                        if (line.IndexOf("TKIP") > 0) ap.TKIP = true;
                    }
                }
            }

            return aps;
        }

        public Version Version
        {
            get
            {
                byte[] b = this.iface.ServerVersion;
                return new Version(b[1], b[0]);
            }
        }
    }
}