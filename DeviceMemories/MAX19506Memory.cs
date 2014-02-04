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
    public class MAX19506Memory: EDeviceMemory
    {
        private Scop3FpgaRegisterMemory fpgaMemory;
        private Scop3StrobeMemory strobeMemory;
        private Scop3FpgaRomMemory romMemory;

        //this method defines which type of registers are stored in the memory
        public MAX19506Memory(EDevice eDevice,
            Scop3FpgaRegisterMemory fpgaMemory, Scop3StrobeMemory strobeMemory, Scop3FpgaRomMemory romMemory)
        {
            this.eDevice = eDevice;
            this.fpgaMemory = fpgaMemory;
            this.strobeMemory = strobeMemory;
            this.romMemory = romMemory;

            //look up how many registers are required
            registers = new List<EDeviceMemoryRegister>();
            foreach (MAX19506 reg in Enum.GetValues(typeof(MAX19506)))
            {
                registers.Add(new MemoryRegisters.ByteRegister((int)reg, Enum.GetName(typeof(MAX19506), reg), this));
            }

        }

        public override void ReadRange(int startAddress, int burstSize)
        {
            for (int i = 0; i < burstSize; i++)
            {
                //first send correct address to FPGA
                int address = startAddress + i;
                fpgaMemory.GetRegister(REG.SPI_ADDRESS).InternalValue = (byte)(address+128); //for a read, MSB must be 1
                fpgaMemory.WriteSingle(REG.SPI_ADDRESS);

                //next, trigger rising edge to initiate SPI comm
                strobeMemory.GetRegister(STR.INIT_SPI_TRANSFER).InternalValue = 0;
                strobeMemory.WriteSingle(STR.INIT_SPI_TRANSFER);
                strobeMemory.GetRegister(STR.INIT_SPI_TRANSFER).InternalValue = 1;
                strobeMemory.WriteSingle(STR.INIT_SPI_TRANSFER);

                //finally read acquired value
                romMemory.ReadSingle(ROM.SPI_RECEIVED_VALUE);
                int acquiredVal = romMemory.GetRegister(ROM.SPI_RECEIVED_VALUE).InternalValue;
                Registers[address].InternalValue = (byte)acquiredVal;
            }            
            
        }

        public override void WriteRange(int startAddress, int burstSize)
        {
            for (int i = 0; i < burstSize; i++)
            {
                //first send correct address to FPGA
                int address = startAddress + i; //for a write, MSB must be 0
                fpgaMemory.GetRegister(REG.SPI_ADDRESS).InternalValue = (byte)address;
                fpgaMemory.WriteSingle(REG.SPI_ADDRESS);

                //next, send the write value to FPGA
                int valToWrite = Registers[address].InternalValue;
                fpgaMemory.GetRegister(REG.SPI_WRITE_VALUE).InternalValue = (byte)valToWrite;
                fpgaMemory.WriteSingle(REG.SPI_WRITE_VALUE);

                //finally, trigger rising edge
                strobeMemory.GetRegister(STR.INIT_SPI_TRANSFER).InternalValue = 0;
                strobeMemory.WriteSingle(STR.INIT_SPI_TRANSFER);
                strobeMemory.GetRegister(STR.INIT_SPI_TRANSFER).InternalValue = 1;
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
        public EDeviceMemoryRegister GetRegister(MAX19506 r)
        {
            return Registers[(int)r];
        }
    }
}
