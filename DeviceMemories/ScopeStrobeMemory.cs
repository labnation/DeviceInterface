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
                registers.Add((int)str, new BoolRegister((int)str, Enum.GetName(typeof(STR), str)));
        }

        private int StrobeToRomAddress(int strobe)
        {
            return (int)ROM.STROBES + (int)Math.Floor((double)strobe / 8.0);
        }

        public override void Read(int address, int length)
        {
            if (length < 1) return;
            //Compute range of ROM registers to read from
            int romStartAddress = StrobeToRomAddress(address);
            int romEndAddress = StrobeToRomAddress(address + length - 1);
            readMemory.Read(romStartAddress, romEndAddress - romStartAddress + 1);

            for (int i = address; i < address + length; i++)
            {
                int romAddress = StrobeToRomAddress(i);
                int offset = i % 8;
                registers[i].Set( ((readMemory.GetRegister(romAddress).GetByte() >> offset) & 0x01) == 0x01);
            }
        }

        public override void Write(int address, int length)
        {
            for (int i = 0; i < length; i++)
            {
                int strobeAddress = address+i;
                BoolRegister reg = GetRegister(strobeAddress);

                //prepare data te be sent
                int valToSend = strobeAddress;
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
            this.WriteSingle((int)r);
        }
        public void ReadSingle(STR r)
        {
            this.ReadSingle((int)r);
        }
        public BoolRegister GetRegister(STR r)
        {
            return GetRegister((int)r);
        }
        public BoolRegister GetRegister(int a)
        {
            return (BoolRegister)Registers[a];
        }
    }
}
