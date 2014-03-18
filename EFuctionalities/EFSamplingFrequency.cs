using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.MemoryRegisters;

namespace ECore.EFuctionalities
{
    public class EFSamplingFrequency : EFunctionality
    {
        //the constructor always takes name and unit
        //and then the registers/eFuncs specifically required for this eFunc.
        //it has to return a registerList to the base class. if no registers are used, an empty list can be returned.
        public EFSamplingFrequency(string name, string unit, float defaultValue)
            : base(name, unit, defaultValue)
        {
            //create register list, so it can be returned to base constructor
            this.registerList = new List<EDeviceMemoryRegister>();

            //calculate possible multiplication factors

            //set default value
            this.InternalValue = defaultValue;            
        }

        //this method should set the InternalValue of all registers
        public override void F2H(float clampedFuncVal)
        {
        }

        //this method should read the InternalValue of all registers
        public override float H2F()
        {
            return defaultValue;
        }

        public override float CheckRange(float funcVal)
        {
            return 1;
        }
    }
}
