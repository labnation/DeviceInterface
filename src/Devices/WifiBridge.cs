using LabNation.DeviceInterface.Hardware;

namespace LabNation.DeviceInterface.Devices
{
    public class WifiBridge : IWifiBridge
    {
        private SmartScopeInterfaceEthernet iface;

        public WifiBridge(SmartScopeInterfaceEthernet iface)
        {
            this.iface = iface;
        }

        public string GetAccessPoints()
        {
            //TODO: Parse returned string
            return this.iface.GetAccessPoints();
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
    }
}