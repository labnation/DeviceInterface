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

        public override void Read(uint address, uint length)
        {
            if (length < 1) return;
            //Compute range of ROM registers to read from
            uint romStartAddress = StrobeToRomAddress(address);
            uint romEndAddress = StrobeToRomAddress(address + length - 1);
            readMemory.Read(romStartAddress, romEndAddress - romStartAddress + 1);

            for (uint i = address; i < address + length; i++)
            {
                uint romAddress = StrobeToRomAddress(i);
                int offset = (int)(i % 8);
                registers[i].Set( ((readMemory.GetRegister(romAddress).GetByte() >> offset) & 0x01) == 0x01);
            }
        }

        public override void Write(uint address, uint length)
        {
            for (uint i = 0; i < length; i++)
            {
                uint strobeAddress = address+i;
                BoolRegister reg = GetRegister(strobeAddress);

                //prepare data te be sent
                int valToSend = (int)strobeAddress;
                valToSend = valToSend << 1;
                valToSend += reg.GetBool() ? 1: 0; //set strobe high or low

                //now put this in the correct FPGA register
                writeMemory.GetRegister(REG.STROBE_UPDATE).Set(valToSend);

                //and send to FPGA
                writeMemory.WriteSingle(REG.STROBE_UPDATE);
            }            
        }

        public void WriteSingle(STR r)
        {
            this.WriteSingle((uint)r);
        }
        public void ReadSingle(STR r)
        {
            this.ReadSingle((uint)r);
        }
        public BoolRegister GetRegister(STR r)
        {
            return GetRegister((uint)r);
        }
        public BoolRegister GetRegister(uint a)
        {
            return (BoolRegister)Registers[a];
        }
        public BoolRegister this[STR r]
        {
            get { return GetRegister((uint)r); }
        }
    }
}
