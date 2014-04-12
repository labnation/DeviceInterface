using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.HardwareInterfaces;

namespace ECore.DeviceMemories
{
    //abstract class, representing a physical memory on the PCB.
    //examples: ROM, FPGA, FX2, ADC register banks, ...
    abstract public class DeviceMemory
    {
        protected Dictionary<int, MemoryRegister> registers = new Dictionary<int, MemoryRegister>();

        abstract public void Write(int address, int length);
        abstract public void Read(int address, int length);
        
        virtual public void WriteSingle(int address)
        {
            this.Write(address, 1);
        }

        virtual public void ReadSingle(int address)
        {
            Read(address, 1);
        }

        //public int MaxValue { get { return registers[0].MaxValue; } }
        public int NumberOfRegisters { get { return registers.Count; } }
        public Dictionary<int, MemoryRegister> Registers { get { return registers; } }
    }
}
