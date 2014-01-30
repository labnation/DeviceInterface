using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.DeviceMemories
{
    //this class defines which type of registers it contain, how much of them, and how to access them
    //actual filling of these registers must be defined by the specific HWImplementation, through the constructor of this class
    public class Scop3StrobeMemory: EDeviceMemory
    {
        private EDeviceMemory accessorMemory;

        //this method defines which type of registers are stored in the memory
        public Scop3StrobeMemory(EDevice eDevice, Dictionary<string, int> strobeNames, EDeviceMemory accessorMemory)
        {
            this.eDevice = eDevice;
            this.registerIndices = strobeNames;
            this.accessorMemory = accessorMemory;
                        
            //look up how many registers are required
            int largestIndex = 0;
            foreach (KeyValuePair<string, int> kvp in strobeNames)
                if (kvp.Value > largestIndex) 
                    largestIndex = kvp.Value;

            //instantiate registerList
            registers = new List<EDeviceMemoryRegister>();
            for (int i = 0; i < largestIndex+1; i++)
            {
                //find name of this register
                string regName = "<none>";
                foreach (KeyValuePair<string, int> kvp in strobeNames)
                    if (kvp.Value == i)
                        regName = kvp.Key;

                registers.Add(new MemoryRegisters.BoolRegister(regName, this));
            }

        }

        public override void ReadRange(int startAddress, int burstSize)
        {
            throw new NotImplementedException();
        }

        public override void WriteRange(int startAddress, int burstSize)
        {
            int bytesWritten = 16;
            byte[] writeBuffer = new byte[bytesWritten];

            for (int i = 0; i < burstSize; i++)
            {
                int strobeAddress = startAddress+i;
                int strobeValue = Registers[strobeAddress].InternalValue;

                //range check
                if (strobeValue<0)
                    Logger.AddEntry(this, LogMessageType.ECoreError, "Cannot upload " + strobeValue.ToString() + " into strobe " + strobeAddress.ToString());
                else if(strobeValue>1)
                    Logger.AddEntry(this, LogMessageType.ECoreError, "Cannot upload "+strobeValue.ToString()+" into strobe " + strobeAddress.ToString());
                else
                    Logger.AddEntry(this, LogMessageType.ECoreInfo, "Request to upload "+strobeValue.ToString()+" into strobe " + strobeAddress.ToString());

                //prepare data te be sent
                int valToSend = strobeAddress;
                valToSend = valToSend << 1;
                valToSend += strobeValue; //set strobe high or low

                //now put this in the correct FPGA register
                accessorMemory.RegisterByName("REG_STROBE_UPDATE").InternalValue = (byte)valToSend;

                //and send to FPGA
                accessorMemory.WriteSingle("REG_STROBE_UPDATE");

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
    }
}
