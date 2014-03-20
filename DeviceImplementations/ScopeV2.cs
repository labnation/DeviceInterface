using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            
            byteMemories.Add(fpgaRom);
            byteMemories.Add(fpgaSettingsMemory);
            byteMemories.Add(adcMemory);
            byteMemories.Add(picMemory);
            byteMemories.Add(strobeMemory);
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
            this.SetDivider(0, 1);
            this.SetDivider(1, 1);

            fpgaSettingsMemory.GetRegister(REG.CALIB_VOLTAGE).InternalValue = 78;
            fpgaSettingsMemory.WriteSingle(REG.CALIB_VOLTAGE);

            //FIXME: use this instead of code below
            //this.SetTriggerLevel(0f);
            fpgaSettingsMemory.GetRegister(REG.TRIGGERLEVEL).InternalValue = 130;
            fpgaSettingsMemory.WriteSingle(REG.TRIGGERLEVEL);

            //FIXME: these are byte values, since the setter helper is not converting volt to byte
            this.SetYOffset(0, 100f);
            this.SetYOffset(1, 100f);

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

            this.SetEnableDcCoupling(0, true);
            this.SetEnableDcCoupling(1, true);

            this.SetEnableFreeRunning(true);

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

        public override byte[] GetBytes()
        {
            int samplesToFetch = 4096;
            int bytesToFetch = samplesToFetch;
            return eDevice.HWInterface.GetData(bytesToFetch);          
        }

        public override float[] ConvertBytesToVoltages(byte[] buffer)
        {
            float[] voltageValues = new float[buffer.Length];

            //this section converts twos complement to a physical voltage value
            float yOffFPGA = (float)fpgaSettingsMemory.GetRegister(REG.CHB_YOFFSET_VOLTAGE).InternalValue;
            float totalOffset = fpgaSettingsMemory.GetRegister(REG.CHB_YOFFSET_VOLTAGE).InternalValue * calibrationCoefficients[1] + calibrationCoefficients[2];
            for (int i = 0; i < buffer.Length; i++)
            {                
                float gainedVal = (float)buffer[i] * calibrationCoefficients[0];
                voltageValues[i] = gainedVal + totalOffset;
            }

            return voltageValues;
        }

        #endregion
    }
}
