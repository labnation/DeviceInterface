using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.DeviceMemories
{
    public enum MAX19506
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
    //this class defines which type of registers it contain, how much of them, and how to access them
    //actual filling of these registers must be defined by the specific HWImplementation, through the constructor of this class
    public class MAX19506Memory : ByteMemory
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

        internal override void Read(uint address)
        {
            fpgaSettings[REG.SPI_ADDRESS].Write((byte)(address + 128)); //for a read, MSB must be 1

            //next, trigger rising edge to initiate SPI comm
            strobeMemory[STR.INIT_SPI_TRANSFER].Write(false);
            strobeMemory[STR.INIT_SPI_TRANSFER].Write(true);

            //finally read acquired value
            int acquiredVal = fpgaRom[ROM.SPI_RECEIVED_VALUE].Read().GetByte();
            registers[address].Set(acquiredVal);
        }

        internal override void Write(uint address)
        {
            //first send correct address to FPGA
            fpgaSettings[REG.SPI_ADDRESS].Write((int)(address));

            //next, send the write value to FPGA
            int valToWrite = this[address].GetByte();
            fpgaSettings[REG.SPI_WRITE_VALUE].Write(valToWrite);

            //finally, trigger rising edge
            strobeMemory[STR.INIT_SPI_TRANSFER].Write(false);
            strobeMemory[STR.INIT_SPI_TRANSFER].Write(true);
        }

        public ByteRegister this[MAX19506 r]
        {
            get { return this[(uint)r]; }
        }
    }
}
