using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.HardwareInterfaces;

namespace ECore.DeviceMemories
{
    //this class defines which type of registers it contain, how much of them, and how to access them
    //actual filling of these registers must be defined by the specific HWImplementation, through the constructor of this class
    public class ScopeFpgaRom : ByteMemory
    {
        private EDeviceHWInterface hwInterface;
        //this method defines which type of registers are stored in the memory
        public ScopeFpgaRom(EDeviceHWInterface hwInterface)
        {
            this.hwInterface = hwInterface;
                        
            foreach (ROM reg in Enum.GetValues(typeof(ROM)))
            {
                registers.Add((int)reg, new ByteRegister((int)reg, Enum.GetName(typeof(ROM), reg)));
            }
            //Add ROM registers
            int lastStrobe = (int)Enum.GetValues(typeof(STR)).Cast<STR>().Max();
            for(int i = (int)ROM.STROBES + 1; i < (int)ROM.STROBES + lastStrobe / 8 + 1; i++)
            {
                registers.Add(i, new ByteRegister(i, "STROBES " + (i - (int)ROM.STROBES)));
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
            toSend2[i++] = (byte)(6); //this has to be i2c address immediately, not bitshifted or anything! address 6 = ROM registers inside FPGA
            toSend2[i++] = (byte)burstSize;

            //send over to HW, to perform read operation
            hwInterface.WriteControlBytes(toSend2);

            //now data is stored in EP3 of PIC, so read it
            byte[] readBuffer = hwInterface.ReadControlBytes(16); //EP3 always contains 16 bytes xxx should be linked to constant

            //strip away first 4 bytes (as these are not data) and store inside registers
            byte[] returnBuffer = new byte[burstSize];
            for (int j = 0; j < burstSize && (j+4) < readBuffer.Length; j++)
                registers[startAddress + j].Set(readBuffer[4 + j]);
        }

        public override void WriteRange(int startAddress, int burstSize)
        {
            Logger.AddEntry(this, LogMessageType.ECoreError, "Can't write to ROM");
        }

        public void WriteSingle(ROM r)
        {
            this.WriteSingle((int)r);
        }
        public void ReadSingle(ROM r)
        {
            this.ReadSingle((int)r);
        }
        public ByteRegister GetRegister(ROM r)
        {
            return GetRegister((int)r);
        }
    }
}
