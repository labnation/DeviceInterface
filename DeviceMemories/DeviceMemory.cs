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
#if INTERNAL
        public Dictionary<uint, MemoryRegister> Registers { get { return registers; } }
#endif


        abstract internal void Write(uint address);
        abstract internal void Read(uint address);
        
        public MemoryRegister this[uint address]
        {
            get { return registers[address]; }
        }
    }
}
