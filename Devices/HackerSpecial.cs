using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.Memories;
using System.IO;
using LabNation.DeviceInterface.DataSources;
using LabNation.DeviceInterface.Hardware;
using LabNation.Common;
using AForge.Math;
using System.Threading.Tasks;
using System.Threading;
#if ANDROID
using Android.Content;
#endif

namespace LabNation.DeviceInterface.Devices
{
    public class HackerSpecial : IDevice
    {
        public ISmartScopeInterface iface { get; private set; }
        public bool Ready { get; private set; }
        public string Serial { get; private set; }

        public ScopeFpgaRom FpgaRom { get; private set; }
        public ScopeFpgaSettingsMemory FpgaSettingsMemory { get; private set; }
        public HackerSpecial(ISmartScopeInterface iface)
        {
            this.iface = iface;
            this.Ready = true;
            this.Serial = iface.Serial;

            memories.Clear();
            FpgaSettingsMemory = new ScopeFpgaSettingsMemory(iface);
            FpgaRom = new ScopeFpgaRom(iface);
            memories.Add(FpgaSettingsMemory);
            memories.Add(FpgaRom);

            //Get FW contents
            string fwName = "SmartScopeHackerSpecial.bin";
            byte[] firmware = Resources.Load(fwName);
            
            if (firmware == null)
                throw new ScopeIOException("Failed to read FW");

            Logger.Info("Got firmware of length " + firmware.Length);
            if (!SmartScopeFlashHelpers.FlashFpga(this.iface, firmware))
                throw new ScopeIOException("failed to flash FPGA");
            
            Logger.Info("FPGA flashed...");

            SmartScopeFlashHelpers.FlashFpga(iface, firmware);
        }

        private List<DeviceMemory> memories = new List<DeviceMemory>();
        public List<DeviceMemory> GetMemories()
        {
            return memories;
        }

        public IHardwareInterface HardwareInterface { get { return this.iface; } }
    }
}
