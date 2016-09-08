using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.Hardware;

namespace LabNation.DeviceInterface.Memories
{
#if DEBUG
    public
#else
    internal
#endif
    class ScopeFpgaSettingsMemory : ScopeFpgaI2cMemory
    {
        public ScopeFpgaSettingsMemory(ISmartScopeInterface hwInterface, byte I2cAddress) : base(hwInterface, I2cAddress)
        {
            foreach(REG reg in Enum.GetValues(typeof(REG)))
                registers[(uint)reg] = new ByteRegister(this, (uint)reg, reg.ToString());
        }

        public ByteRegister this[REG r]
        {
            get { return this[(uint)r]; }
            set { this[(uint)r] = value; }
        }
    }
}
