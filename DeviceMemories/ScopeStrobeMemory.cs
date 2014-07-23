using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.DeviceMemories
{
    //this class defines which type of registers it contain, how much of them, and how to access them
    //actual filling of these registers must be defined by the specific HWImplementation, through the constructor of this class
    public class ScopeStrobeMemory : DeviceMemory
    {
        private ScopeFpgaSettingsMemory writeMemory;
        private ScopeFpgaRom readMemory;

        public ScopeStrobeMemory(ScopeFpgaSettingsMemory writeMemory, ScopeFpgaRom readMemory)
        {
            this.writeMemory = writeMemory;
            this.readMemory = readMemory;

            foreach (STR str in Enum.GetValues(typeof(STR)))
                registers.Add((uint)str, new BoolRegister(this, (uint)str, str.ToString()));
        }

        private uint StrobeToRomAddress(uint strobe)
        {
            return (uint)ROM.STROBES + (uint)Math.Floor((double)strobe / 8.0);
        }

        internal override void Read(uint address)
        {
            //Compute range of ROM registers to read from
            uint romStartAddress = StrobeToRomAddress(address);
            readMemory.Read(romStartAddress);

            uint romAddress = StrobeToRomAddress(address);
            int offset = (int)(address % 8);
            registers[address].Set( ((readMemory[romAddress].GetByte() >> offset) & 0x01) == 0x01);
            registers[address].Dirty = false;
        }

        internal override void Write(uint address)
        {
            BoolRegister reg = this[address];

            //prepare data te be sent
            int valToSend = (int)address;
            valToSend = valToSend << 1;
            valToSend += reg.GetBool() ? 1: 0; //set strobe high or low

            //now put this in the correct FPGA register
            writeMemory[REG.STROBE_UPDATE].WriteImmediate(valToSend);
            registers[address].Dirty = false;
        }

        new public BoolRegister this[uint address]
        {
            get { return (BoolRegister)registers[address]; }
            set { ((BoolRegister)registers[address]).Set(value); }
        }

        public BoolRegister this[STR r]
        {
            get { return this[(uint)r]; }
        }
    }
}
