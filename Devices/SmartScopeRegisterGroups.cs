using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.DeviceMemories;

namespace ECore.Devices
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
            REG.TRIGGER_WIDTH,
			REG.TRIGGERHOLDOFF_B0, 
            REG.TRIGGERHOLDOFF_B1, 
            REG.TRIGGERHOLDOFF_B2, 
            REG.TRIGGERHOLDOFF_B3, 
			REG.CHA_YOFFSET_VOLTAGE, 
			REG.CHB_YOFFSET_VOLTAGE, 
			REG.DIVIDER_MULTIPLIER,
			REG.INPUT_DECIMATION, 
            REG.TRIGGER_THRESHOLD,
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
			REG.VIEW_BURSTS
        };
        internal static readonly STR[] AcquisitionStrobes = new STR[]
        {
			STR.AWG_ENABLE,
			STR.LA_ENABLE,
			STR.CHA_DCCOUPLING,
			STR.CHB_DCCOUPLING,
            STR.DIGI_DEBUG,
            STR.ROLL,
            STR.LA_CHANNEL
        };
    }
}
