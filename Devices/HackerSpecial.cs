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
        public ScopeFpgaI2cMemory FpgaSettingsMemory { get; private set; }
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

            FpgaSettingsMemory = new ScopeFpgaI2cMemory(iface, FPGA_I2C_ADDRESS_SETTINGS, 39);
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

        public void ConfigureAdc()
        {
            WriteAdcReg(0x0A, 0x5A); //SW reset
            WriteAdcReg(0x00, 0x04); //Complete power down
            WriteAdcReg(0x02, 0x00); //Activeate DOR & DCLK
            WriteAdcReg(0x06, 0x10); //Offset binary encoding
            WriteAdcReg(0x03, 0x38); //Configure timing
            WriteAdcReg(0x04, 0x02); //DATA 100 ohm termination
            WriteAdcReg(0x00, 0x03); //Power on
            WriteAdcReg(0x01, 0x02); //DDR data on bus
        }

        public const uint REG_SPI_ADDR          = 1;
        public const uint REG_SPI_VAL           = 2;
        public const uint REG_CHA_GAIN          = 3;
        public const uint REG_CHB_GAIN          = 4;
        public const uint REG_CHA_YPOS          = 5;
        public const uint REG_CHB_YPOS          = 6;

        public const uint REG_FLAGS             = 7;
        public const byte B_FLAGS_SPI_CMD       = 0;
        public const byte B_FLAGS_CHA_AC_DC     = 1;
        public const byte B_FLAGS_CHB_AC_DC     = 2;
        public const uint ROM_SPI_VAL           = 4;

        public byte ReadAdcReg(uint address)
        {
            FpgaSettingsMemory[REG_SPI_ADDR].WriteImmediate((byte)(address + 128)); //for a read, MSB must be 1

            //next, trigger rising edge to initiate SPI comm
            FpgaSettingsMemory[REG_FLAGS].ClearBit(B_FLAGS_SPI_CMD).WriteImmediate();
            FpgaSettingsMemory[REG_FLAGS].SetBit(B_FLAGS_SPI_CMD).WriteImmediate();

            //finally read acquired value
            return FpgaRom[ROM_SPI_VAL].Read().GetByte();
        }

        public void WriteAdcReg(uint address, byte value)
        {
            FpgaSettingsMemory[REG_SPI_ADDR].WriteImmediate((byte)(address)); //for a read, MSB must be 1

            FpgaSettingsMemory[REG_SPI_VAL].WriteImmediate(value);
            //next, trigger rising edge to initiate SPI comm
            FpgaSettingsMemory[REG_FLAGS].ClearBit(B_FLAGS_SPI_CMD).WriteImmediate();
            FpgaSettingsMemory[REG_FLAGS].SetBit(B_FLAGS_SPI_CMD).WriteImmediate();
        }

        public IHardwareInterface HardwareInterface { get { return this.iface; } }
    }
}
