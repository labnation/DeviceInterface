using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.EFuctionalities;
using ECore.DeviceMemories;
using System.IO;
#if IPHONE || ANDROID
#else
using System.Windows.Forms;
#endif


namespace ECore.DeviceImplementations
{
    //this is the main class which fills the EDevice with data specific to the HW implementation.
    //eg: which memories, which registers in these memories, which additional functionalities, the start and stop routines, ...
    public partial class ScopeV2:EDeviceImplementation
    {
        
		public static string DemoStatusText = "";
        private DeviceMemories.ScopeFpgaSettingsMemory  fpgaSettingsMemory;
        private DeviceMemories.ScopeFpgaRom             fpgaRom;
        private DeviceMemories.ScopeStrobeMemory        strobeMemory;
        private DeviceMemories.MAX19506Memory           adcMemory;
        private DeviceMemories.ScopePicRegisterMemory   picMemory;
        
        private float[] calibrationCoefficients = new float[] {0.0042f, -0.0029f, 0.1028f};
        private int yOffset_Midrange0V;
        public float ChannelAYOffsetVoltage { get { return 0; } }
        public float ChannelBYOffsetVoltage { get { return (float)((fpgaSettingsMemory.GetRegister(REG.CHB_YOFFSET_VOLTAGE).InternalValue-yOffset_Midrange0V)) * calibrationCoefficients[1]; } }

		#if ANDROID
		public Android.Content.Res.AssetManager Assets;
		#endif
        
        public ScopeV2(EDevice eDevice) : base(eDevice) 
        {
            //figure out which yOffset value needs to be put in order to set a 0V signal to midrange of the ADC = 128binary
            //FIXME: no clue why this line is here...
            yOffset_Midrange0V = (int)((0 - 128f * calibrationCoefficients[0] - calibrationCoefficients[2]) / calibrationCoefficients[1]);
        }

        #region initializers

        public override void InitializeHardwareInterface()
        {
			#if ANDROID
			hardwareInterface = new HardwareInterfaces.HWInterfacePIC_Xamarin(this);
			#else
			hardwareInterface = new HardwareInterfaces.HWInterfacePIC_LibUSB();

			//check communication by reading PIC FW version
			hardwareInterface.WriteControlBytes(new byte[] {123, 1});
            byte[] response = hardwareInterface.ReadControlBytes(16);
			string resultString = "PIC FW Version readout ("+response.Length.ToString()+" bytes): ";
			foreach (byte b in response)
				resultString += b.ToString()+";";
			Logger.AddEntry(this, LogMessageType.Persistent, resultString);
			#endif
        }

        public override ScopeV2RomManager CreateRomManager()
        {
            return new ScopeV2RomManager(eDevice);
        }

        //master method where all memories, registers etc get defined and linked together
        public override void InitializeMemories()
        {
            //Create memories
            picMemory = new DeviceMemories.ScopePicRegisterMemory(eDevice);
            fpgaSettingsMemory = new DeviceMemories.ScopeFpgaSettingsMemory(eDevice);
            fpgaRom = new DeviceMemories.ScopeFpgaRom(eDevice);
            strobeMemory = new DeviceMemories.ScopeStrobeMemory(eDevice, fpgaSettingsMemory);
            adcMemory = new DeviceMemories.MAX19506Memory(eDevice, fpgaSettingsMemory, strobeMemory, fpgaRom);
            //Add them in order we'd like them in the GUI
            
            memories.Add(fpgaRom);
            memories.Add(fpgaSettingsMemory);
            memories.Add(adcMemory);
            memories.Add(picMemory);
            memories.Add(strobeMemory);
        }

        public override void InitializeFunctionalities()
        { }

        #endregion

        #region start_stop

        override public void Start()
        {
            //raise global reset
            strobeMemory.GetRegister(STR.GLOBAL_RESET).InternalValue = 1;
            strobeMemory.WriteSingle(STR.GLOBAL_RESET);

            //flush any transfers still queued on PIC
            //eDevice.HWInterface.FlushHW();

            fpgaSettingsMemory.GetRegister(REG.RAM_CONFIGURATION).InternalValue = 0;
            fpgaSettingsMemory.WriteSingle(REG.RAM_CONFIGURATION);

            //set feedback loopand to 1V for demo purpose and enable
            strobeMemory.GetRegister(STR.CHA_DIV1).InternalValue = 1;
            strobeMemory.WriteSingle(STR.CHA_DIV1);

            strobeMemory.GetRegister(STR.CHB_DIV1).InternalValue = 1;
            strobeMemory.WriteSingle(STR.CHB_DIV1);

            fpgaSettingsMemory.GetRegister(REG.CALIB_VOLTAGE).InternalValue = 78;
            fpgaSettingsMemory.WriteSingle(REG.CALIB_VOLTAGE);

            fpgaSettingsMemory.GetRegister(REG.TRIGGERLEVEL).InternalValue = 130;
            fpgaSettingsMemory.WriteSingle(REG.TRIGGERLEVEL);

            fpgaSettingsMemory.GetRegister(REG.CHA_YOFFSET_VOLTAGE).InternalValue = 100;
            fpgaSettingsMemory.WriteSingle(REG.CHA_YOFFSET_VOLTAGE);

            fpgaSettingsMemory.GetRegister(REG.CHB_YOFFSET_VOLTAGE).InternalValue = 100;// (byte)yOffset_Midrange0V;
            fpgaSettingsMemory.WriteSingle(REG.CHB_YOFFSET_VOLTAGE);

            //fpgaMemory.RegisterByName(REG.TRIGGERHOLDOFF_B1).InternalValue = 4;
            //fpgaMemory.WriteSingle(REG.TRIGGERHOLDOFF_B1);

            //fpgaMemory.RegisterByName(REG.SAMPLECLOCKDIV_B1).InternalValue = 1;
            //fpgaMemory.WriteSingle(REG.SAMPLECLOCKDIV_B1);

            //fpgaMemory.RegisterByName(REG.SAMPLECLOCKDIV_B1).InternalValue = 1;
            //fpgaMemory.WriteSingle(REG.SAMPLECLOCKDIV_B1);

            //set ADC output resistance to 300ohm instead of 50ohm
            adcMemory.GetRegister(MAX19506.CHA_TERMINATION).InternalValue = 4;
            adcMemory.WriteSingle(MAX19506.CHA_TERMINATION);
            adcMemory.GetRegister(MAX19506.CHB_TERMINATION).InternalValue = 4;
            adcMemory.WriteSingle(MAX19506.CHB_TERMINATION);

            //set ADC to offset binary output (required for FPGA triggering)
            adcMemory.GetRegister(MAX19506.FORMAT_PATTERN).InternalValue = 16;
            adcMemory.WriteSingle(MAX19506.FORMAT_PATTERN);

            //output counter by default
            strobeMemory.GetRegister(STR.CHA_DCCOUPLING).InternalValue = 1;
            strobeMemory.WriteSingle(STR.CHA_DCCOUPLING);
            strobeMemory.GetRegister(STR.CHB_DCCOUPLING).InternalValue = 1;
            strobeMemory.WriteSingle(STR.CHB_DCCOUPLING);

            //freerunning as global enable
            strobeMemory.GetRegister(STR.FREE_RUNNING).InternalValue = 1;
            strobeMemory.WriteSingle(STR.FREE_RUNNING);

            //lower global reset
            strobeMemory.GetRegister(STR.GLOBAL_RESET).InternalValue = 0;
            strobeMemory.WriteSingle(STR.GLOBAL_RESET);

            //generate negative voltage
            fpgaSettingsMemory.GetRegister(REG.NEG_DCDC_PWM).InternalValue = 70;
            fpgaSettingsMemory.WriteSingle(REG.NEG_DCDC_PWM);

            strobeMemory.GetRegister(STR.ENABLE_NEG_DCDC).InternalValue = 1;
            strobeMemory.WriteSingle(STR.ENABLE_NEG_DCDC);

            //romMemory.ReadSingle(ROM.FPGA_STATUS);
            //if (romMemory.RegisterByName(ROM.FPGA_STATUS).InternalValue != 3)
            //Logger.AddEntry(this, LogMessageType.ECoreError, "!!! DCMs not locked !!!");
        }

        override public void Stop()
        {
        }

        #endregion

        #region data_handlers

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

        public override float[] ConvertRawDataToVoltages(float[] rawData)
        {
            float[] voltageValues = new float[rawData.Length];

            //this section converts twos complement to a physical voltage value
            float yOffFPGA = (float)fpgaSettingsMemory.GetRegister(REG.CHB_YOFFSET_VOLTAGE).InternalValue;
            float totalOffset = fpgaSettingsMemory.GetRegister(REG.CHB_YOFFSET_VOLTAGE).InternalValue * calibrationCoefficients[1] + calibrationCoefficients[2];
            for (int i = 0; i < rawData.Length; i++)
            {                
                float gainedVal = rawData[i] * calibrationCoefficients[0];
                voltageValues[i] = gainedVal + totalOffset;
            }

            return voltageValues;
        }

        #endregion
    }
}
