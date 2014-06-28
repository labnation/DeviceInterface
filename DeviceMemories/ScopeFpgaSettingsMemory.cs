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

            foreach(REG reg in Enum.GetValues(typeof(REG)))
                registers.Add((uint)reg, new ByteRegister(this, (uint)reg, reg.ToString()));
        }

        public override void Read(uint address, uint length)
        {
            byte[] data = null;
            hwInterface.GetControllerRegister(ScopeController.FPGA, address, length, out data);
            
            for (uint j = 0; j < data.Length; j++)
                registers[address + j].Set(data[j]);
        }

        public override void Write(uint address, uint length)
        {
            byte[] data = new byte[length];
            for (uint j = 0; j < length; j++)
                data[j] = GetRegister(address + j).GetByte();

            hwInterface.SetControllerRegister(ScopeController.FPGA, address, data);
        }
        public void WriteSingle(REG r)
        {
            this.WriteSingle((uint)r);
        }
        public void ReadSingle(REG r)
        {
            this.ReadSingle((uint)r);
        }
        public ByteRegister GetRegister(REG r)
        {
            return GetRegister((uint)r);
        }
    }
}
