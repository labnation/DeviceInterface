using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.HardwareInterfaces;
using Common;

namespace ECore.DeviceMemories
{
    //this class defines which type of registers it contain, how much of them, and how to access them
    //actual filling of these registers must be defined by the specific HWImplementation, through the constructor of this class
    public class ScopeFpgaRom : ByteMemory
    {
        private ScopeUsbInterface hwInterface;

        internal ScopeFpgaRom(ScopeUsbInterface hwInterface)
        {
            this.hwInterface = hwInterface;
                        
            foreach (ROM reg in Enum.GetValues(typeof(ROM)))
                registers.Add((uint)reg, new ByteRegister(this, (uint)reg, reg.ToString()));

            int lastStrobe = (int)Enum.GetValues(typeof(STR)).Cast<STR>().Max();
            for(uint i = (uint)ROM.STROBES + 1; i < (uint)ROM.STROBES + lastStrobe / 8 + 1; i++)
                registers.Add(i, new ByteRegister(this, i, "STROBES " + (i - (int)ROM.STROBES)));
        }

        internal override void Read(uint address)
        {
            byte[] data = null;
            hwInterface.GetControllerRegister(ScopeController.FPGA_ROM, address, 1, out data);
            if (data == null)
                return;

            registers[address].Set(data[0]);
            registers[address].Dirty = false;
        }

        internal override void Write(uint address)
        {
            Logger.Error("Can't write to ROM");
            registers[address].Dirty = false;
        }

        public ByteRegister this[ROM r]
        {
            get { return this[(uint)r]; }
            set { this[(uint)r] = value; }
        }
    }
}
