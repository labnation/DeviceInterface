using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.DeviceMemories
{
    //this class defines which type of registers it contain, how much of them, and how to access them
    //actual filling of these registers must be defined by the specific HWImplementation, through the constructor of this class
    public class ScopeStrobeMemory : DeviceMemory<MemoryRegister<byte>>
    {
        private ScopeFpgaSettingsMemory accessorMemory;

        //this method defines which type of registers are stored in the memory
        public ScopeStrobeMemory(EDevice eDevice, ScopeFpgaSettingsMemory accessorMemory)
        {
            this.eDevice = eDevice;
            this.accessorMemory = accessorMemory;

            registers = new Dictionary<int, MemoryRegister<byte>>();
            foreach (STR str in Enum.GetValues(typeof(STR)))
            {
                registers.Add((int)str, new MemoryRegister<byte>((int)str, Enum.GetName(typeof(STR), str)));
            }

        }

        public override void ReadRange(int startAddress, int burstSize)
        {
            //throw new NotImplementedException();
        }

        public override void WriteRange(int startAddress, int burstSize)
        {
            int bytesWritten = 16;
            byte[] writeBuffer = new byte[bytesWritten];

            for (int i = 0; i < burstSize; i++)
            {
                int strobeAddress = startAddress+i;
                MemoryRegister<byte> reg = Registers[strobeAddress];

                //range check
                if (reg.InternalValue < 0)
                    Logger.AddEntry(this, LogMessageType.ECoreError, "Cannot upload " + reg.InternalValue + " into strobe " + reg.Name + "(" + reg.Address + ")");
                else if(reg.InternalValue > 1)
                    Logger.AddEntry(this, LogMessageType.ECoreError, "Cannot upload " + reg.InternalValue + " into strobe " + reg.Name + "(" + reg.Address + ")");
                else
                    Logger.AddEntry(this, LogMessageType.ECoreInfo, "Request to upload "+ reg.InternalValue +" into strobe " +  reg.Name + "(" + reg.Address + ")");

                //prepare data te be sent
                int valToSend = strobeAddress;
                valToSend = valToSend << 1;
                valToSend += reg.InternalValue; //set strobe high or low

                //now put this in the correct FPGA register
                accessorMemory.GetRegister(REG.STROBE_UPDATE).InternalValue = (byte)valToSend;

                //and send to FPGA
                accessorMemory.WriteSingle(REG.STROBE_UPDATE);

                /*
                writeBuffer[0] = 123; //preamble
                writeBuffer[1] = 10; //send to fpga
                writeBuffer[2] = 0; //FPGA register address of strobe control !!! TO BE CHANGED !!!
                writeBuffer[3] = 1; //only one register to write
                writeBuffer[4] = (byte)valToSend;

                eDevice.HWInterface.WriteControlBytes(writeBuffer);
                 * */
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
        public MemoryRegister<byte> GetRegister(STR r)
        {
            return Registers[(int)r];
        }
    }
}
