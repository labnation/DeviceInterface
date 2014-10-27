using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.HardwareInterfaces;

namespace ECore.DeviceMemories
{
#if DEBUG
        public
#else
    internal
#endif
    abstract class ByteMemory : DeviceMemory
    {
        public new ByteRegister this[uint address]
        {
            get { return (ByteRegister)registers[address]; }
            set { ((ByteRegister)registers[address]).Set(value); }
        }
    }
}
