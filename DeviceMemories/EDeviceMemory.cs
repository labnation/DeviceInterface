using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
//using System.Windows.Forms;

namespace ECore
{
    //abstract class, representing a physical memory on the PCB.
    //examples: ROM, FPGA, FX2, ADC register banks, ...
    abstract public class EDeviceMemory
    {
        protected List<EDeviceMemoryRegister> registers;
        protected EDevice eDevice;

        abstract public void WriteRange(int startAddress, int burstSize);
        abstract public void ReadRange(int startAddress, int burstSize);
        
        virtual public void WriteSingle(int registerAddress)
        {
            this.WriteRange(registerAddress, 1);
        }

        virtual public void ReadSingle(int registerAddress)
        {
            ReadRange(registerAddress, 1);
        }

        public int MaxValue { get { return registers[0].MaxValue; } }
        public int NumberOfRegisters { get { return registers.Count; } }
        public List<EDeviceMemoryRegister> Registers { get { return registers; } }

    }
}
