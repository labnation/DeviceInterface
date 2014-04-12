using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.HardwareInterfaces;

namespace ECore.DeviceMemories
{
    abstract public class ByteMemory : DeviceMemory
    {
        public ByteRegister GetRegister(int address)
        {
            return (ByteRegister)registers[address];
        }
    }
}
