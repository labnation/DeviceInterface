using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.Hardware;
using LabNation.Common;

namespace LabNation.DeviceInterface.Memories
{
#if DEBUG
        public
#else
        internal
#endif
    abstract class DeviceMemory
    {
        abstract public Dictionary<uint, MemoryRegister> Registers { get; }

        abstract public void Write(uint address);
        abstract public void Read(uint address);

        /// <summary>
        /// Writes away all registers with the Dirty flag set
        /// </summary>
        public List<MemoryRegister> Commit()
        {
            List<MemoryRegister> dirtyRegisters = Registers.Values.Where(x => x.Dirty).ToList();
            if (dirtyRegisters.Count == 0)
                return dirtyRegisters;

            //Logger.Debug(String.Format("About to flush {0} / {1} registers in {2}", flushCount, registers.Count, this.GetType()));

            foreach (MemoryRegister m in dirtyRegisters)
            {
                m.WriteImmediate();
            }
            return dirtyRegisters;
        }
        
        public MemoryRegister this[uint address]
        {
            get { return Registers[address]; }
        }
    }
}
