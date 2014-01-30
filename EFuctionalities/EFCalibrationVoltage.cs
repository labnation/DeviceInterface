using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.MemoryRegisters;

namespace ECore.EFuctionalities
{
    public class EFCalibrationVoltage : EFunctionality
    {
        //private static List<EDeviceMemoryRegister> regList;
        private EDeviceMemoryRegister regCalVal;
        private float maxValue;

        //the constructor always takes name and unit
        //and then the registers/eFuncs specifically required for this eFunc.
        //it has to return a registerList to the base class. if no registers are used, an empty list can be returned.
        public EFCalibrationVoltage(string name, string unit, float defaultValue, EDeviceMemoryRegister regCalVal, float maxValue)
            : base(name, unit, defaultValue)
        {
            this.regCalVal = regCalVal;
            this.maxValue = maxValue;

            //create register list, so it can be returned to base constructor
            this.registerList = new List<EDeviceMemoryRegister>();
            this.registerList.Add(regCalVal);     
       
            //set default value
            this.InternalValue = defaultValue;
        }

        //this method should set the InternalValue of all registers
        public override void F2H(float clampedFuncVal)
        {
            regCalVal.InternalValue = (byte)(clampedFuncVal / maxValue * 255f);
        }

        //this method should read the InternalValue of all registers
        public override float H2F()
        {
            return (float)regCalVal.InternalValue / 255f * maxValue;
        }

        public override float CheckRange(float funcVal)
        {
            if (funcVal < 0) funcVal = 0;
            if (funcVal > maxValue) funcVal = maxValue;
            return funcVal;
        }
    }
}
