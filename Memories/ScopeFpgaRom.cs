using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.Hardware;
using LabNation.Common;

namespace LabNation.DeviceInterface.Memories
{
	public class ScopeFpgaRom : ScopeFpgaI2cMemory
    {
        public ScopeFpgaRom(ISmartScopeInterface hwInterface, byte I2cAddress) : base(hwInterface, I2cAddress, 0, true)
        {
            foreach (ROM reg in Enum.GetValues(typeof(ROM)))
                Registers.Add((uint)reg, new ByteRegister(this, (uint)reg, reg.ToString()));

            int lastStrobe = (int)Enum.GetValues(typeof(STR)).Cast<STR>().Max();
            for(uint i = (uint)ROM.STROBES + 1; i < (uint)ROM.STROBES + lastStrobe / 8 + 1; i++)
                Registers.Add(i, new ByteRegister(this, i, "STROBES " + (i - (int)ROM.STROBES)));
        }

        public ByteRegister this[ROM r]
        {
            get { return this[(uint)r]; }
            set { this[(uint)r] = value; }
        }
    }
}
