using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.HardwareInterfaces;

namespace ECore.DeviceMemories
{
    //this class defines which type of registers it contain, how much of them, and how to access them
    //actual filling of these registers must be defined by the specific HWImplementation, through the constructor of this class
    public enum PIC
    {
        FORCE_STREAMING = 0,
    }

    public class ScopePicRegisterMemory : ByteMemory
    {
        protected IScopeHardwareInterface hwInterface;

        public ScopePicRegisterMemory(IScopeHardwareInterface hwInterface)
        {
            this.hwInterface = hwInterface;
                        
            foreach (PIC reg in Enum.GetValues(typeof(PIC)))
                registers.Add((uint)reg, new ByteRegister(this, (uint)reg, reg.ToString()));
        }

        public override void Read(uint address, uint length)
        {            
            byte[] data;
            hwInterface.GetControllerRegister(ScopeController.PIC, address, length, out data);
            
            for (uint i = 0; i < data.Length; i++)
                registers[address + i].Set(data[i]);
        }

        public override void Write(uint address, uint length)
        {
            byte[] data = new byte[length];
            for (uint i = 0; i < length; i++)
                data[i] = GetRegister(address + i).GetByte();
            
            hwInterface.SetControllerRegister(ScopeController.PIC, address, data);
        }
        public void WriteSingle(PIC r)
        {
            this.WriteSingle((uint)r);
        }
        public void ReadSingle(PIC r)
        {
            this.ReadSingle((uint)r);
        }
        public ByteRegister GetRegister(PIC r)
        {
            return GetRegister((uint)r);
        }
        public ByteRegister this[PIC r]
        {
            get { return GetRegister((uint)r); }
        }
    }
}
