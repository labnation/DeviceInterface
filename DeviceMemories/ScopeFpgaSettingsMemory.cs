using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.HardwareInterfaces;

namespace ECore.DeviceMemories
{
    //this class defines which type of registers it contain, how much of them, and how to access them
    //actual filling of these registers must be defined by the specific HWImplementation, through the constructor of this class
    public class ScopeFpgaSettingsMemory : ByteMemory
    {
        protected IScopeHardwareInterface hwInterface;

        public ScopeFpgaSettingsMemory(IScopeHardwareInterface hwInterface)
        {
            this.hwInterface = hwInterface;

            //instantiate registerList
            foreach(REG reg in Enum.GetValues(typeof(REG)))
            {
                registers.Add((int)reg, new ByteRegister((int)reg, Enum.GetName(typeof(REG), reg)));
            }

        }

        public override void Read(int address, int length)
        {
            byte[] data = null;
            hwInterface.GetControllerRegister(ScopeController.FPGA, address, length, out data);
            
            for (int j = 0; j < data.Length; j++)
                registers[address + j].Set(data[j]);
        }

        public override void Write(int address, int length)
        {
            byte[] data = new byte[length];
            //append the actual data
            for (int j = 0; j < length; j++)
                data[j] = GetRegister(address + j).GetByte();

            hwInterface.SetControllerRegister(ScopeController.FPGA, address, data);
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
    }
}
