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

        public override void Read(uint address, uint length)
        {
            byte[] data = null;
            hwInterface.GetControllerRegister(ScopeController.FPGA_ROM, address, length, out data);
            if (data == null)
                return;

            for (uint j = 0; j < data.Length; j++)
                registers[address + j].Set(data[j]);
        }

        public override void Write(uint address, uint length)
        {
            Logger.Error("Can't write to ROM");
        }

        public void WriteSingle(ROM r)
        {
            this.WriteSingle((uint)r);
        }
        public void ReadSingle(ROM r)
        {
            this.ReadSingle((uint)r);
        }
        public ByteRegister GetRegister(ROM r)
        {
            return GetRegister((uint)r);
        }
        public ByteRegister this[ROM r]
        {
            get { return GetRegister((uint)r); }
        }
    }
}
