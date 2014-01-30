using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.MemoryRegisters;

namespace ECore.EFuctionalities
{
    public class EFMultiplicationFactor : EFunctionality
    {
        EDeviceMemoryRegister[] strobeRegisters;
        EFOffset efOffset;
        int baseResistor;
        int[] multiplyingResistors;
        int switchResistance;
        float[] possibleMultiplications;
        float[] correspondingOffsets;

        //the constructor always takes name and unit
        //and then the registers/eFuncs specifically required for this eFunc.
        //it has to return a registerList to the base class. if no registers are used, an empty list can be returned.
        public EFMultiplicationFactor(string name, string unit, float defaultValue, EDeviceMemoryRegister[] strobeRegisters, int baseResistor, int[] multiplyingResistors, int switchResistance, EFOffset efOffset)
            : base(name, unit, defaultValue)
        {
            this.strobeRegisters = strobeRegisters;
            this.baseResistor = baseResistor; ;
            this.multiplyingResistors = multiplyingResistors;
            this.switchResistance = switchResistance;
            this.efOffset = efOffset;

            //create register list, so it can be returned to base constructor
            this.registerList = new List<EDeviceMemoryRegister>();
            foreach (EDeviceMemoryRegister reg in strobeRegisters)
                this.registerList.Add(reg);

            //get possible multiplication factors from ROM
            possibleMultiplications = new float[multiplyingResistors.Length];
            possibleMultiplications[0] = 1.0828f;
            possibleMultiplications[1] = 2.5811f;
            possibleMultiplications[2] = 9.5344f;

            correspondingOffsets = new float[multiplyingResistors.Length];
            correspondingOffsets[0] = -0.1241f;
            correspondingOffsets[1] = -0.1076f;
            correspondingOffsets[2] = -0.0295f;

            //set default value
            this.InternalValue = defaultValue;
        }

        //this method should set the InternalValue of all registers
        public override void F2H(float clampedFuncVal)
        {
            for (int i = 0; i < strobeRegisters.Length; i++)
            {
                if (possibleMultiplications[i] == clampedFuncVal)
                {
                    //make-before-break: first enable the correct switch, this disable all others
                    EDeviceMemoryRegister register = strobeRegisters[i];
                    register.InternalValue = 1;

                    //set offset, as this is dependant on the multiplier chosen
                    this.efOffset.InternalValue = correspondingOffsets[i];

                    //disable all others
                    for (int j = 0; j < strobeRegisters.Length; j++)
                        if (i != j)
                            strobeRegisters[j].InternalValue = 0;
                }
            }
        }

        //this method should read the InternalValue of all registers
        public override float H2F()
        {
            //return first activated switch
            for (int i = 0; i < strobeRegisters.Length; i++)
			{
			    EDeviceMemoryRegister register = strobeRegisters[i];
                if (register.InternalValue == 1)
                    return possibleMultiplications[i];
			}

            //if this section was reached, no multiplication factor was set
            return 0;
        }

        public override float CheckRange(float funcVal)
        {
            //find the differences between what's asked for and what's possible
            float[] distancesFromPossibilities = new float[possibleMultiplications.Length];
            for (int i = 0; i < possibleMultiplications.Length; i++)
                distancesFromPossibilities[i] = (float)Math.Abs(funcVal - this.possibleMultiplications[i]);

            //return the closest possibilty
            int minIndex = Array.IndexOf(distancesFromPossibilities, distancesFromPossibilities.Min());
            return possibleMultiplications[minIndex];
        }
    }
}
