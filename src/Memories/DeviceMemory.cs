using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.Hardware;
using LabNation.Common;

namespace LabNation.DeviceInterface.Memories
{
	public abstract class DeviceMemory
    {
        abstract public Dictionary<uint, MemoryRegister> Registers { get; }

        abstract public void Write(uint address);
        abstract public void Read(uint address);
        abstract public void WriteRange(uint from, uint until);

        internal void WriteRangeSimple(uint from, uint until)
        {
            for (uint i = from; i <= until; i++)
                Write(i);
        }
        /// <summary>
        /// Writes away all registers with the Dirty flag set
        /// </summary>
        public List<MemoryRegister> Commit()
        {
            List<MemoryRegister> dirtyRegisters = Registers.Values.Where(x => x.Dirty).ToList();
            if (dirtyRegisters.Count == 0)
                return dirtyRegisters;

            uint[] regs = dirtyRegisters.Select(x => x.Address).ToArray();
                
            Array.Sort(regs);

            uint from = regs[0];
            uint until = regs[0];
            for(int i = 1; i < regs.Length; i++)
            {
                if (regs[i] != until + 1) {
                    this.WriteRange(from, until);
                    from = regs[i];
                }
                until = regs[i];
            }
            this.WriteRange(from, until);
            return dirtyRegisters;
        }
        
        public MemoryRegister this[uint address]
        {
            get { return Registers[address]; }
        }
    }
}
