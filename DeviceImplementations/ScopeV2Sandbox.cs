using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.DeviceMemories;

namespace ECore.DeviceImplementations
{
    partial class ScopeV2
    {

        #region sandbox

        public void DemoLCTank()
        {
            FpgaSettingsMemory.GetRegister(REG.TRIGGERLEVEL).InternalValue = 78;
            FpgaSettingsMemory.WriteSingle(REG.TRIGGERLEVEL);

            FpgaSettingsMemory.GetRegister(REG.TRIGGERHOLDOFF_B0).InternalValue = 230;
            FpgaSettingsMemory.WriteSingle(REG.TRIGGERHOLDOFF_B0);

            FpgaSettingsMemory.GetRegister(REG.TRIGGERHOLDOFF_B1).InternalValue = 3;
            FpgaSettingsMemory.WriteSingle(REG.TRIGGERHOLDOFF_B1);

            FpgaSettingsMemory.GetRegister(REG.CHB_YOFFSET_VOLTAGE).InternalValue = 90;
            FpgaSettingsMemory.WriteSingle(REG.CHB_YOFFSET_VOLTAGE);

            StrobeMemory.GetRegister(STR.FREE_RUNNING).InternalValue = 0;
            StrobeMemory.WriteSingle(STR.FREE_RUNNING);

            StrobeMemory.GetRegister(STR.CHB_DIV10).InternalValue = 1;
            StrobeMemory.WriteSingle(STR.CHB_DIV10);

            StrobeMemory.GetRegister(STR.CHB_MULT1).InternalValue = 0;
            StrobeMemory.WriteSingle(STR.CHB_MULT1);

            FpgaSettingsMemory.GetRegister(REG.SAMPLECLOCKDIVIDER_B1).InternalValue = 0;
            FpgaSettingsMemory.WriteSingle(REG.SAMPLECLOCKDIVIDER_B1);
        }

        public void DemoArduino()
        {
            FpgaSettingsMemory.GetRegister(REG.TRIGGERLEVEL).InternalValue = 119;
            FpgaSettingsMemory.WriteSingle(REG.TRIGGERLEVEL);

            FpgaSettingsMemory.GetRegister(REG.TRIGGERHOLDOFF_B0).InternalValue = 55;
            FpgaSettingsMemory.WriteSingle(REG.TRIGGERHOLDOFF_B0);

            FpgaSettingsMemory.GetRegister(REG.TRIGGERHOLDOFF_B1).InternalValue = 1;
            FpgaSettingsMemory.WriteSingle(REG.TRIGGERHOLDOFF_B1);

            FpgaSettingsMemory.GetRegister(REG.CHA_YOFFSET_VOLTAGE).InternalValue = 35;
            FpgaSettingsMemory.WriteSingle(REG.CHA_YOFFSET_VOLTAGE);

            FpgaSettingsMemory.GetRegister(REG.CHB_YOFFSET_VOLTAGE).InternalValue = 36;
            FpgaSettingsMemory.WriteSingle(REG.CHB_YOFFSET_VOLTAGE);

            FpgaSettingsMemory.GetRegister(REG.SAMPLECLOCKDIVIDER_B1).InternalValue = 100;
            FpgaSettingsMemory.WriteSingle(REG.SAMPLECLOCKDIVIDER_B1);

            StrobeMemory.GetRegister(STR.FREE_RUNNING).InternalValue = 0;
            StrobeMemory.WriteSingle(STR.FREE_RUNNING);


            StrobeMemory.GetRegister(STR.CHB_DIV1).InternalValue = 0;
            StrobeMemory.WriteSingle(STR.CHB_DIV1);

            StrobeMemory.GetRegister(STR.CHB_DIV10).InternalValue = 1;
            StrobeMemory.WriteSingle(STR.CHB_DIV10);

            StrobeMemory.GetRegister(STR.CHB_MULT1).InternalValue = 1;
            StrobeMemory.WriteSingle(STR.CHB_MULT1);

            StrobeMemory.GetRegister(STR.CHB_MULT2).InternalValue = 1;
            StrobeMemory.WriteSingle(STR.CHB_MULT2);

            StrobeMemory.GetRegister(STR.CHA_DIV1).InternalValue = 1;
            StrobeMemory.WriteSingle(STR.CHA_DIV1);

            StrobeMemory.GetRegister(STR.CHA_DIV10).InternalValue = 0;
            StrobeMemory.WriteSingle(STR.CHA_DIV10);

            StrobeMemory.GetRegister(STR.CHA_MULT1).InternalValue = 0;
            StrobeMemory.WriteSingle(STR.CHA_MULT1);

            StrobeMemory.GetRegister(STR.CHA_MULT2).InternalValue = 0;
            StrobeMemory.WriteSingle(STR.CHA_MULT2);
        }

        #endregion
    }
}
