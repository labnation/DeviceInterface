using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.Hardware;

namespace LabNation.DeviceInterface.Memories
{
    public abstract class  ByteMemory : DeviceMemory
    {
        public new ByteRegister this[uint address]
        {
            get { return (ByteRegister)Registers[address]; }
            set { ((ByteRegister)Registers[address]).Set(value); }
        }
    }
}
