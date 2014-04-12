using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.DeviceMemories
{
    //this class defines which type of registers it contain, how much of them, and how to access them
    //actual filling of these registers must be defined by the specific HWImplementation, through the constructor of this class
    public class ScopeFpgaSettingsMemory : DeviceMemory
    {
        protected EDeviceHWInterface hwInterface;

        public ScopeFpgaSettingsMemory(EDeviceHWInterface hwInterface)
        {
            this.hwInterface = hwInterface;

            //instantiate registerList
            foreach(REG reg in Enum.GetValues(typeof(REG)))
            {
                registers.Add((int)reg, new ByteRegister((int)reg, Enum.GetName(typeof(REG), reg)));
            }

        }

        public override void ReadRange(int startAddress, int burstSize)
        {            
            ////////////////////////////////////////////////////////
            //first initiate i2c write to send FPGA I2C address and register to read from
            byte[] toSend1 = new byte[5];
            //prep header
            int i = 0;
            toSend1[i++] = 123; //message for FPGA
            toSend1[i++] = 10; //I2C send
            toSend1[i++] = (byte)(2); //just 2 bytes: the FPGA I2C address, and the register address inside the FPGA
            toSend1[i++] = (byte)(5 << 1); //first I2C byte: FPGA i2c address (5) + '0' as LSB, indicating write operation
            toSend1[i++] = (byte)startAddress; //second I2C byte: address of the register inside the FPGA

            //send this over, so FPGA register pointer is set to correct register
            hwInterface.WriteControlBytes(toSend1);

            ////////////////////////////////////////////////////////
            //now initiate I2C read operation
            byte[] toSend2 = new byte[4];

            //prep header
            i = 0;
            toSend2[i++] = 123; //message for FPGA
            toSend2[i++] = 11; //I2C read
            toSend2[i++] = (byte)(5); //this has to be i2c address immediately, not bitshifted or anything!
            toSend2[i++] = (byte)burstSize;

            //send over to HW, to perform read operation
            hwInterface.WriteControlBytes(toSend2);

            //now data is stored in EP3 of PIC, so read it
            byte[] readBuffer = hwInterface.ReadControlBytes(16); //EP3 always contains 16 bytes xxx should be linked to constant
            if (readBuffer.Length > 0)
            {
                //strip away first 4 bytes (as these are not data) and store inside registers
                byte[] returnBuffer = new byte[burstSize];
                for (int j = 0; j < burstSize; j++)
                    registers[startAddress + j].Set(readBuffer[4 + j]);
            }
        }

        public override void WriteRange(int startAddress, int burstSize)
        {
            byte[] toSend = new byte[burstSize + 5];

            //prep header
            int i = 0;
            toSend[i++] = 123; //message for FPGA
            toSend[i++] = 10; //I2C send
            toSend[i++] = (byte)(burstSize+2); //data and 2 more bytes: the FPGA I2C address, and the register address inside the FPGA
            toSend[i++] = (byte)(5 << 1); //first I2C byte: FPGA i2c address (5) + '0' as LSB, indicating write operation
            toSend[i++] = (byte)startAddress; //second I2C byte: address of the register inside the FPGA

            //append the actual data
            for (int j = 0; j < burstSize; j++)
                toSend[i++] = GetRegister(startAddress + j).GetByte();

            hwInterface.WriteControlBytes(toSend);
        }
        public void WriteSingle(REG r)
        {
            this.WriteSingle((int)r);
        }
        public void ReadSingle(REG r)
        {
            this.ReadSingle((int)r);
        }
        public ByteRegister GetRegister(REG r)
        {
            return GetRegister((int)r);
        }
        public ByteRegister GetRegister(int a)
        {
            return (ByteRegister)Registers[a];
        }
    }
}
