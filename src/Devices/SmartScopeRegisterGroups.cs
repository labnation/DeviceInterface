using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.Memories;

namespace LabNation.DeviceInterface.Devices
{
    partial class SmartScope
    {
        /*
         * WARNING: the following arrays are manually constructed from VHDL code in
         * TypesConstants.vhd - Make sure that when you change the VHDL, you also
         * update this code
         */
        internal static readonly REG[] AcquisitionRegisters = new REG[]
        {
            REG.TRIGGER_LEVEL, 
            REG.TRIGGER_MODE,
			REG.TRIGGERHOLDOFF_B0, 
            REG.TRIGGERHOLDOFF_B1, 
            REG.TRIGGERHOLDOFF_B2, 
            REG.TRIGGERHOLDOFF_B3, 
			REG.CHA_YOFFSET_VOLTAGE, 
			REG.CHB_YOFFSET_VOLTAGE, 
			REG.DIVIDER_MULTIPLIER,
			REG.INPUT_DECIMATION, 
            REG.TRIGGER_PW_MIN_B0,
			REG.TRIGGER_PW_MIN_B1,
			REG.TRIGGER_PW_MIN_B2,
            REG.TRIGGER_PW_MAX_B0,
			REG.TRIGGER_PW_MAX_B1,
			REG.TRIGGER_PW_MAX_B2,
            REG.TRIGGER_PWM,
			REG.DIGITAL_TRIGGER_RISING,
			REG.DIGITAL_TRIGGER_FALLING,
			REG.DIGITAL_TRIGGER_HIGH,
			REG.DIGITAL_TRIGGER_LOW,
            REG.ACQUISITION_DEPTH
        };
        internal static readonly REG[] DumpRegisters = new REG[]
        {
			REG.VIEW_DECIMATION,
			REG.VIEW_OFFSET_B0,
            REG.VIEW_OFFSET_B1,
            REG.VIEW_OFFSET_B2,
			REG.VIEW_ACQUISITIONS,
			REG.VIEW_BURSTS,
            REG.VIEW_EXCESS_B0,
            REG.VIEW_EXCESS_B1
        };
        internal static readonly STR[] AcquisitionStrobes = new STR[]
        {
			STR.LA_ENABLE,
			STR.CHA_DCCOUPLING,
			STR.CHB_DCCOUPLING,
            STR.ROLL,
            STR.LA_CHANNEL
        };
    }
}
