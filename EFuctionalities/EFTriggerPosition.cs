using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.MemoryRegisters;

namespace ECore.EFuctionalities
{
    public class EFTriggerPosition : EFunctionality
    {
        //private static List<EDeviceMemoryRegister> regList;
        private EDeviceMemoryRegister trigPosRegister;

        //the constructor always takes name and unit
        //and then the registers/eFuncs specifically required for this eFunc.
        //it has to return a registerList to the base class. if no registers are used, an empty list can be returned.
        public EFTriggerPosition(string name, string unit, float defaultValue, EDeviceMemoryRegister trigPosRegister)
            : base(name, unit, defaultValue)
        {
            this.trigPosRegister = trigPosRegister;

            //create register list, so it can be returned to base constructor
            this.registerList = new List<EDeviceMemoryRegister>();
            this.registerList.Add(trigPosRegister);

            //set default value
            this.InternalValue = defaultValue;
        }

        //this method should set the InternalValue of all registers
        public override void F2H(float clampedFuncVal)
        {
            trigPosRegister.InternalValue = (byte)(clampedFuncVal);
        }

        //this method should read the InternalValue of all registers
        public override float H2F()
        {
            return (float)trigPosRegister.InternalValue;
        }

        public override float CheckRange(float funcVal)
        {
            if (funcVal < 0) funcVal = 0;
            if (funcVal > 256) funcVal = 256;
            return funcVal;
        }
    }
}
