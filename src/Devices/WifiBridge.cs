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
            //TODO: Parse returned string
            return ParseAccessPoints(this.iface.GetAccessPoints());
        }
        public string SetAccessPoint(string ssid, string bssid, string enc, string key)
        {
            return this.iface.SetAccessPoint(ssid, bssid, enc, key);
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
                }
            }

            return aps;
        }

    }
}