using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.HardwareInterfaces;

namespace ECore.DeviceMemories
{
    abstract public class DeviceMemory
    {
        protected Dictionary<uint, MemoryRegister> registers = new Dictionary<uint, MemoryRegister>();

        abstract public void Write(uint address, uint length);
        abstract public void Read(uint address, uint length);
        
        virtual public void WriteSingle(uint address)
        {
            this.Write(address, 1);
        }

        virtual public void ReadSingle(uint address)
        {
            Read(address, 1);
        }

        public Dictionary<uint, MemoryRegister> Registers { get { return registers; } }
    }
}
