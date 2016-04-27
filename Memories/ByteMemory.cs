using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.Hardware;
using LabNation.DeviceInterface.Devices;

namespace LabNation.DeviceInterface.Memories
{
#if DEBUG
        public
#else
    internal
#endif
    abstract class ByteMemory : DeviceMemory
    {
        public ByteMemory(IDevice dev) : base(dev) { } 
        public new ByteRegister this[uint address]
        {
            get { return (ByteRegister)registers[address]; }
            set { ((ByteRegister)registers[address]).Set(value); }
        }
    }
}
