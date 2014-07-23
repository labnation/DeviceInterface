using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.HardwareInterfaces;

namespace ECore.DeviceMemories
{
    abstract public class ByteMemory : DeviceMemory
    {
        new public ByteRegister this[uint address]
        {
            get { return (ByteRegister)registers[address]; }
            set { ((ByteRegister)registers[address]).Set(value); }
        }
    }
}
