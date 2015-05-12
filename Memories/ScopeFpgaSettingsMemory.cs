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
    class ScopeFpgaSettingsMemory : ByteMemory
    {
        public ISmartScopeUsbInterface hwInterface;

        public ScopeFpgaSettingsMemory(ISmartScopeUsbInterface hwInterface)
        {
            this.hwInterface = hwInterface;

            foreach(REG reg in Enum.GetValues(typeof(REG)))
                registers.Add((uint)reg, new ByteRegister(this, (uint)reg, reg.ToString()));
        }

        public override void Read(uint address)
        {
            byte[] data = null;
            hwInterface.GetControllerRegister(ScopeController.FPGA, address, 1, out data);
            registers[address].Set(data[0]);
            registers[address].Dirty = false;
        }

        public override void Write(uint address)
        {
            byte[] data = new byte[] { this[address].GetByte() };
            hwInterface.SetControllerRegister(ScopeController.FPGA, address, data);
            registers[address].Dirty = false;
        }

        public ByteRegister this[REG r]
        {
            get { return this[(uint)r]; }
            set { this[(uint)r] = value; }
        }
    }
}
