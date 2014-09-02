using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.HardwareInterfaces;
using Common;

namespace ECore.DeviceMemories
{
#if INTERNAL
        public
#else
        internal
#endif
    abstract class DeviceMemory
    {
        protected Dictionary<uint, MemoryRegister> registers = new Dictionary<uint, MemoryRegister>();
        public Dictionary<uint, MemoryRegister> Registers { get { return registers; } }

        abstract public void Write(uint address);
        abstract public void Read(uint address);

        /// <summary>
        /// Writes away all registers with the Dirty flag set
        /// </summary>
        public int Commit()
        {
            int flushCount = registers.Values.Where(x => x.Dirty).Count();
            if (flushCount == 0)
                return flushCount;

            Logger.Debug(String.Format("About to flush {0} / {1} registers in {2}", flushCount, registers.Count, this.GetType()));

            foreach (MemoryRegister m in registers.Values.Where(x => x.Dirty))
            {
                m.WriteImmediate();
            }
            return flushCount;
        }
        
        public MemoryRegister this[uint address]
        {
            get { return registers[address]; }
        }
    }
}
