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
using System.Threading.Tasks;
using System.Threading;
#if ANDROID
using Android.Content;
#endif

namespace ECore.Devices
{
    public partial class SmartScope : IScope, IDisposable
    {
#if INTERNAL
    public
#else
    private
#endif
        ISmartScopeUsbInterface hardwareInterface;
#if INTERNAL
        public
#else
        private
#endif
        Rom rom;
        private bool flashed = false;
        private bool deviceReady = false;
        private ScopeConnectHandler scopeConnectHandler;

        private List<DeviceMemory> memories = new List<DeviceMemory>();
#if INTERNAL
        public List<DeviceMemory> GetMemories() { return memories; }
#endif

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

        #if ANDROID
        Context context;
        #endif

        private DataSources.DataSource dataSourceScope;
        public DataSources.DataSource DataSourceScope { get { return dataSourceScope; } }

        byte[] chA = null, chB = null;
#if INTERNAL
        byte[] rawBuffer = null;
#endif

        private const double BASE_SAMPLE_PERIOD = 10e-9; //10MHz sample rate
        private const int NUMBER_OF_SAMPLES = 2048;
        private const int BURST_SIZE = 64;
        //FIXME: this should be automatically parsed from VHDL
        private const int INPUT_DECIMATION_MAX_FOR_FREQUENCY_COMPENSATION = 4;
        private const int INPUT_DECIMATION_MIN_FOR_ROLLING_MODE = 14;
        private const int INPUT_DECIMATION_MAX = 21;

        private bool acquiring = false;
        private bool stopPending = false;
        private Dictionary<AnalogChannel, GainCalibration> channelSettings = new Dictionary<AnalogChannel,GainCalibration>();
        private float triggerLevel = 0f;

        //Select through: AnalogChannel, multiplier, subsampling
        private Dictionary<AnalogChannel, Dictionary<double, Dictionary<ushort, Complex[]>>> compensationSpectrum;
#if INTERNAL
        public 
#else
        private
#endif
        FrequencyCompensationCPULoad FrequencyCompensationMode { get; set; }
#if INTERNAL
        public 
#else
        private
#endif
        bool TimeSmoothingEnabled = true;

#if INTERNAL
        public bool DebugDigital { get; set; }
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

        public SmartScope(
#if ANDROID
            Context context,
#endif
            ScopeConnectHandler handler)
            : base()
        {
            #if ANDROID
            this.context = context;
            #endif
            deviceReady = false;
            this.scopeConnectHandler += handler;
            FrequencyCompensationMode = FrequencyCompensationCPULoad.Basic;

            coupling = new Dictionary<AnalogChannel, Coupling>();
            probeSettings = new Dictionary<AnalogChannel, ProbeDivision>();
            foreach (AnalogChannel ch in AnalogChannel.List)
            {
                coupling[ch] = Coupling.DC;
                probeSettings[ch] = ProbeDivision.X1;
            }

            dataSourceScope = new DataSources.DataSource(this);
            InitializeHardwareInterface();
        }

        public void Dispose()
        {
            dataSourceScope.Stop();
            #if ANDROID
            InterfaceManagerXamarin.Instance.onConnect -= OnDeviceConnect;
            #else
            InterfaceManagerLibUsb.Instance.onConnect -= OnDeviceConnect;
            #endif
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
            InterfaceManagerXamarin.context = this.context;
            InterfaceManagerXamarin.Instance.onConnect += OnDeviceConnect;

            InterfaceManagerXamarin.Instance.PollDevice();
#else
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                InterfaceManagerWinUsb.Instance.onConnect += OnDeviceConnect;
                InterfaceManagerWinUsb.Instance.PollDevice();
            }
            else
            { 
                InterfaceManagerLibUsb.Instance.onConnect += OnDeviceConnect;
                InterfaceManagerLibUsb.Instance.PollDevice();
            }
#endif
        }

        private void OnDeviceConnect(ISmartScopeUsbInterface hwInterface, bool connected)
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

                    byte[] response = GetPicFirmwareVersion();
                    if (response == null)
                        throw new Exception("Failed to read from device");
                    Logger.Debug(String.Format("PIC FW Version readout {0}", String.Join(".", response)));

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
                if (scopeConnectHandler != null)
                    scopeConnectHandler(this, connected);
            }
            else
            {
                this.dataSourceScope.Stop();
                if (scopeConnectHandler != null)
                    scopeConnectHandler(this, connected);
                
                stopPending = false;
                acquiring = false;
                deviceReady = false;

                if (this.hardwareInterface == hwInterface)
                {
                    this.hardwareInterface = null;
                    this.flashed = false;
                }
                InitializeMemories();
            }
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
            foreach (AnalogChannel ch in AnalogChannel.List)
            {
                SetVerticalRange(ch, -1f, 1f);
                SetYOffset(ch, 0f);
                SetCoupling(ch, coupling[ch]);
            }

            StrobeMemory[STR.ENABLE_ADC].Set(true);
            StrobeMemory[STR.ENABLE_RAM].Set(true);
            StrobeMemory[STR.ENABLE_NEG].Set(true);

            SetTriggerWidth(2);
            SetTriggerThreshold(2);

            SetAwgStretching(0);
            SetAwgNumberOfSamples(AWG_SAMPLES_MAX);

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
#endif

#if INTERNAL
        public 
#else
        private
#endif
        void Reset()
        {
            this.DataSourceScope.Stop();
            ISmartScopeUsbInterface hwIfTmp = this.hardwareInterface;
            OnDeviceConnect(this.hardwareInterface, false);
            try {
                hwIfTmp.Reset();
            }
            catch (Exception)
            {  
            }
        }

        public void SoftReset()
        {
            dataSourceScope.Reset();
            if(Ready)
                Configure();
        }

        #endregion

        #region data_handlers

        private float[] ConvertByteToVoltage(AnalogChannel ch, double divider, double multiplier, byte[] buffer, byte yOffset, ProbeDivision division)
        {
            double[] coefficients = rom.getCalibration(ch, divider, multiplier).coefficients;
            float[] voltage = new float[buffer.Length];

            //this section converts twos complement to a physical voltage value
            float totalOffset = (float)(yOffset * coefficients[1] + coefficients[2]);
            float gain = 1f;
            if (division == ProbeDivision.X10)
                gain = 10f;

            voltage = buffer.Select(x => (float)(x * coefficients[0] + totalOffset) * gain).ToArray();
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
            SmartScopeHeader header;
            
            try { buffer = hardwareInterface.GetData(BURST_SIZE); }
            catch (ScopeIOException) { return null; }
            if (buffer == null) return null;

            try { header = new SmartScopeHeader(buffer); }
            catch (Exception e)
            {
                Logger.Error("Failed to parse header - resetting scope: " + e.Message);
                Reset();
                return null;
            }

            acquiring = !header.LastAcquisition;
            stopPending = header.ScopeStopPending;

            if (header.NumberOfPayloadBursts == 0)
                return null;

            try { buffer = hardwareInterface.GetData(BURST_SIZE * header.NumberOfPayloadBursts); }
            catch (Exception e)
            {
                Logger.Error("Failed to fetch payload - disconnecting scope: " + e.Message);
                Reset();
                return null;
            }
                
            if (buffer == null)
            {
                Logger.Error("Failed to get payload (got null)");
                Reset();
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
                    //FIXME: this shouldn't be possible
                    if (chA == null)
                        return null;
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
#if INTERNAL
                    rawBuffer = new byte[header.AcquisitionSize * header.BytesPerBurst];
#endif
                }
            }

            for (int i = 0; i < header.Samples; i++)
            {
                chA[dataOffset + i] = buffer[header.Channels * i];
                chB[dataOffset + i] = buffer[header.Channels * i + 1];
#if INTERNAL
                if (rawBuffer != null)
                {
                    for (int j = 0; j < header.Channels; j++)
                    {
                        rawBuffer[(dataOffset + i) * header.Channels + j] = buffer[i * header.Channels + j];
                    }
                }
#endif
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
            //FIXME: Get these scope settings from header
            int triggerIndex = 0;

            //If we're not decimating a lot, fetch on till the package is complete
            if (!header.Rolling && header.SamplesPerAcquisition > chA.Length)
            {
                while (true)
                {
                    DataPackageScope p = GetScopeData();
                    if (p == null)
                    {
                        Logger.Error("While trying to complete acquisition, failed and got null");
                        return null;
                    }
                    if (p.Partial == false)
                        return p;
                }
            }
#if INTERNAL
            if (header.GetStrobe(STR.DEBUG_RAM) && header.GetRegister(REG.ACQUISITION_MULTIPLE_POWER) == 0)
            {
                UInt16[] testData = new UInt16[rawBuffer.Length / 2];
                Buffer.BlockCopy(rawBuffer, 0, testData, 0, sizeof(UInt16) * testData.Length);
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
            if (header.GetStrobe(STR.LA_ENABLE) && header.GetStrobe(STR.DIGI_DEBUG) && !header.GetStrobe(STR.DEBUG_RAM) && DebugDigital)
            {
                //Test if data in CHB is correct
                byte[] testVector = new byte[header.SamplesPerAcquisition];
                byte[] testData = header.GetStrobe(STR.LA_CHANNEL) ? chB : chA;
                byte nextValue = testData[0];
                for (int i = 0; i < testVector.Length; i++)
                {
                    testVector[i] = nextValue;
                    int val = (nextValue >> 4) + 1;
                    nextValue = (byte)((val << 4) + (Utils.ReverseWithLookupTable((byte)val) >> 4));
                    if (testVector[i] != testData[i])
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
            DataPackageScope data = new DataPackageScope(header.SamplePeriod, triggerIndex, chA.Length, 0, chA.Length < header.SamplesPerAcquisition, header.Rolling);
#if INTERNAL
            data.AddSetting("TriggerAddress", header.TriggerAddress);
#endif
            //Parse div_mul
            byte divMul = header.GetRegister(REG.DIVIDER_MULTIPLIER);
            double divA = validDividers[(divMul >> 0) & 0x3];
            double mulA = validMultipliers[(divMul >> 2) & 0x3];
            double divB = validDividers[(divMul >> 4) & 0x3];
            double mulB = validMultipliers[(divMul >> 6) & 0x3];
#if INTERNAL
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
#endif
            byte subSamplingBase10Power = header.GetRegister(REG.INPUT_DECIMATION);

            bool performFrequencyCompensation = header.GetRegister(REG.INPUT_DECIMATION) <= INPUT_DECIMATION_MAX_FOR_FREQUENCY_COMPENSATION;
                bool logicAnalyserOnChannelA = header.GetStrobe(STR.LA_ENABLE) && !header.GetStrobe(STR.LA_CHANNEL);
                bool logicAnalyserOnChannelB = header.GetStrobe(STR.LA_ENABLE) && header.GetStrobe(STR.LA_CHANNEL);

                if (logicAnalyserOnChannelA)
                {
                    data.SetDataDigital(chA);
                }
                else
                {
                    float[] ChAConverted = ConvertByteToVoltage(AnalogChannel.ChA, divA, mulA, chA, header.GetRegister(REG.CHA_YOFFSET_VOLTAGE), probeSettings[AnalogChannel.ChA]);

                    if (TimeSmoothingEnabled)
                        ChAConverted = ECore.FrequencyCompensation.TimeDomainSmoothing(ChAConverted, chA);

                    if (performFrequencyCompensation)
                        ChAConverted = ECore.FrequencyCompensation.Compensate(this.compensationSpectrum[AnalogChannel.ChA][mulA][subSamplingBase10Power], ChAConverted, FrequencyCompensationMode);

                    data.SetData(AnalogChannel.ChA, ChAConverted);
                    //FIXME: this is because the frequency compensation changes the data length
                    data.Samples = ChAConverted.Length;
                }

                if (logicAnalyserOnChannelB)
                {
                    data.SetDataDigital(chB);
                }
                else
                {
                    float[] ChBConverted = ConvertByteToVoltage(AnalogChannel.ChB, divB, mulB, chB, header.GetRegister(REG.CHB_YOFFSET_VOLTAGE), probeSettings[AnalogChannel.ChB]);
                    
                    if (TimeSmoothingEnabled)
                        ChBConverted = ECore.FrequencyCompensation.TimeDomainSmoothing(ChBConverted, chB);

                    if (performFrequencyCompensation)
                        ChBConverted = ECore.FrequencyCompensation.Compensate(this.compensationSpectrum[AnalogChannel.ChB][mulB][subSamplingBase10Power], ChBConverted, FrequencyCompensationMode);

                    data.SetData(AnalogChannel.ChB, ChBConverted);
                    data.Samples = ChBConverted.Length;
                }
#if INTERNAL                    
            }
#endif
            return data;
        }

        //FIXME: this needs proper handling
        private bool Connected { get { return this.hardwareInterface != null && this.flashed; } }
        public bool Ready { get { return this.Connected && this.deviceReady; } }

        #endregion
    }
}
