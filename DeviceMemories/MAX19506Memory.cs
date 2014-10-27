using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.DeviceMemories
{
#if DEBUG
    public
#else
    internal
#endif
    enum MAX19506
    {
        POWER_MANAGEMENT = 0,
        OUTPUT_FORMAT = 1,
        OUTPUT_PWR_MNGMNT = 2,
        DATA_CLK_TIMING = 3,
        CHA_TERMINATION = 4,
        CHB_TERMINATION = 5,
        FORMAT_PATTERN = 6,
        COMMON_MODE = 8,
        SOFT_RESET = 10,
    }
#if DEBUG
    public
#else
    internal
#endif
    class MAX19506Memory : ByteMemory
    {
        private ScopeFpgaSettingsMemory fpgaSettings;
        private ScopeStrobeMemory strobeMemory;
        private ScopeFpgaRom fpgaRom;

        internal MAX19506Memory(ScopeFpgaSettingsMemory fpgaMemory, ScopeStrobeMemory strobeMemory, ScopeFpgaRom fpgaRom)
        {
            this.fpgaSettings = fpgaMemory;
            this.strobeMemory = strobeMemory;
            this.fpgaRom = fpgaRom;

            foreach (MAX19506 reg in Enum.GetValues(typeof(MAX19506)))
                registers.Add((uint)reg, new ByteRegister(this, (uint)reg, reg.ToString()));
        }

        public override void Read(uint address)
        {
            fpgaSettings[REG.SPI_ADDRESS].WriteImmediate((byte)(address + 128)); //for a read, MSB must be 1

            //next, trigger rising edge to initiate SPI comm
            strobeMemory[STR.INIT_SPI_TRANSFER].WriteImmediate(false);
            strobeMemory[STR.INIT_SPI_TRANSFER].WriteImmediate(true);

            //finally read acquired value
            int acquiredVal = fpgaRom[ROM.SPI_RECEIVED_VALUE].Read().GetByte();
            registers[address].Set(acquiredVal);
            registers[address].Dirty = false;
        }

        public override void Write(uint address)
        {
            //first send correct address to FPGA
            fpgaSettings[REG.SPI_ADDRESS].WriteImmediate((int)(address));

            //next, send the write value to FPGA
            int valToWrite = this[address].GetByte();
            fpgaSettings[REG.SPI_WRITE_VALUE].WriteImmediate(valToWrite);

            //finally, trigger rising edge
            strobeMemory[STR.INIT_SPI_TRANSFER].WriteImmediate(false);
            strobeMemory[STR.INIT_SPI_TRANSFER].WriteImmediate(true);
            registers[address].Dirty = false;
        }

        public ByteRegister this[MAX19506 r]
        {
            get { return this[(uint)r]; }
            set { this[(uint)r] = value; }
        }
    }
}
