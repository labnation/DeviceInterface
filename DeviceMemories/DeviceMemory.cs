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

        /// <summary>
        /// Writes away all registers with the Dirty flag set
        /// </summary>
        internal void Commit()
        {
            foreach (MemoryRegister m in registers.Values.Where(x => x.Dirty))
            {
                m.WriteImmediate();
            }
        }
        
        public MemoryRegister this[uint address]
        {
            get { return registers[address]; }
        }
    }
}
