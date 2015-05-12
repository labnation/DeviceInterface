using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.Hardware;
using LabNation.Common;

namespace LabNation.DeviceInterface.Memories
{
#if DEBUG
    public
#else
    internal
#endif
    class ScopeFpgaRom : ByteMemory
    {
        private ISmartScopeUsbInterface hwInterface;

        public ScopeFpgaRom(ISmartScopeUsbInterface hwInterface)
        {
            this.hwInterface = hwInterface;
                        
            foreach (ROM reg in Enum.GetValues(typeof(ROM)))
                registers.Add((uint)reg, new ByteRegister(this, (uint)reg, reg.ToString()));

            int lastStrobe = (int)Enum.GetValues(typeof(STR)).Cast<STR>().Max();
            for(uint i = (uint)ROM.STROBES + 1; i < (uint)ROM.STROBES + lastStrobe / 8 + 1; i++)
                registers.Add(i, new ByteRegister(this, i, "STROBES " + (i - (int)ROM.STROBES)));
        }

        public override void Read(uint address)
        {
            byte[] data = null;
            hwInterface.GetControllerRegister(ScopeController.FPGA_ROM, address, 1, out data);
            if (data == null)
                return;

            registers[address].Set(data[0]);
            registers[address].Dirty = false;
        }

        public override void Write(uint address) 
        {
            Read(address);
            Logger.Warn("Attempting to write to ROM");
        }

        public ByteRegister this[ROM r]
        {
            get { return this[(uint)r]; }
            set { this[(uint)r] = value; }
        }
    }
}
