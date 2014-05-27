using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.HardwareInterfaces;

namespace ECore.DeviceMemories
{
    //this class defines which type of registers it contain, how much of them, and how to access them
    //actual filling of these registers must be defined by the specific HWImplementation, through the constructor of this class
    public class ScopeFpgaRom : ByteMemory
    {
        private IScopeHardwareInterface hwInterface;

        public ScopeFpgaRom(IScopeHardwareInterface hwInterface)
        {
            this.hwInterface = hwInterface;
                        
            foreach (ROM reg in Enum.GetValues(typeof(ROM)))
                registers.Add((int)reg, new ByteRegister((int)reg, Enum.GetName(typeof(ROM), reg)));

            int lastStrobe = (int)Enum.GetValues(typeof(STR)).Cast<STR>().Max();
            for(int i = (int)ROM.STROBES + 1; i < (int)ROM.STROBES + lastStrobe / 8 + 1; i++)
                registers.Add(i, new ByteRegister(i, "STROBES " + (i - (int)ROM.STROBES)));
        }

        public override void Read(int address, int length)
        {
            byte[] data = null;
            hwInterface.GetControllerRegister(ScopeController.FPGA_ROM, address, length, out data);
            if (data == null)
                return;

            for (int j = 0; j < data.Length; j++)
                registers[address + j].Set(data[j]);
        }

        public override void Write(int address, int length)
        {
            Logger.AddEntry(this, LogLevel.Error, "Can't write to ROM");
        }

        public void WriteSingle(ROM r)
        {
            this.WriteSingle((int)r);
        }
        public void ReadSingle(ROM r)
        {
            this.ReadSingle((int)r);
        }
        public ByteRegister GetRegister(ROM r)
        {
            return GetRegister((int)r);
        }
    }
}
