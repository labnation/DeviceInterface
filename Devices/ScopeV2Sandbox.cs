using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.DeviceMemories;

namespace ECore.Devices
{
    partial class ScopeV2
    {

        #region sandbox

        public void DemoLCTank()
        {
            FpgaSettingsMemory.GetRegister(REG.TRIGGERLEVEL).Set(78);
            FpgaSettingsMemory.WriteSingle(REG.TRIGGERLEVEL);

            FpgaSettingsMemory.GetRegister(REG.TRIGGERHOLDOFF_B0).Set(230);
            FpgaSettingsMemory.WriteSingle(REG.TRIGGERHOLDOFF_B0);

            FpgaSettingsMemory.GetRegister(REG.TRIGGERHOLDOFF_B1).Set(3);
            FpgaSettingsMemory.WriteSingle(REG.TRIGGERHOLDOFF_B1);

            FpgaSettingsMemory.GetRegister(REG.CHB_YOFFSET_VOLTAGE).Set(90);
            FpgaSettingsMemory.WriteSingle(REG.CHB_YOFFSET_VOLTAGE);

            StrobeMemory.GetRegister(STR.FREE_RUNNING).Set(0);
            StrobeMemory.WriteSingle(STR.FREE_RUNNING);

            StrobeMemory.GetRegister(STR.CHB_DIV10).Set(1);
            StrobeMemory.WriteSingle(STR.CHB_DIV10);

            StrobeMemory.GetRegister(STR.CHB_MULT1).Set(0);
            StrobeMemory.WriteSingle(STR.CHB_MULT1);

            FpgaSettingsMemory.GetRegister(REG.SAMPLECLOCKDIVIDER_B1).Set(0);
            FpgaSettingsMemory.WriteSingle(REG.SAMPLECLOCKDIVIDER_B1);
        }

        public void DemoArduino()
        {
            FpgaSettingsMemory.GetRegister(REG.TRIGGERLEVEL).Set(119);
            FpgaSettingsMemory.WriteSingle(REG.TRIGGERLEVEL);

            FpgaSettingsMemory.GetRegister(REG.TRIGGERHOLDOFF_B0).Set(55);
            FpgaSettingsMemory.WriteSingle(REG.TRIGGERHOLDOFF_B0);

            FpgaSettingsMemory.GetRegister(REG.TRIGGERHOLDOFF_B1).Set(1);
            FpgaSettingsMemory.WriteSingle(REG.TRIGGERHOLDOFF_B1);

            FpgaSettingsMemory.GetRegister(REG.CHA_YOFFSET_VOLTAGE).Set(35);
            FpgaSettingsMemory.WriteSingle(REG.CHA_YOFFSET_VOLTAGE);

            FpgaSettingsMemory.GetRegister(REG.CHB_YOFFSET_VOLTAGE).Set(36);
            FpgaSettingsMemory.WriteSingle(REG.CHB_YOFFSET_VOLTAGE);

            FpgaSettingsMemory.GetRegister(REG.SAMPLECLOCKDIVIDER_B1).Set(100);
            FpgaSettingsMemory.WriteSingle(REG.SAMPLECLOCKDIVIDER_B1);

            StrobeMemory.GetRegister(STR.FREE_RUNNING).Set(0);
            StrobeMemory.WriteSingle(STR.FREE_RUNNING);


            StrobeMemory.GetRegister(STR.CHB_DIV1).Set(0);
            StrobeMemory.WriteSingle(STR.CHB_DIV1);

            StrobeMemory.GetRegister(STR.CHB_DIV10).Set(1);
            StrobeMemory.WriteSingle(STR.CHB_DIV10);

            StrobeMemory.GetRegister(STR.CHB_MULT1).Set(1);
            StrobeMemory.WriteSingle(STR.CHB_MULT1);

            StrobeMemory.GetRegister(STR.CHB_MULT2).Set(1);
            StrobeMemory.WriteSingle(STR.CHB_MULT2);

            StrobeMemory.GetRegister(STR.CHA_DIV1).Set(1);
            StrobeMemory.WriteSingle(STR.CHA_DIV1);

            StrobeMemory.GetRegister(STR.CHA_DIV10).Set(0);
            StrobeMemory.WriteSingle(STR.CHA_DIV10);

            StrobeMemory.GetRegister(STR.CHA_MULT1).Set(0);
            StrobeMemory.WriteSingle(STR.CHA_MULT1);

            StrobeMemory.GetRegister(STR.CHA_MULT2).Set(0);
            StrobeMemory.WriteSingle(STR.CHA_MULT2);
        }

        #endregion
    }
}
