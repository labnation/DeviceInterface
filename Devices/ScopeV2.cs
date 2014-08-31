using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.DeviceMemories;
using System.IO;
using ECore.DataSources;
using ECore.HardwareInterfaces;
using Common;
using AForge.Math;

namespace ECore.Devices
{
    public partial class ScopeV2 : EDevice, IScope, IDisposable
    {
#if INTERNAL
    public
#else
    private
#endif
        ScopeUsbInterface hardwareInterface;
#if INTERNAL
        public
#else
        private
#endif
        Rom rom;
        private bool flashed = false;
        private bool deviceReady = false;
        private ScopeConnectHandler scopeConnectHandler;

#if INTERNAL 
        public DeviceMemories.ScopeFpgaSettingsMemory FpgaSettingsMemory { get; private set; }
        public DeviceMemories.ScopeFpgaRom FpgaRom { get; private set; }
        public DeviceMemories.ScopeStrobeMemory StrobeMemory { get; private set; }
        public DeviceMemories.MAX19506Memory AdcMemory { get; private set; }
        public DeviceMemories.ScopePicRegisterMemory PicMemory { get; private set; }
#else
        private DeviceMemories.ScopeFpgaSettingsMemory FpgaSettingsMemory;
        private DeviceMemories.ScopeFpgaRom FpgaRom;
        private DeviceMemories.ScopeStrobeMemory StrobeMemory;
        private DeviceMemories.MAX19506Memory AdcMemory;
        private DeviceMemories.ScopePicRegisterMemory PicMemory;
#endif

        private DataSources.DataSourceScope dataSourceScope;
        public DataSources.DataSourceScope DataSourceScope { get { return dataSourceScope; } }

        private bool disableVoltageConversion = false;
        byte[] chA = null, chB = null;

        private const double BASE_SAMPLE_PERIOD = 10e-9; //10MHz sample rate
        private const int NUMBER_OF_SAMPLES = 2048;
        private const int BURST_SIZE = 64;
        private const int INPUT_DECIMATION_MAX_FOR_FREQUENCY_COMPENSATION = 14;

        private bool acquisitionRunning = false;
        private Dictionary<AnalogChannel, GainCalibration> channelSettings = new Dictionary<AnalogChannel,GainCalibration>();
        private float triggerLevel = 0f;

        //Select through: AnalogChannel, multiplier, subsampling
        private Dictionary<AnalogChannel, Dictionary<double, Dictionary<ushort, Complex[]>>> compensationSpectrum;
        public FrequencyCompensationCPULoad FrequencyCompensationMode { get; set; }

#if INTERNAL
        Dictionary<AnalogChannel, float[]> debugSignal = new Dictionary<AnalogChannel, float[]>();
#endif

        public string Serial
        {
            get
            {
                if (hardwareInterface == null)
                    return null;
                return hardwareInterface.GetSerial();
            }
        }

#if INTERNAL
        public int ramTestPasses, ramTestFails, digitalTestPasses, digitalTestFails;
#endif
#if ANDROID
		public Android.Content.Res.AssetManager Assets;
#endif

        public ScopeV2(ScopeConnectHandler handler)
            : base()
        {
            deviceReady = false;
            this.scopeConnectHandler += handler;
            dataSourceScope = new DataSources.DataSourceScope(this);
            InitializeHardwareInterface();
            FrequencyCompensationMode = FrequencyCompensationCPULoad.Basic;
        }

        public void Dispose()
        {
            dataSourceScope.Stop();
            HWInterfacePIC_LibUSB.RemoveConnectHandler(OnDeviceConnect);
            if (hardwareInterface != null)
                OnDeviceConnect(this.hardwareInterface, false);
        }

        #region initializers

        private void InitializeHardwareInterface()
        {
            //The memory initialisation is done here so that if no device is detected,
            //we'll still have deviceMemory objects to play with
            InitializeMemories();
#if ANDROID
		hardwareInterface = new HardwareInterfaces.HWInterfacePIC_Xamarin(this);
#else
            HWInterfacePIC_LibUSB.AddConnectHandler(OnDeviceConnect);
            HWInterfacePIC_LibUSB.Initialize();
            HWInterfacePIC_LibUSB.PollDevice();
#endif
        }

        private void OnDeviceConnect(ScopeUsbInterface hwInterface, bool connected)
        {
            if (connected)
            {
                try
                {
#if INTERNAL
                    resetTestResults("all");
#endif
                    this.hardwareInterface = hwInterface;
                    //FIXME: I have to do this synchronously here because there's no blocking on the USB traffic
                    //but there should be when flashing the FPGA.

                    hardwareInterface.SendCommand(ScopeUsbInterface.PIC_COMMANDS.PIC_VERSION);
                    byte[] response = hardwareInterface.ReadControlBytes(16);
                    if (response == null)
                        throw new Exception("Failed to read from device");
                    Logger.Debug(String.Format("PIC FW Version readout {0}", String.Join(";", response)));

                    //Init ROM
                    this.rom = new Rom(hardwareInterface);

                    //precalc compensation spectra
                    this.compensationSpectrum = new Dictionary<AnalogChannel,Dictionary<double,Dictionary<ushort,Complex[]>>>() {
                        { AnalogChannel.ChA, new Dictionary<double, Dictionary<ushort,Complex[]>>() },
                        { AnalogChannel.ChB, new Dictionary<double, Dictionary<ushort,Complex[]>>() },
                    };
                    foreach (FrequencyResponse fr in rom.frequencyResponse)
                    {
                        Complex[] artSpectr = FrequencyCompensation.CreateArtificialSpectrum(fr.magnitudes, fr.phases);
                        
                        Dictionary<ushort, Complex[]> subsamplingSpectrum = new Dictionary<ushort, Complex[]>();
                        subsamplingSpectrum.Add(0, artSpectr);
                        for (ushort subsamplingBase10 = 1; subsamplingBase10 < 20; subsamplingBase10++)
                        {
                            //subsample the reconstruction spectrum
                            artSpectr = FrequencyCompensation.SubsampleSpectrum(artSpectr);
                            subsamplingSpectrum.Add(subsamplingBase10, artSpectr);
                        }

                        compensationSpectrum[fr.channel].Add(fr.multiplier, subsamplingSpectrum);
                    }

                    //Init FPGA
                    LogWait("Starting fpga flashing...", 0);
                    if (!FlashFpga())
                        throw new Exception("failed to flash FPGA");
                    LogWait("FPGA flashed...");
                    InitializeMemories();
                    LogWait("Memories initialized...");
                    Logger.Debug("FPGA ROM MSB:LSB = " + FpgaRom[ROM.FW_MSB].Read().GetByte() + ":" + FpgaRom[ROM.FW_LSB].Read().GetByte());

                    Logger.Info(String.Format("FPGA FW version = 0x{0:x}", GetFpgaFirmwareVersion()));

                    Configure();
                    deviceReady = true;
                }
                catch (ScopeIOException e)
                {
                    Logger.Error("Failure while connecting to device: " + e.Message);
                    connected = false;
                    this.hardwareInterface = null;
                    this.flashed = false;
                    InitializeMemories();
                    throw e;
                }
            }
            else
            {
                acquisitionRunning = false;
                deviceReady = false;
                if (this.hardwareInterface == hwInterface)
                {
                    this.hardwareInterface = null;
                    this.flashed = false;
                }
                InitializeMemories();
            }
            if (scopeConnectHandler != null)
                scopeConnectHandler(this, connected);
        }

#if INTERNAL
        public void resetTestResults(string test)
        {
            if (test == "ram" || test == "all")
            {
                ramTestPasses = 0;
                ramTestFails = 0;
            }

            if (test == "digi" || test == "all")
            {
                digitalTestPasses = 0;
                digitalTestFails = 0;
            }
        }
#endif

        //master method where all memories, registers etc get defined and linked together
        private void InitializeMemories()
        {
            memories.Clear();
            //Create memories
            PicMemory = new DeviceMemories.ScopePicRegisterMemory(hardwareInterface);
            FpgaSettingsMemory = new DeviceMemories.ScopeFpgaSettingsMemory(hardwareInterface);
            FpgaRom = new DeviceMemories.ScopeFpgaRom(hardwareInterface);
            StrobeMemory = new DeviceMemories.ScopeStrobeMemory(FpgaSettingsMemory, FpgaRom);
            AdcMemory = new DeviceMemories.MAX19506Memory(FpgaSettingsMemory, StrobeMemory, FpgaRom);
            //Add them in order we'd like them in the GUI
            memories.Add(PicMemory);
            memories.Add(FpgaRom);
            memories.Add(StrobeMemory);
            memories.Add(FpgaSettingsMemory);
            memories.Add(AdcMemory);
            
        }

        #endregion

        #region start_stop

		private void LogWait(string message, int sleep = 0)
        {
            Logger.Debug(message);
			System.Threading.Thread.Sleep(sleep);
        }

        private void Configure()
        {
            //Part 1: Just set all desired memory settings

            /*********
             *  ADC  *
            *********/
            AdcMemory[MAX19506.POWER_MANAGEMENT].Set(4);
            AdcMemory[MAX19506.OUTPUT_PWR_MNGMNT].Set(1);
            AdcMemory[MAX19506.FORMAT_PATTERN].Set(16);
            AdcMemory[MAX19506.CHA_TERMINATION].Set(18);
            AdcMemory[MAX19506.DATA_CLK_TIMING].Set(0);
            AdcMemory[MAX19506.POWER_MANAGEMENT].Set(3);
            AdcMemory[MAX19506.OUTPUT_FORMAT].Set(0x02); //DDR on chA

            /***************************/

            //Enable scope controller
            StrobeMemory[STR.SCOPE_ENABLE].Set(true);
            foreach(AnalogChannel ch in AnalogChannel.listPhysical)
            {
                SetVerticalRange(ch, -1f, 1f);
                SetYOffset(ch, 0f);
                SetCoupling(ch, Coupling.DC);
            }

            StrobeMemory[STR.ENABLE_ADC].Set(true);
            StrobeMemory[STR.ENABLE_RAM].Set(true);
            StrobeMemory[STR.ENABLE_NEG].Set(true);

            SetTriggerWidth(2);
            SetTriggerThreshold(2);            

            try
            {
                //Part 2: perform actual writes                
                StrobeMemory[STR.GLOBAL_RESET].WriteImmediate(true);
                AdcMemory[MAX19506.SOFT_RESET].WriteImmediate(90);
                CommitSettings();
                hardwareInterface.FlushDataPipe();
            } catch (ScopeIOException e) {
                Logger.Error("Something went wrong while configuring the scope. Try replugging it : " + e.Message);
                OnDeviceConnect(hardwareInterface, false);
            }

        }

#if INTERNAL
        public void LoadBootLoader()
        {
            this.DataSourceScope.Stop();
            this.hardwareInterface.LoadBootLoader();
        }
        public void Reset()
        {
            this.DataSourceScope.Stop();
            try { this.hardwareInterface.Reset(); }
            catch (ScopeIOException)
            { OnDeviceConnect(this.hardwareInterface, false); }
        }
#endif
        public void SoftReset()
        {
            dataSourceScope.Reset();
            if(Ready)
                Configure();
        }

        #endregion

        #region data_handlers

        private float[] ConvertByteToVoltage(AnalogChannel ch, double divider, double multiplier, byte[] buffer, byte yOffset)
        {
            double[] coefficients = rom.getCalibration(ch, divider, multiplier).coefficients;
            float[] voltage = new float[buffer.Length];

            //this section converts twos complement to a physical voltage value
            float totalOffset = (float)(yOffset * coefficients[1] + coefficients[2]);
            voltage = buffer.Select(x => (float)(x * coefficients[0] + totalOffset)).ToArray();
            return voltage;
        }

        /// <summary>
        /// Get a package of scope data
        /// </summary>
        /// <returns>Null in case communication failed, a data package otherwise. Might result in disconnecting the device if a sync error occurs</returns>
        public DataPackageScope GetScopeData()
        {
            if (hardwareInterface == null)
                return null;

            byte[] buffer;
            ScopeV2Header header;
            
            try { buffer = hardwareInterface.GetData(BURST_SIZE); }
            catch (ScopeIOException) { return null; }
            if (buffer == null) return null;

            try { header = new ScopeV2Header(buffer); }
            catch (Exception e)
            {
                Logger.Error("Failed to parse header - disconnecting scope: " + e.Message);
                OnDeviceConnect(this.hardwareInterface, false);
                return null;
            }

            try { buffer = hardwareInterface.GetData(BURST_SIZE * header.NumberOfPayloadBursts); }
            catch (Exception e)
            {
                Logger.Error("Failed to fetch payload - disconnecting scope: " + e.Message);
                OnDeviceConnect(this.hardwareInterface, false);
                return null;
            }
                
            if (buffer == null)
            {
                Logger.Error("Failed to get payload");
                OnDeviceConnect(this.hardwareInterface, false);
                return null;
            }

            int dataOffset;
            if (header.Rolling)
            {
                if (chA == null)
                {
                    chA = new byte[header.Samples];
                    chB = new byte[header.Samples];
                    dataOffset = 0;
                }
                else //blow up the array
                {
                    byte[] chANew = new byte[chA.Length + header.Samples];
                    byte[] chBNew = new byte[chB.Length + header.Samples];
                    chA.CopyTo(chANew, 0);
                    chB.CopyTo(chBNew, 0);
                    dataOffset = chA.Length;
                    chA = chANew;
                    chB = chBNew;
                }
            }
            else
            {
                //If it's part of an acquisition of which we already received
                //samples, add to previously received data
                dataOffset = 0;
                if (header.PackageOffset != 0)
                {
                    byte[] chANew = new byte[chA.Length + header.Samples];
                    byte[] chBNew = new byte[chB.Length + header.Samples];
                    chA.CopyTo(chANew, 0);
                    chB.CopyTo(chBNew, 0);
                    chA = chANew;
                    chB = chBNew;
                    dataOffset = BURST_SIZE * header.PackageOffset / header.Channels;
                }
                else //New acquisition, new buffers
                {
                    chA = new byte[header.Samples];
                    chB = new byte[header.Samples];
                }
            }

            for (int i = 0; i < header.Samples; i++)
            {
                chA[dataOffset + i] = buffer[2 * i];
                chB[dataOffset + i] = buffer[2 * i + 1];
            }

            //In rolling mode, crop the channel to the display length
            if (chA.Length > NUMBER_OF_SAMPLES)
            {
                byte[] chANew = new byte[NUMBER_OF_SAMPLES];
                byte[] chBNew = new byte[NUMBER_OF_SAMPLES];
                Array.ConstrainedCopy(chA, chA.Length - NUMBER_OF_SAMPLES, chANew, 0, NUMBER_OF_SAMPLES);
                Array.ConstrainedCopy(chB, chB.Length - NUMBER_OF_SAMPLES, chBNew, 0, NUMBER_OF_SAMPLES);
                chA = chANew;
                chB = chBNew;
            }
            acquisitionRunning = header.ScopeRunning;
            //FIXME: Get these scope settings from header
            int triggerIndex = 0;

            //If we're not decimating a lot don't return partial package
            if (
                header.GetRegister(REG.INPUT_DECIMATION) <= INPUT_DECIMATION_MAX_FOR_FREQUENCY_COMPENSATION 
            && 
                header.PackageSize * BURST_SIZE / header.Channels > chA.Length
            )
              return null;
#if INTERNAL
            if (header.GetStrobe(STR.DEBUG_RAM) && header.GetRegister(REG.ACQUISITION_MULTIPLE_POWER) == 0)
            {
                UInt16[] testData = new UInt16[header.Samples];
                Buffer.BlockCopy(buffer, header.BytesPerBurst, testData, 0, sizeof(UInt16) * testData.Length);
                for (int i = 1; i < testData.Length; i++)
                {
                    UInt16 expected = Utils.nextFpgaTestVector(testData[i - 1]);
                    bool mismatch = !expected.Equals(testData[i]);
                    if (mismatch)
                    {
                        Logger.Debug("Stress test mismatch at sample " + i);
                        ramTestFails++;
                        goto ram_test_done;
                    }
                }
                ramTestPasses++;
            }
            ram_test_done:
#endif

            this.coupling[AnalogChannel.ChA] = header.GetStrobe(STR.CHA_DCCOUPLING) ? Coupling.DC : Coupling.AC;
            this.coupling[AnalogChannel.ChB] = header.GetStrobe(STR.CHB_DCCOUPLING) ? Coupling.DC : Coupling.AC;

#if INTERNAL
            if (header.GetStrobe(STR.LA_ENABLE) && header.GetStrobe(STR.DIGI_DEBUG) && !header.GetStrobe(STR.DEBUG_RAM))
            {
                //Test if data in CHB is correct
                byte[] testVector = new byte[chB.Length];
                byte nextValue = chB[0];
                for (int i = 0; i < testVector.Length; i++)
                {
                    testVector[i] = nextValue;
                    int val = (nextValue >> 4) + 1;
                    nextValue = (byte)((val << 4) + (Utils.ReverseWithLookupTable((byte)val) >> 4));
                    if (testVector[i] != chB[i])
                    {
                        Logger.Error("Digital mismatch at sample " + i + ". Aborting check");
                        digitalTestFails++;
                        goto done;
                    }
                }
                digitalTestPasses++;
            }
        done:
#endif

            //construct data package
            //FIXME: get firstsampletime and samples from FPGA
            //FIXME: parse package header and set DataPackageScope's trigger index
            DataPackageScope data = new DataPackageScope(header.SamplePeriod, triggerIndex, chA.Length, 0);
#if INTERNAL
            data.AddSetting("TriggerAddress", header.TriggerAddress);
#endif
            //Parse div_mul
            byte divMul = header.GetRegister(REG.DIVIDER_MULTIPLIER);
            double divA = validDividers[(divMul >> 0) & 0x3];
            double mulA = validMultipliers[(divMul >> 2) & 0x3];
            double divB = validDividers[(divMul >> 4) & 0x3];
            double mulB = validMultipliers[(divMul >> 6) & 0x3];
            data.AddSetting("DividerA", divA);
            data.AddSetting("DividerB", divB);
            data.AddSetting("MultiplierA", mulA);
            data.AddSetting("MultiplierB", mulB);

            if (this.disableVoltageConversion)
            {
                data.SetData(AnalogChannel.ChA, Utils.CastArray<byte, float>(chA));
                data.SetData(AnalogChannel.ChB, Utils.CastArray<byte, float>(chB));
                data.SetDataDigital(chB);
                data.AddSetting("OffsetA", (float)header.GetRegister(REG.CHA_YOFFSET_VOLTAGE));
                data.AddSetting("OffsetB", (float)header.GetRegister(REG.CHB_YOFFSET_VOLTAGE));
            }
            else
            {
                byte subSamplingBase10Power = (byte)FpgaSettingsMemory[REG.INPUT_DECIMATION].Get();

                float[] ChAConverted = ConvertByteToVoltage(AnalogChannel.ChA, divA, mulA, chA, header.GetRegister(REG.CHA_YOFFSET_VOLTAGE));
                bool performFrequencyCompensation = header.GetRegister(REG.INPUT_DECIMATION) <= INPUT_DECIMATION_MAX_FOR_FREQUENCY_COMPENSATION && !header.Rolling;
                
                if(performFrequencyCompensation)
                    ChAConverted = ECore.FrequencyCompensation.Compensate(this.compensationSpectrum[AnalogChannel.ChA][mulA][subSamplingBase10Power], ChAConverted, FrequencyCompensationMode);

                data.SetData(AnalogChannel.ChA, ChAConverted);
                //FIXME: this is because the frequency compensation changes the data length
                data.Samples = ChAConverted.Length;

                //Check if we're in LA mode and fill either analog channel B or digital channels
                if (!header.GetStrobe(STR.LA_ENABLE))
                {
                    float[] ChBConverted = ConvertByteToVoltage(AnalogChannel.ChB, divB, mulB, chB, header.GetRegister(REG.CHB_YOFFSET_VOLTAGE));
                    if (performFrequencyCompensation)
                        ChBConverted = ECore.FrequencyCompensation.Compensate(this.compensationSpectrum[AnalogChannel.ChB][mulB][subSamplingBase10Power], ChBConverted, FrequencyCompensationMode);

                    data.SetData(AnalogChannel.ChB, ChBConverted);
                }
                else
                    data.SetDataDigital(chB);
            }
            return data;
        }

        //FIXME: this needs proper handling
        private bool Connected { get { return this.hardwareInterface != null && this.flashed; } }
        public override bool Ready { get { return this.Connected && this.deviceReady; } }

        #endregion
    }
}
