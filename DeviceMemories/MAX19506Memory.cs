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
    public class MAX19506Memory : DeviceMemory
    {
        private ScopeFpgaSettingsMemory fpgaSettings;
        private ScopeStrobeMemory strobeMemory;
        private ScopeFpgaRom fpgaRom;

        //this method defines which type of registers are stored in the memory
        public MAX19506Memory(ScopeFpgaSettingsMemory fpgaMemory, ScopeStrobeMemory strobeMemory, ScopeFpgaRom fpgaRom)
        {
            this.fpgaSettings = fpgaMemory;
            this.strobeMemory = strobeMemory;
            this.fpgaRom = fpgaRom;

            //look up how many registers are required
            foreach (MAX19506 reg in Enum.GetValues(typeof(MAX19506)))
            {
                registers.Add((int)reg, new ByteRegister((int)reg, Enum.GetName(typeof(MAX19506), reg)));
            }

        }

        public override void ReadRange(int startAddress, int burstSize)
        {
            for (int i = 0; i < burstSize; i++)
            {
                //first send correct address to FPGA
                int address = startAddress + i;
                fpgaSettings.GetRegister(REG.SPI_ADDRESS).Set(address+128); //for a read, MSB must be 1
                fpgaSettings.WriteSingle(REG.SPI_ADDRESS);

                //next, trigger rising edge to initiate SPI comm
                strobeMemory.GetRegister(STR.INIT_SPI_TRANSFER).Set(0);
                strobeMemory.WriteSingle(STR.INIT_SPI_TRANSFER);
                strobeMemory.GetRegister(STR.INIT_SPI_TRANSFER).Set(1);
                strobeMemory.WriteSingle(STR.INIT_SPI_TRANSFER);

                //finally read acquired value
                fpgaRom.ReadSingle(ROM.SPI_RECEIVED_VALUE);
                int acquiredVal = fpgaRom.GetRegister(ROM.SPI_RECEIVED_VALUE).GetByte();
                Registers[address].Set(acquiredVal);
            }            
            
        }

        public override void WriteRange(int startAddress, int burstSize)
        {
            for (int i = 0; i < burstSize; i++)
            {
                //first send correct address to FPGA
                int address = startAddress + i; //for a write, MSB must be 0
                fpgaSettings.GetRegister(REG.SPI_ADDRESS).Set(address);
                fpgaSettings.WriteSingle(REG.SPI_ADDRESS);

                //next, send the write value to FPGA
                int valToWrite = GetRegister(address).GetByte();
                fpgaSettings.GetRegister(REG.SPI_WRITE_VALUE).Set(valToWrite);
                fpgaSettings.WriteSingle(REG.SPI_WRITE_VALUE);

                //finally, trigger rising edge
                strobeMemory.GetRegister(STR.INIT_SPI_TRANSFER).Set(0);
                strobeMemory.WriteSingle(STR.INIT_SPI_TRANSFER);
                strobeMemory.GetRegister(STR.INIT_SPI_TRANSFER).Set(1);
                strobeMemory.WriteSingle(STR.INIT_SPI_TRANSFER);
            }
        }
        public void WriteSingle(MAX19506 r)
        {
            this.WriteSingle((int)r);
        }
        public void ReadSingle(MAX19506 r)
        {
            this.ReadSingle((int)r);
        }
        public ByteRegister GetRegister(MAX19506 r)
        {
            return GetRegister((int)r);
        }
        public ByteRegister GetRegister(int a)
        {
            return (ByteRegister)registers[a];
        }
    }
}
