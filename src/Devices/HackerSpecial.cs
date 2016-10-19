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

        public ByteMemoryEnum<ROM> FpgaRom { get; private set; }
        public ByteMemoryEnum<REG> FpgaSettingsMemory { get; private set; }
        public ScopeFpgaI2cMemory FpgaUserMemory { get; private set; }

        public enum REG
        {
            POWER = 0,
            SPI_ADDR = 1,
            SPI_VAL = 2,
            CHA_GAIN = 3,
            CHB_GAIN = 4,
            CHA_YPOS = 5,
            CHB_YPOS = 6,
            FLAGS = 7
        };

        public enum FLAG
        {
            SPI_CMD = 0,
            CHA_AC_DC = 1,
            CHB_AC_DC = 2,
        };

        public enum ROM
        {
            SPI_VAL = 4,
        };

        public HackerSpecial(ISmartScopeInterface iface)
        {
            this.iface = iface;
            this.Ready = true;
            this.Serial = iface.Serial;

            memories.Clear();
            byte FPGA_I2C_ADDRESS_SETTINGS = 0x0C;
            byte FPGA_I2C_ADDRESS_ROM = 0x0D;
            byte FPGA_I2C_ADDRESS_USER = 0x0E;

            FpgaSettingsMemory = new ByteMemoryEnum<REG>(new ScopeFpgaI2cMemory(iface, FPGA_I2C_ADDRESS_SETTINGS, 39));
            FpgaRom = new ByteMemoryEnum<ROM>(new ScopeFpgaI2cMemory(iface, FPGA_I2C_ADDRESS_ROM, 256, true));
            FpgaUserMemory = new ScopeFpgaI2cMemory(iface, FPGA_I2C_ADDRESS_USER, 256);

            memories.Add(FpgaSettingsMemory);
            memories.Add(FpgaRom);
            memories.Add(FpgaUserMemory);

            //Get FW contents
            string fwName = "blobs.SmartScopeHackerSpecial.bin";
            byte[] firmware = Resources.Load(fwName);
            
            if (firmware == null)
                throw new ScopeIOException("Failed to read FW");

            Logger.Info("Got firmware of length " + firmware.Length);
            if (!FlashFPGA(firmware))
                throw new ScopeIOException("failed to flash FPGA");
            
            Logger.Info("FPGA flashed...");

            SmartScopeFlashHelpers.FlashFpga(iface, firmware);
        }

        private List<DeviceMemory> memories = new List<DeviceMemory>();
        public List<DeviceMemory> GetMemories()
        {
            return memories;
        }

        public bool FlashFPGA(byte[] firmware)
        {
            return SmartScopeFlashHelpers.FlashFpga(this.iface, firmware);
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
            WriteAdcReg(0x03, 0x38); //Configure timing
            WriteAdcReg(0x04, 0x02); //DATA 100 ohm termination
            WriteAdcReg(0x06, 0x10); //Offset binary encoding
            WriteAdcReg(0x00, 0x03); //Power on
            WriteAdcReg(0x01, 0x02); //DDR data on bus
            
        }

        public byte ReadAdcReg(uint address)
        {
            FpgaSettingsMemory[REG.SPI_ADDR].WriteImmediate((byte)(address + 128)); //for a read, MSB must be 1

            //next, trigger rising edge to initiate SPI comm
            FpgaSettingsMemory[REG.FLAGS].ClearBit((int)FLAG.SPI_CMD).WriteImmediate();
            FpgaSettingsMemory[REG.FLAGS].SetBit((int)FLAG.SPI_CMD).WriteImmediate();

            //finally read acquired value
            return FpgaRom[ROM.SPI_VAL].Read().GetByte();
        }

        public void WriteAdcReg(uint address, byte value)
        {
            FpgaSettingsMemory[REG.SPI_ADDR].WriteImmediate((byte)(address)); //for a read, MSB must be 1

            FpgaSettingsMemory[REG.SPI_VAL].WriteImmediate(value);
            //next, trigger rising edge to initiate SPI comm
            FpgaSettingsMemory[REG.FLAGS].ClearBit((int)FLAG.SPI_CMD).WriteImmediate();
            FpgaSettingsMemory[REG.FLAGS].SetBit((int)FLAG.SPI_CMD).WriteImmediate();
        }

        public IHardwareInterface HardwareInterface { get { return this.iface; } }
    }
}
