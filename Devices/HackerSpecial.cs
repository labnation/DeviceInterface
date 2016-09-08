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
        public ScopeFpgaI2cMemory FpgaUserMemory { get; private set; }
        public HackerSpecial(ISmartScopeInterface iface)
        {
            this.iface = iface;
            this.Ready = true;
            this.Serial = iface.Serial;

            memories.Clear();
            byte FPGA_I2C_ADDRESS_SETTINGS = 0x0C;
            byte FPGA_I2C_ADDRESS_ROM = 0x0D;
            byte FPGA_I2C_ADDRESS_USER = 0x0E;

            FpgaSettingsMemory = new ScopeFpgaSettingsMemory(iface, FPGA_I2C_ADDRESS_SETTINGS);
            FpgaRom = new ScopeFpgaRom(iface, FPGA_I2C_ADDRESS_ROM);
            FpgaUserMemory = new ScopeFpgaI2cMemory(iface, FPGA_I2C_ADDRESS_USER, 256);

            memories.Add(FpgaSettingsMemory);
            memories.Add(FpgaRom);
            memories.Add(FpgaUserMemory);

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

        public byte[] GetFpgaData()
        {
            return this.iface.GetData(64);
        }

        public IHardwareInterface HardwareInterface { get { return this.iface; } }
    }
}
