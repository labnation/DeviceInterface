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
        private ScopeUsbInterface hwInterface;

        internal ScopePicRegisterMemory(ScopeUsbInterface hwInterface)
        {
            this.hwInterface = hwInterface;
                        
            foreach (PIC reg in Enum.GetValues(typeof(PIC)))
                registers.Add((uint)reg, new ByteRegister(this, (uint)reg, reg.ToString()));
        }

        internal override void Read(uint address)
        {            
            byte[] data;
            hwInterface.GetControllerRegister(ScopeController.PIC, address, 1, out data);
            
            for (uint i = 0; i < data.Length; i++)
                registers[address + i].Set(data[i]);
        }

        internal override void Write(uint address)
        {
            byte[] data = new byte[] { this[address].GetByte() };
            hwInterface.SetControllerRegister(ScopeController.PIC, address, data);
        }
        public ByteRegister this[PIC r]
        {
            get { return this[(uint)r]; }
        }
    }
}
