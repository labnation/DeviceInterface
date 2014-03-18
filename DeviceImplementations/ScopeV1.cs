using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.EFuctionalities;
using ECore.DeviceMemories;

namespace ECore.DeviceImplementations
{
    //this is the main class which fills the EDevice with data specific to the HW implementation.
    //eg: which memories, which registers in these memories, which additional functionalities, the start and stop routines, ...
    public class ScopeV1:EDeviceImplementation
    {
        private DeviceMemories.ScopeFpgaSettingsMemory fpgaSettingsMemory;
        private DeviceMemories.ScopeFpgaRom fpgaRom;
        private DeviceMemories.ScopeStrobeMemory strobeMemory;
        private DeviceMemories.MAX19506Memory adcMemory;
        //constructor relaying to base class
        public ScopeV1(EDevice eDevice) : base(eDevice) { }        
        
        public override void InitializeHardwareInterface()
        {
			#if ANDROID
			this.hardwareInterface = new HardwareInterfaces.HWInterfacePIC_Xamarin(this);
            #else
            this.hardwareInterface = new HardwareInterfaces.HWInterfacePIC_LibUSB();
			#endif
        }

		public override void FlashHW ()
		{
		}

        public override ScopeV2.ScopeV2RomManager CreateRomManager()
        {
            throw new NotImplementedException();
        }

        //master method where all memories, registers etc get defined and linked together
        public override void InitializeMemories()
        {
            //add FPGA register memory
            fpgaSettingsMemory = new DeviceMemories.ScopeFpgaSettingsMemory(eDevice);
            memories.Add(fpgaSettingsMemory);

            //add FPGA rom memory     
            fpgaRom = new DeviceMemories.ScopeFpgaRom(eDevice);
            memories.Add(fpgaRom);

            //add strobe memory
            strobeMemory = new DeviceMemories.ScopeStrobeMemory(eDevice, fpgaSettingsMemory);
            memories.Add(strobeMemory);

            //add ADC memory
            adcMemory = new DeviceMemories.MAX19506Memory(eDevice, fpgaSettingsMemory, strobeMemory, fpgaRom);
            memories.Add(adcMemory);
        }

        public override void InitializeFunctionalities()
        {
            functionalities.Add(new ScopeV1CalibrationVoltage(this));
            functionalities.Add(new Scope3v1ScopeChannelB(this));
        }
     
        private string LoadMultilineVHDL()
        {
            string multiline = @"
                //       ||   add VHDL      ||        
                //       \/                 \/
                

constant REG_STROBE_UPDATE	 			: INTEGER := 0;
constant REG_SPI_ADDRESS	  			: INTEGER := 1;
constant REG_SPI_WRITE_VALUE	  		: INTEGER := 2;
constant REG_CALIB_VOLTAGE		  		: INTEGER := 3;
constant REG_TRIGGERLEVEL		  		: INTEGER := 4;
constant REG_TRIGGERHOLDOFF_B0	  		: INTEGER := 5;
constant REG_TRIGGERHOLDOFF_B1	  		: INTEGER := 6;
constant REG_SAMPLECLOCKDIV_B0  		: INTEGER := 7;
constant REG_SAMPLECLOCKDIV_B1  		: INTEGER := 8;


constant STR_DEBUGCOUNTER_PIC_OUTPUT  		: INTEGER := 0;
constant STR_INIT_SPI_TRANSFER  			: INTEGER := 1;
constant STR_GLOBAL_RESET	  			: INTEGER := 2;
constant STR_DEBUG_FIFO_A				: INTEGER := 3;
constant STR_FORCE_TRIGGER				: INTEGER := 4;
constant STR_FREE_RUNNING				: INTEGER := 5;
constant STR_CHB_DIV1					: INTEGER := 6;
constant STR_CHB_DIV10				: INTEGER := 7;
constant STR_CHB_DIV100				: INTEGER := 8;
constant STR_CHB_MULT1				: INTEGER := 9;
constant STR_CHB_MULT2				: INTEGER := 10;
constant STR_CHB_MULT3				: INTEGER := 11;
constant STR_CHB_MULT4				: INTEGER := 12;
constant STR_CHB_ENABLECALIB			: INTEGER := 13;
constant STR_CHB_DCCOUPLING				: INTEGER := 14;




constant ROM_FW_MSB	  				: INTEGER := 0;
constant ROM_FW_LSB	  				: INTEGER := 1;
constant ROM_FW_BUILD	  				: INTEGER := 2;
constant ROM_SPI_RECEIVED_VALUE 			: INTEGER := 3;
constant ROM_FIFO_STATUS	 			: INTEGER := 4;

                //       /\                 /\
                //       ||    end VHDL     ||
                //";

            return multiline;
        }

        override public void Start()
        {
            //set feedback loopand to 1V for demo purpose and enable
            strobeMemory.GetRegister(STR.CHB_DIV1).InternalValue = 1;
            strobeMemory.WriteSingle(STR.CHB_DIV1);

            strobeMemory.GetRegister(STR.CHB_MULT1).InternalValue = 1;
            strobeMemory.WriteSingle(STR.CHB_MULT1);

            fpgaSettingsMemory.GetRegister(REG.CALIB_VOLTAGE).InternalValue = 78;
            fpgaSettingsMemory.WriteSingle(REG.CALIB_VOLTAGE);

            //set ADC output resistance to 300ohm instead of 50ohm
            adcMemory.GetRegister(MAX19506.CHA_TERMINATION).InternalValue = 4;
            adcMemory.WriteSingle(MAX19506.CHA_TERMINATION);
            adcMemory.GetRegister(MAX19506.CHB_TERMINATION).InternalValue = 4;
            adcMemory.WriteSingle(MAX19506.CHB_TERMINATION);

            //set ADC to offset binary output (required for FPGA triggering)
            adcMemory.GetRegister(MAX19506.FORMAT_PATTERN).InternalValue = 16;
            adcMemory.WriteSingle(MAX19506.FORMAT_PATTERN);

			//output counter by default
			strobeMemory.GetRegister (STR.DEBUGCOUNTER_PIC_OUTPUT).InternalValue = 1;
			strobeMemory.WriteSingle (STR.DEBUGCOUNTER_PIC_OUTPUT);

            //freerunning as global enable
            strobeMemory.GetRegister(STR.FREE_RUNNING).InternalValue = 1;
            strobeMemory.WriteSingle(STR.FREE_RUNNING);
        }

        override public void Stop()
        {
        }

        public override float[] GetRawData()
        {
            int samplesToFetch = 4096;
            int bytesToFetch = samplesToFetch;
            byte[] rawData = eDevice.HWInterface.GetData(bytesToFetch);
            float[] rawFloats = new float[samplesToFetch];
            for (int i = 0; i < rawData.Length; i++)
                rawFloats[i] = (float)rawData[i];

            return rawFloats;
        }

        public override float[] ConvertRawDataToVoltages(float[] rawFloats)
        {            
            return rawFloats;            
        }

        //private nested classes, shielding this from the outside.
        //only ScopeV1 can instantiate this class!
        private class ScopeV1CalibrationVoltage: EInterfaces.ICalibrationVoltage
        {
            //private EFunctionality calibrationEnabled;
            private EFunctionality calibrationVoltage;

            //public EFunctionality CalibrationEnabled { get { return calibrationEnabled; } }
            public EFunctionality CalibrationVoltage { get { return calibrationVoltage; } }

            public ScopeV1CalibrationVoltage(ScopeV1 deviceImplementation)
            {
                this.calibrationVoltage = new EFCalibrationVoltage("Calibration voltage", "V", 0, deviceImplementation.fpgaSettingsMemory.GetRegister(REG.CALIB_VOLTAGE), 3.3f);
                //this.calibrationEnabled = new EFunctionality("Calibration enabled", "", deviceImplementation.strobeMemory, new string[] { "STR_CHB_DIV1" }, F2H_CalibEnabled, H2F_CalibEnabled);
            }
        }

        private class Scope3v1ScopeChannelB : EInterfaces.IScopeChannel
        {
            private EFunctionality multiplicationFactor;
            private EFunctionality divisionFactor;
            private EFunctionality samplingFrequency;
            private EFOffset channelOffset;

            public EFunctionality MultiplicationFactor { get { return multiplicationFactor; } }
            public EFunctionality DivisionFactor { get { return divisionFactor; } }
            public EFunctionality SamplingFrequency { get { return samplingFrequency; } }
            public EFunctionality ChannelOffset { get { return channelOffset; } }

            public Scope3v1ScopeChannelB(ScopeV1 deviceImplementation)
            {
                EDeviceMemoryRegister[] multiplicationStrobes = new EDeviceMemoryRegister[3];
                multiplicationStrobes[0] = deviceImplementation.strobeMemory.GetRegister(STR.CHB_MULT1);
                multiplicationStrobes[1] = deviceImplementation.strobeMemory.GetRegister(STR.CHB_MULT2);
                multiplicationStrobes[2] = deviceImplementation.strobeMemory.GetRegister(STR.CHB_MULT3);

                int[] multiplyingResistors = new int[3] { 0, 1000, 6200 };

                this.channelOffset = new EFOffset("Channel offset", "V", 0);
                this.multiplicationFactor = new EFMultiplicationFactor("Multiplication factor", "", 1, multiplicationStrobes, 1000, multiplyingResistors, 24, channelOffset);
                this.divisionFactor = new EFDivisionFactor("Division factor", "", 1);
                this.samplingFrequency = new EFSamplingFrequency("Sampling Frequency", "Hz", 100000000);
            }

            public float MaxRange { get { return 255f; } }
            public float ScalingFactor { get { return multiplicationFactor.InternalValue / divisionFactor.InternalValue; } }
        }
    }
}
