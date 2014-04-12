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
                registers.Add((int)reg, new ByteRegister((int)reg, Enum.GetName(typeof(PIC), reg)));
        }

        public override void Read(int address, int length)
        {            
            byte[] data;
            hwInterface.GetControllerRegister(ScopeController.PIC, address, length, out data);
            
            for (int i = 0; i < data.Length; i++)
                registers[address + i].Set(data[i]);
        }

        public override void Write(int address, int length)
        {
            byte[] data = new byte[length];
            for (int i = 0; i < length; i++)
                data[i] = GetRegister(address + i).GetByte();
            
            hwInterface.SetControllerRegister(ScopeController.PIC, address, data);
        }
        public void WriteSingle(PIC r)
        {
            this.WriteSingle((int)r);
        }
        public void ReadSingle(PIC r)
        {
            this.ReadSingle((int)r);
        }
        public ByteRegister GetRegister(PIC r)
        {
            return GetRegister((int)r);
        }
    }
}
