using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
//using System.Windows.Forms;

namespace ECore
{
    //this attribute means that all methods of an interface are 'functionalities'
    //hence, all methods will be scanned for, allowing a gui to be added to the Functionalities panel
    public class FunctionalityInterfaceAttribute : Attribute { }
    
    public delegate UInt32 F2H_Delegate(float clampedFuncVal);
    public delegate float H2F_Delegate(UInt32 hwVal);

    public abstract class EFunctionality
    {
        private string name;
        private string unit;
        protected List<EDeviceMemoryRegister> registerList;
        protected float defaultValue;
        //private EDeviceMemory affectedMemory;
        //private string[] affectedRegisters; //MSB is at index 0!!!
        //private H2F_Delegate H2F;
        //private F2H_Delegate F2H;

        private EFunctionality() { }

        public abstract void F2H(float funcVal);
        public abstract float H2F();

        public EFunctionality(string name, string unit, float defaultValue)//, List<EDeviceMemoryRegister> registers)
        {
            this.name = name;
            this.unit = unit;
            this.defaultValue = defaultValue;
            //this.registerList = registers;

            //check whether child constructuror fileld in the register list
            //if (registerList == null)
                //throw new Exception("Child constructor did not initialize the list of register dependencies");


            /*this.affectedMemory = affectedMemory;
            this.affectedRegisters = affectedRegisters;
            this.F2H = F2H;
            this.H2F = H2F;*/
        }

        public string Name { get { return this.name; } }
        public string Unit { get { return this.unit; } }

        public float WriteToHW(float funcVal)
        {
            //make sure value is within range supported by registers
            float rangedVal = CheckRange(funcVal);

            //update SW registers
            InternalValue = rangedVal;

            //now upload all registers to HW!
            for (int i = 0; i < registerList.Count; i++)
                registerList[i].ParentMemory.WriteSingle(registerList[i].Address);

            return rangedVal;
        }

        //retrieve val from HW
        public float ReadFromHW()
        {            
            //have the registers read their values from HW into their internal memory
            for (int i = 0; i < registerList.Count; i++)
                registerList[i].ParentMemory.ReadSingle(registerList[i].Address);

            return InternalValue; //as this will fetch the internal memories of the registers, which have just been updated
        }

        public float InternalValue
        {
            get
            {
                return H2F();
                /*
                //get value from SW internal register memory
                UInt32 internalValue = affectedMemory.RegisterByName(affectedRegisters[0]).InternalValue;

                //if value is stretched over multiple registers:
                int stride = affectedMemory.MaxValue + 1;
                for (int i = 1; i < affectedRegisters.Length; i++)
                {
                    internalValue *= (UInt32)stride; //bitshift by width of register
                    internalValue += (UInt32)affectedMemory.RegisterByName(affectedRegisters[i]).InternalValue;
                }*/
            }
            set
            {
                float clampedValue = CheckRange(value);
                F2H(clampedValue);
                /*
                //convert to HW value
                UInt32 hwVal = F2H(value);

                //store this value into the SW registers
                UInt32 stride = (UInt32)affectedMemory.MaxValue + 1;
                for (int i = 0; i < affectedRegisters.Length; i++)
                {
                    byte currentRegisterValue = (byte)(hwVal % stride);
                    affectedMemory.RegisterByName(affectedRegisters[affectedRegisters.Length - 1 - i]).InternalValue = currentRegisterValue; //-1-i, because we're storing the smallest value first!
                    hwVal /= stride; //bitshift over width of register
                }
                 * */
            }
        }

        public abstract float CheckRange(float funcVal);
        /*
        {
            //check whether this efunc allows writing
            if (this.F2H == null)
                return 0;

            //find desired hw val
            UInt32 hwVal = F2H(funcVal);

            //find max val that can fit into hw registers
            UInt32 maxSupportedValue = (UInt32)Math.Pow(affectedMemory.MaxValue, affectedRegisters.Length);

            //clamp
            if (hwVal < 0) hwVal = 0;
            if (hwVal > maxSupportedValue) hwVal = maxSupportedValue;

            //convert back to functional value
            float clampedVal = H2F(hwVal);

            return clampedVal;
        }*/
    }
}
