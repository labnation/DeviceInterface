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
        internal ScopeUsbInterface hwInterface;

        internal ScopeFpgaSettingsMemory(ScopeUsbInterface hwInterface)
        {
            this.hwInterface = hwInterface;

            foreach(REG reg in Enum.GetValues(typeof(REG)))
                registers.Add((uint)reg, new ByteRegister(this, (uint)reg, reg.ToString()));
        }

        internal override void Read(uint address)
        {
            byte[] data = null;
            hwInterface.GetControllerRegister(ScopeController.FPGA, address, 1, out data);
            
            registers[address].Set(data[0]);
            registers[address].Dirty = false;
        }

        internal override void Write(uint address)
        {
            byte[] data = new byte[] { this[address].GetByte() };
            hwInterface.SetControllerRegister(ScopeController.FPGA, address, data);
            registers[address].Dirty = false;
        }

        public ByteRegister this[REG r]
        {
            get { return this[(uint)r]; }
            set { this[(uint)r] = value; }
        }
    }
}
