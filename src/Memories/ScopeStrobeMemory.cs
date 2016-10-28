using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LabNation.DeviceInterface.Memories
{
	public class ScopeStrobeMemory : DeviceMemory
    {
        private ByteMemoryEnum<REG> writeMemory;
        private ScopeFpgaRom readMemory;

        protected Dictionary<uint, MemoryRegister> registers = new Dictionary<uint, MemoryRegister>();
        public override Dictionary<uint, MemoryRegister> Registers { get { return this.registers; } }

        public ScopeStrobeMemory(ByteMemoryEnum<REG> writeMemory, ScopeFpgaRom readMemory)
        {
            this.writeMemory = writeMemory;
            this.readMemory = readMemory;

            foreach (STR str in Enum.GetValues(typeof(STR)))
                Registers.Add((uint)str, new BoolRegister(this, (uint)str, str.ToString()));
        }

        private uint StrobeToRomAddress(uint strobe)
        {
            return (uint)ROM.STROBES + (uint)Math.Floor((double)strobe / 8.0);
        }

        public override void Read(uint address)
        {
            //Compute range of ROM registers to read from
            uint romStartAddress = StrobeToRomAddress(address);
            readMemory.Read(romStartAddress);

            uint romAddress = StrobeToRomAddress(address);
            int offset = (int)(address % 8);
            Registers[address].Set( ((readMemory[romAddress].GetByte() >> offset) & 0x01) == 0x01);
            Registers[address].Dirty = false;
        }

        public override void Write(uint address)
        {
            BoolRegister reg = this[address];

            //prepare data te be sent
            int valToSend = (int)address;
            valToSend = valToSend << 1;
            valToSend += reg.GetBool() ? 1: 0; //set strobe high or low

            //now put this in the correct FPGA register
            writeMemory[REG.STROBE_UPDATE].WriteImmediate(valToSend);
            Registers[address].Dirty = false;
        }

        public override void WriteRange(uint from, uint until)
        {
            WriteRangeSimple(from, until);
        }

        new public BoolRegister this[uint address]
        {
            get { return (BoolRegister)Registers[address]; }
            set { ((BoolRegister)Registers[address]).Set(value); }
        }

        public BoolRegister this[STR r]
        {
            get { return this[(uint)r]; }
        }
    }
}
