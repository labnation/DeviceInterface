using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.EFuctionalities;

namespace ECore.DeviceImplementations
{
    //this is the main class which fills the EDevice with data specific to the HW implementation.
    //eg: which memories, which registers in these memories, which additional functionalities, the start and stop routines, ...
    public class Scop3v1:EDeviceImplementation
    {
        private DeviceMemories.Scop3FpgaRegisterMemory fpgaMemory;
        private DeviceMemories.Scop3FpgaRomMemory romMemory;
        private DeviceMemories.Scop3StrobeMemory strobeMemory;
        private DeviceMemories.MAX19506Memory adcMemory;
        //constructor relaying to base class
        public Scop3v1(EDevice eDevice) : base(eDevice) { }        
        
        public override EDeviceHWInterface CreateHWInterface()
        {
			#if ANDROID
			return new HardwareInterfaces.HWInterfacePIC_Xamarin(this);
			#else
            return new HardwareInterfaces.HWInterfacePIC_LibUSB();
			#endif
        }

		public override void FlashHW ()
		{
		}

        public override Scop3v2.Scop3v2RomManager CreateRomManager()
        {
            throw new NotImplementedException();
        }

        //master method where all memories, registers etc get defined and linked together
        public override List<EDeviceMemory> CreateMemories()
        {
            memories = new List<EDeviceMemory>();
            
            //add FPGA register memory
            Dictionary<string, int> fpgaRegisters = Utils.VhdlReader(LoadMultilineVHDL(), "REG");
            fpgaMemory = new DeviceMemories.Scop3FpgaRegisterMemory(eDevice, fpgaRegisters);
            memories.Add(fpgaMemory);

            //add FPGA rom memory     
            Dictionary<string, int> fpgaRoms = Utils.VhdlReader(LoadMultilineVHDL(), "ROM");
            romMemory = new DeviceMemories.Scop3FpgaRomMemory(eDevice, fpgaRoms);
            memories.Add(romMemory);

            //add strobe memory
            Dictionary<string, int> fpgaStrobes = Utils.VhdlReader(LoadMultilineVHDL(), "STR");
            strobeMemory = new DeviceMemories.Scop3StrobeMemory(eDevice, fpgaStrobes, fpgaMemory);
            memories.Add(strobeMemory);

            //add ADC memory
            adcMemory = new DeviceMemories.MAX19506Memory(eDevice, fpgaMemory, strobeMemory, romMemory);
            memories.Add(adcMemory);

            return memories;
        }

        public override List<object> CreateFunctionalities()
        {
            List<object> functionalities = new List<object>();
            functionalities.Add(new Scope3v1CalibrationVoltage(this));
            functionalities.Add(new Scope3v1ScopeChannelB(this));

            return functionalities;
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

        override public void StartDevice()
        {
            //set feedback loopand to 1V for demo purpose and enable
            strobeMemory.RegisterByName("STR_CHB_DIV1").InternalValue = 1;
            strobeMemory.WriteSingle("STR_CHB_DIV1");

            strobeMemory.RegisterByName("STR_CHB_MULT1").InternalValue = 1;
            strobeMemory.WriteSingle("STR_CHB_MULT1");

            fpgaMemory.RegisterByName("REG_CALIB_VOLTAGE").InternalValue = 78;
            fpgaMemory.WriteSingle("REG_CALIB_VOLTAGE");

            //set ADC output resistance to 300ohm instead of 50ohm
            adcMemory.RegisterByName("MAX_CHA_TERMINATION").InternalValue = 4;
            adcMemory.WriteSingle("MAX_CHA_TERMINATION");
            adcMemory.RegisterByName("MAX_CHB_TERMINATION").InternalValue = 4;
            adcMemory.WriteSingle("MAX_CHB_TERMINATION");

            //set ADC to offset binary output (required for FPGA triggering)
            adcMemory.RegisterByName("MAX_FORMAT/PATTERN").InternalValue = 16;
            adcMemory.WriteSingle("MAX_FORMAT/PATTERN");

			//output counter by default
			strobeMemory.RegisterByName ("STR_DEBUGCOUNTER_PIC_OUTPUT").InternalValue = 1;
			strobeMemory.WriteSingle ("STR_DEBUGCOUNTER_PIC_OUTPUT");

            //freerunning as global enable
            strobeMemory.RegisterByName("STR_FREE_RUNNING").InternalValue = 1;
            strobeMemory.WriteSingle("STR_FREE_RUNNING");
        }

        override public void StopDevice()
        {
        }

        public override float[] GetDataAndConvertToVoltageValues()
        {
            int samplesToFetch = 4096;
            int bytesToFetch = samplesToFetch;
            byte[] rawData = eDevice.HWInterface.GetData(bytesToFetch);
            float[] voltageValues = new float[samplesToFetch];

            /*
            //this section converts twos complement to a physical voltage value
            for (int i = 0; i < rawData.Length; i++)
            {
                byte byteVal = (byte)rawData[i];
                float twosVal = (float)(sbyte)byteVal;
                float scaledVal = twosVal + 128;
                voltageValues[i] = scaledVal / 255f * 1.8f;
            }*/

            //this section converts twos complement to a physical voltage value
            for (int i = 0; i < rawData.Length; i++)
            {
                byte byteVal = (byte)rawData[i];
                //float twosVal = (float)byteVal;
                //float scaledVal = twosVal + 128;
                voltageValues[i] = (float)byteVal / 255f * 255f;
            }

            return voltageValues;            
        }

        //private nested classes, shielding this from the outside.
        //only Scop3v1 can instantiate this class!
        private class Scope3v1CalibrationVoltage: EInterfaces.ICalibrationVoltage
        {
            //private EFunctionality calibrationEnabled;
            private EFunctionality calibrationVoltage;

            //public EFunctionality CalibrationEnabled { get { return calibrationEnabled; } }
            public EFunctionality CalibrationVoltage { get { return calibrationVoltage; } }

            public Scope3v1CalibrationVoltage(Scop3v1 deviceImplementation)
            {
                this.calibrationVoltage = new EFCalibrationVoltage("Calibration voltage", "V", 0, deviceImplementation.fpgaMemory.RegisterByName("REG_CALIB_VOLTAGE"), 3.3f);
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

            public Scope3v1ScopeChannelB(Scop3v1 deviceImplementation)
            {
                EDeviceMemoryRegister[] multiplicationStrobes = new EDeviceMemoryRegister[3];
                multiplicationStrobes[0] = deviceImplementation.strobeMemory.RegisterByName("STR_CHB_MULT1");
                multiplicationStrobes[1] = deviceImplementation.strobeMemory.RegisterByName("STR_CHB_MULT2");
                multiplicationStrobes[2] = deviceImplementation.strobeMemory.RegisterByName("STR_CHB_MULT3");

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
