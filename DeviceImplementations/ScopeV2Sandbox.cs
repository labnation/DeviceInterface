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
            fpgaSettingsMemory.GetRegister(REG.TRIGGERLEVEL).InternalValue = 78;
            fpgaSettingsMemory.WriteSingle(REG.TRIGGERLEVEL);

            fpgaSettingsMemory.GetRegister(REG.TRIGGERHOLDOFF_B0).InternalValue = 230;
            fpgaSettingsMemory.WriteSingle(REG.TRIGGERHOLDOFF_B0);

            fpgaSettingsMemory.GetRegister(REG.TRIGGERHOLDOFF_B1).InternalValue = 3;
            fpgaSettingsMemory.WriteSingle(REG.TRIGGERHOLDOFF_B1);

            fpgaSettingsMemory.GetRegister(REG.CHB_YOFFSET_VOLTAGE).InternalValue = 90;
            fpgaSettingsMemory.WriteSingle(REG.CHB_YOFFSET_VOLTAGE);

            strobeMemory.GetRegister(STR.FREE_RUNNING).InternalValue = 0;
            strobeMemory.WriteSingle(STR.FREE_RUNNING);

            strobeMemory.GetRegister(STR.CHB_DIV10).InternalValue = 1;
            strobeMemory.WriteSingle(STR.CHB_DIV10);

            strobeMemory.GetRegister(STR.CHB_MULT1).InternalValue = 0;
            strobeMemory.WriteSingle(STR.CHB_MULT1);

            fpgaSettingsMemory.GetRegister(REG.SAMPLECLOCKDIVIDER_B1).InternalValue = 0;
            fpgaSettingsMemory.WriteSingle(REG.SAMPLECLOCKDIVIDER_B1);
        }

        public void DemoArduino()
        {
            fpgaSettingsMemory.GetRegister(REG.TRIGGERLEVEL).InternalValue = 119;
            fpgaSettingsMemory.WriteSingle(REG.TRIGGERLEVEL);

            fpgaSettingsMemory.GetRegister(REG.TRIGGERHOLDOFF_B0).InternalValue = 55;
            fpgaSettingsMemory.WriteSingle(REG.TRIGGERHOLDOFF_B0);

            fpgaSettingsMemory.GetRegister(REG.TRIGGERHOLDOFF_B1).InternalValue = 1;
            fpgaSettingsMemory.WriteSingle(REG.TRIGGERHOLDOFF_B1);

            fpgaSettingsMemory.GetRegister(REG.CHA_YOFFSET_VOLTAGE).InternalValue = 35;
            fpgaSettingsMemory.WriteSingle(REG.CHA_YOFFSET_VOLTAGE);

            fpgaSettingsMemory.GetRegister(REG.CHB_YOFFSET_VOLTAGE).InternalValue = 36;
            fpgaSettingsMemory.WriteSingle(REG.CHB_YOFFSET_VOLTAGE);

            fpgaSettingsMemory.GetRegister(REG.SAMPLECLOCKDIVIDER_B1).InternalValue = 100;
            fpgaSettingsMemory.WriteSingle(REG.SAMPLECLOCKDIVIDER_B1);

            strobeMemory.GetRegister(STR.FREE_RUNNING).InternalValue = 0;
            strobeMemory.WriteSingle(STR.FREE_RUNNING);


            strobeMemory.GetRegister(STR.CHB_DIV1).InternalValue = 0;
            strobeMemory.WriteSingle(STR.CHB_DIV1);

            strobeMemory.GetRegister(STR.CHB_DIV10).InternalValue = 1;
            strobeMemory.WriteSingle(STR.CHB_DIV10);

            strobeMemory.GetRegister(STR.CHB_MULT1).InternalValue = 1;
            strobeMemory.WriteSingle(STR.CHB_MULT1);

            strobeMemory.GetRegister(STR.CHB_MULT2).InternalValue = 1;
            strobeMemory.WriteSingle(STR.CHB_MULT2);

            strobeMemory.GetRegister(STR.CHA_DIV1).InternalValue = 1;
            strobeMemory.WriteSingle(STR.CHA_DIV1);

            strobeMemory.GetRegister(STR.CHA_DIV10).InternalValue = 0;
            strobeMemory.WriteSingle(STR.CHA_DIV10);

            strobeMemory.GetRegister(STR.CHA_MULT1).InternalValue = 0;
            strobeMemory.WriteSingle(STR.CHA_MULT1);

            strobeMemory.GetRegister(STR.CHA_MULT2).InternalValue = 0;
            strobeMemory.WriteSingle(STR.CHA_MULT2);
        }

        #endregion
    }
}
