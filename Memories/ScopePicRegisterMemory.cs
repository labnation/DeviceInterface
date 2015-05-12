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
    enum PIC
    {
        FORCE_STREAMING = 0,
    }

#if DEBUG
    public
#else
    internal
#endif
    class ScopePicRegisterMemory : ByteMemory
    {
        private ISmartScopeUsbInterface hwInterface;

        public ScopePicRegisterMemory(ISmartScopeUsbInterface hwInterface)
        {
            this.hwInterface = hwInterface;
                        
            foreach (PIC reg in Enum.GetValues(typeof(PIC)))
                registers.Add((uint)reg, new ByteRegister(this, (uint)reg, reg.ToString()));
        }

        public override void Read(uint address)
        {            
            byte[] data;
            hwInterface.GetControllerRegister(ScopeController.PIC, address, 1, out data);

            for (uint i = 0; i < data.Length; i++)
            {
                registers[address + i].Set(data[i]);
                registers[address + i].Dirty = false;
            }
        }

        public override void Write(uint address)
        {
            byte[] data = new byte[] { this[address].GetByte() };
            hwInterface.SetControllerRegister(ScopeController.PIC, address, data);
            registers[address].Dirty = false;
        }
        public ByteRegister this[PIC r]
        {
            get { return this[(uint)r]; }
            set { this[(uint)r] = value; }
        }
    }
}
