using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.Hardware;

namespace LabNation.DeviceInterface.Memories
{
	public class ByteMemoryEnum<T> : ByteMemory
    {
        private ByteMemory memory = null;
        public override Dictionary<uint, MemoryRegister> Registers { get { return memory.Registers; } }

        public ByteMemoryEnum(ByteMemory m)
        {
            if (!typeof(T).IsEnum)
            {
                throw new ArgumentException("T must be an enumerated type");
            }
            this.memory = m;
            foreach(T reg in Enum.GetValues(typeof(T)))
            {
                uint addr = EnumToVal(reg);
                if (Registers.ContainsKey(addr))
                    Registers[addr].Name = reg.ToString();
                else
                    Registers[addr] = new ByteRegister(this, addr, reg.ToString());
            }
        }

        public ByteRegister this[T r]
        {
            get { return memory[EnumToVal(r)]; }
        }

        public override void Read(uint a) { this.memory.Read(a); }
        public override void Write(uint a) { this.memory.Write(a); }
        public override void WriteRange(uint from, uint until)
        {
            this.memory.WriteRange(from, until);
        }

        private static uint EnumToVal(T e)
        {
            if (e is uint)
                return Convert.ToUInt32(e);
            Enum test = Enum.Parse(typeof(T), e.ToString()) as Enum;
            return Convert.ToUInt32(test);
        }
    }
}
