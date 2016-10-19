﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LabNation.DeviceInterface.Hardware
{
public static class Constants {
	public const int HDR_OFFSET = 15;
	public const int SZ_HDR = 64;
	public const int SZ_OVERVIEW = 2048 * 2;
    public const int ACQUISITION_DEPTH_MIN = 128; //Size of RAM
    public const int ACQUISITION_DEPTH_MAX = 4 * 1024 * 1024; //Size of RAM
    public const int FETCH_SIZE_MAX = 2048 * 2;
        
    public static Dictionary<REG, int> HDR_REGS =
		new Dictionary<REG, int>()
    {
		{ REG.TRIGGER_LEVEL, 0 },
		{ REG.TRIGGER_MODE, 1 },
		{ REG.TRIGGERHOLDOFF_B0, 2 },
		{ REG.TRIGGERHOLDOFF_B1, 3 },
		{ REG.TRIGGERHOLDOFF_B2, 4 },
		{ REG.TRIGGERHOLDOFF_B3, 5 },
		{ REG.CHA_YOFFSET_VOLTAGE, 6 },
		{ REG.CHB_YOFFSET_VOLTAGE, 7 },
		{ REG.DIVIDER_MULTIPLIER, 8 },
		{ REG.INPUT_DECIMATION, 9 },
		{ REG.TRIGGER_PW_MIN_B0, 10 },
		{ REG.TRIGGER_PW_MIN_B1, 11 },
		{ REG.TRIGGER_PW_MIN_B2, 12 },
		{ REG.TRIGGER_PW_MAX_B0, 13 },
		{ REG.TRIGGER_PW_MAX_B1, 14 },
		{ REG.TRIGGER_PW_MAX_B2, 15 },
		{ REG.TRIGGER_PWM, 16 },
		{ REG.DIGITAL_TRIGGER_RISING, 17 },
		{ REG.DIGITAL_TRIGGER_FALLING, 18 },
		{ REG.DIGITAL_TRIGGER_HIGH, 19 },
		{ REG.DIGITAL_TRIGGER_LOW, 20 },
		{ REG.ACQUISITION_DEPTH, 21 },
		{ REG.VIEW_DECIMATION, 22 },
		{ REG.VIEW_OFFSET_B0, 23 },
		{ REG.VIEW_OFFSET_B1, 24 },
		{ REG.VIEW_OFFSET_B2, 25 },
		{ REG.VIEW_ACQUISITIONS, 26 },
		{ REG.VIEW_BURSTS, 27 },
		{ REG.VIEW_EXCESS_B0, 28 },
		{ REG.VIEW_EXCESS_B1, 29 },
    };
	public const int N_HDR_REGS = 30;


	public static Dictionary<STR, int> HDR_STROBES =
		new Dictionary<STR, int>()
    {
		{ STR.LA_ENABLE, 0 },
		{ STR.CHA_DCCOUPLING, 1 },
		{ STR.CHB_DCCOUPLING, 2 },
		{ STR.ROLL, 3 },
		{ STR.LA_CHANNEL, 4 },
    };
	public const int N_HDR_STROBES = 5;



	public static List<STR> AcquisitionStrobes =
		new List<STR>()
    {
		STR.LA_ENABLE,
		STR.CHA_DCCOUPLING,
		STR.CHB_DCCOUPLING,
		STR.ROLL,
		STR.LA_CHANNEL,
    };


	public static List<REG> AcquisitionRegisters =
		new List<REG>()
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
		REG.ACQUISITION_DEPTH,
    };


	public static List<REG> ViewRegisters =
		new List<REG>()
    {
		REG.VIEW_DECIMATION,
		REG.VIEW_OFFSET_B0,
		REG.VIEW_OFFSET_B1,
		REG.VIEW_OFFSET_B2,
		REG.VIEW_ACQUISITIONS,
		REG.VIEW_BURSTS,
		REG.VIEW_EXCESS_B0,
		REG.VIEW_EXCESS_B1,
    };

}

	public enum REG
    {
		STROBE_UPDATE = 0,
		SPI_ADDRESS = 1,
		SPI_WRITE_VALUE = 2,
		DIVIDER_MULTIPLIER = 3,
		CHA_YOFFSET_VOLTAGE = 4,
		CHB_YOFFSET_VOLTAGE = 5,
		TRIGGER_PWM = 6,
		TRIGGER_LEVEL = 7,
		TRIGGER_MODE = 8,
		TRIGGER_PW_MIN_B0 = 9,
		TRIGGER_PW_MIN_B1 = 10,
		TRIGGER_PW_MIN_B2 = 11,
		TRIGGER_PW_MAX_B0 = 12,
		TRIGGER_PW_MAX_B1 = 13,
		TRIGGER_PW_MAX_B2 = 14,
		INPUT_DECIMATION = 15,
		ACQUISITION_DEPTH = 16,
		TRIGGERHOLDOFF_B0 = 17,
		TRIGGERHOLDOFF_B1 = 18,
		TRIGGERHOLDOFF_B2 = 19,
		TRIGGERHOLDOFF_B3 = 20,
		VIEW_DECIMATION = 21,
		VIEW_OFFSET_B0 = 22,
		VIEW_OFFSET_B1 = 23,
		VIEW_OFFSET_B2 = 24,
		VIEW_ACQUISITIONS = 25,
		VIEW_BURSTS = 26,
		VIEW_EXCESS_B0 = 27,
		VIEW_EXCESS_B1 = 28,
		DIGITAL_TRIGGER_RISING = 29,
		DIGITAL_TRIGGER_FALLING = 30,
		DIGITAL_TRIGGER_HIGH = 31,
		DIGITAL_TRIGGER_LOW = 32,
		DIGITAL_OUT = 33,
		GENERATOR_DECIMATION_B0 = 34,
		GENERATOR_DECIMATION_B1 = 35,
		GENERATOR_DECIMATION_B2 = 36,
		GENERATOR_SAMPLES_B0 = 37,
		GENERATOR_SAMPLES_B1 = 38,
    }


	public enum STR
    {
		GLOBAL_RESET = 0,
		INIT_SPI_TRANSFER = 1,
		GENERATOR_TO_AWG = 2,
		LA_ENABLE = 3,
		SCOPE_ENABLE = 4,
		SCOPE_UPDATE = 5,
		FORCE_TRIGGER = 6,
		VIEW_UPDATE = 7,
		VIEW_SEND_OVERVIEW = 8,
		VIEW_SEND_PARTIAL = 9,
		ACQ_START = 10,
		ACQ_STOP = 11,
		CHA_DCCOUPLING = 12,
		CHB_DCCOUPLING = 13,
		ENABLE_ADC = 14,
		ENABLE_NEG = 15,
		ENABLE_RAM = 16,
		DOUT_3V_5V = 17,
		EN_OPAMP_B = 18,
		GENERATOR_TO_DIGITAL = 19,
		ROLL = 20,
		LA_CHANNEL = 21,
    }


	public enum ROM
    {
		FW_GIT0 = 0,
		FW_GIT1 = 1,
		FW_GIT2 = 2,
		FW_GIT3 = 3,
		SPI_RECEIVED_VALUE = 4,
		STROBES = 5,
    }

}
