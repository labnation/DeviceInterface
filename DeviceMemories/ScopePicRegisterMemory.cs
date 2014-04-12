using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.DeviceMemories
{
    //this class defines which type of registers it contain, how much of them, and how to access them
    //actual filling of these registers must be defined by the specific HWImplementation, through the constructor of this class
    public enum PIC
    {
        FORCE_STREAMING = 0,
    }

    public class ScopePicRegisterMemory : DeviceMemory<ByteRegister>
    {       
        //this method defines which type of registers are stored in the memory
        public ScopePicRegisterMemory(EDeviceHWInterface hwInterface)
        {
            this.hwInterface = hwInterface;
                        
            //instantiate registerList
            registers = new Dictionary<int, ByteRegister>();
            foreach (PIC reg in Enum.GetValues(typeof(PIC)))
            {
                registers.Add((int)reg, new ByteRegister((int)reg, Enum.GetName(typeof(PIC), reg)));
            }

        }

        public override void ReadRange(int startAddress, int burstSize)
        {            
            ////////////////////////////////////////////////////////
            //first initiate i2c write to send FPGA I2C address and register to read from
            byte[] toSend1 = new byte[4];
            //prep header
            int i = 0;
            toSend1[i++] = 123; //message for PIC
            toSend1[i++] = 3; //HOST_COMMAND_GET_PIC_REGISTER
            toSend1[i++] = (byte)(startAddress); 
            toSend1[i++] = (byte)(burstSize); 

            //send this over, so FPGA register pointer is set to correct register
            hwInterface.WriteControlBytes(toSend1);

            //now data is stored in EP3 of PIC, so read it
            byte[] readBuffer = hwInterface.ReadControlBytes(16); //EP3 always contains 16 bytes xxx should be linked to constant

            //strip away first 4 bytes (as these are not data) and store inside registers
            byte[] returnBuffer = new byte[burstSize];
            for (int j = 0; j < burstSize; j++)
                registers[startAddress + j].Set(readBuffer[4 + j]);
        }

        public override void WriteRange(int startAddress, int burstSize)
        {
            byte[] toSend = new byte[burstSize + 4];

            //prep header
            int i = 0;
            toSend[i++] = 123; //message for FPGA
            toSend[i++] = 2; //HOST_COMMAND_SET_PIC_REGISTER
            toSend[i++] = (byte)(startAddress); 
            toSend[i++] = (byte)(burstSize); //first I2C byte: FPGA i2c address (5) + '0' as LSB, indicating write operation

            //append the actual data
            for (int j = 0; j < burstSize; j++)
                toSend[i++] = this.registers[startAddress + j].GetByte();

            hwInterface.WriteControlBytes(toSend);
        }
        public void WriteSingle(PIC r)
        {
            this.WriteSingle((int)r);
        }
        public void ReadSingle(PIC r)
        {
            this.ReadSingle((int)r);
        }
        public ByteRegister GetRegister(PIC r)
        {
            return Registers[(int)r];
        }

    }
}
