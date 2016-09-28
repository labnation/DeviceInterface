using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.Hardware;

namespace LabNation.DeviceInterface.Memories
{
	public enum PIC
    {
        FORCE_STREAMING = 0,
    }

	public class ScopePicRegisterMemory : ByteMemory
    {
        private ISmartScopeInterface hwInterface;

        protected Dictionary<uint, MemoryRegister> registers = new Dictionary<uint, MemoryRegister>();
        public override Dictionary<uint, MemoryRegister> Registers { get { return this.registers; } }

        public ScopePicRegisterMemory(ISmartScopeInterface hwInterface)
        {
            this.hwInterface = hwInterface;

            foreach (PIC reg in Enum.GetValues(typeof(PIC)))
                Registers.Add((uint)reg, new ByteRegister(this, (uint)reg, reg.ToString()));
        }

        public override void Read(uint address)
        {            
            byte[] data;
            hwInterface.GetControllerRegister(ScopeController.PIC, address, 1, out data);

            for (uint i = 0; i < data.Length; i++)
            {
                Registers[address + i].Set(data[i]);
                Registers[address + i].Dirty = false;
            }
        }

        public override void Write(uint address)
        {
            byte[] data = new byte[] { this[address].GetByte() };
            hwInterface.SetControllerRegister(ScopeController.PIC, address, data);
            Registers[address].Dirty = false;
        }
        public ByteRegister this[PIC r]
        {
            get { return this[(uint)r]; }
            set { this[(uint)r] = value; }
        }
    }
}
