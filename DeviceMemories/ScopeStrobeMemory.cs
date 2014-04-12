using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.DeviceMemories
{
    //this class defines which type of registers it contain, how much of them, and how to access them
    //actual filling of these registers must be defined by the specific HWImplementation, through the constructor of this class
    public class ScopeStrobeMemory : DeviceMemory<ByteRegister>
    {
        private ScopeFpgaSettingsMemory writeMemory;
        private ScopeFpgaRom readMemory;

        //this method defines which type of registers are stored in the memory
        public ScopeStrobeMemory(ScopeFpgaSettingsMemory writeMemory, ScopeFpgaRom readMemory)
        {
            this.writeMemory = writeMemory;
            this.readMemory = readMemory;

            registers = new Dictionary<int, ByteRegister>();
            foreach (STR str in Enum.GetValues(typeof(STR)))
            {
                registers.Add((int)str, new ByteRegister((int)str, Enum.GetName(typeof(STR), str)));
            }

        }

        private int StrobeToRomAddress(int strobe)
        {
            return (int)ROM.STROBES + (int)Math.Floor((double)strobe / 8.0);
        }

        public override void ReadRange(int startAddress, int burstSize)
        {
            if (burstSize < 1) return;
            //Compute range of ROM registers to read from
            int romStartAddress = StrobeToRomAddress(startAddress);
            int romEndAddress = StrobeToRomAddress(startAddress + burstSize - 1);
            readMemory.ReadRange(romStartAddress, romEndAddress - romStartAddress + 1);

            for (int i = startAddress; i < startAddress + burstSize; i++)
            {
                int romAddress = StrobeToRomAddress(i);
                int offset = i % 8;
                registers[i].Set((byte)((readMemory.GetRegister(romAddress).GetByte() >> offset) & 0x01));
            }
        }

        public override void WriteRange(int startAddress, int burstSize)
        {
            int bytesWritten = 16;
            byte[] writeBuffer = new byte[bytesWritten];

            for (int i = 0; i < burstSize; i++)
            {
                int strobeAddress = startAddress+i;
                ByteRegister reg = Registers[strobeAddress];

                //range check
                if (reg.GetByte() < 0)
                    Logger.AddEntry(this, LogMessageType.ECoreError, "Cannot upload " + reg.Get() + " into strobe " + reg.Name + "(" + reg.Address + ")");
                else if (reg.GetByte() > 1)
                    Logger.AddEntry(this, LogMessageType.ECoreError, "Cannot upload " + reg.Get() + " into strobe " + reg.Name + "(" + reg.Address + ")");
                else
                    Logger.AddEntry(this, LogMessageType.ECoreInfo, "Request to upload " + reg.Get() + " into strobe " + reg.Name + "(" + reg.Address + ")");

                //prepare data te be sent
                int valToSend = strobeAddress;
                valToSend = valToSend << 1;
                valToSend += reg.GetByte(); //set strobe high or low

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
        public ByteRegister GetRegister(STR r)
        {
            return Registers[(int)r];
        }
    }
}
