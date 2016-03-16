using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.Memories;
using System.IO;
using LabNation.DeviceInterface.DataSources;
using LabNation.DeviceInterface.Hardware;
using LabNation.Common;
using AForge.Math;
using System.Threading.Tasks;
using System.Threading;
#if ANDROID
using Android.Content;
#endif

namespace LabNation.DeviceInterface.Devices
{
    public partial class SmartScope : IScope, IWaveGenerator, IDisposable
    {
#if DEBUG
        public
#else
        private
#endif
        ISmartScopeUsbInterface hardwareInterface;
#if DEBUG
        public
#else
        private
#endif
        Rom rom;
        private bool flashed = false;
        private bool deviceReady = false;
        public bool SuspendViewportUpdates { get; set; }
        public event AcquisitionTransferFinishedHandler OnAcquisitionTransferFinished;

        private List<DeviceMemory> memories = new List<DeviceMemory>();
#if DEBUG
        public List<DeviceMemory> GetMemories() { return memories; }
#endif

#if DEBUG 
        public Memories.ScopeFpgaSettingsMemory FpgaSettingsMemory { get; private set; }
        public Memories.ScopeFpgaRom FpgaRom { get; private set; }
        public Memories.ScopeStrobeMemory StrobeMemory { get; private set; }
        public Memories.MAX19506Memory AdcMemory { get; private set; }
        public Memories.ScopePicRegisterMemory PicMemory { get; private set; }
#else
        private Memories.ScopeFpgaSettingsMemory FpgaSettingsMemory;
        private Memories.ScopeFpgaRom FpgaRom;
        private Memories.ScopeStrobeMemory StrobeMemory;
        private Memories.MAX19506Memory AdcMemory;
        private Memories.ScopePicRegisterMemory PicMemory;
#endif

        #if ANDROID
        Context context;
        #endif

        private DataSources.DataSource dataSourceScope;
        public DataSources.DataSource DataSourceScope { get { return dataSourceScope; } }

        DataPackageScope currentDataPackage;
        bool _discardPreviousAcquisition = true;
        object discardPreviousAcquisitionLock = new object();
        bool DiscardPreviousAcquisition
        {
            get { return _discardPreviousAcquisition; }
            set
            {
                lock (discardPreviousAcquisitionLock)
                {
                    _discardPreviousAcquisition = value;
                }
            }
        }
        
        internal static double BASE_SAMPLE_PERIOD = 10e-9; //10MHz sample rate
        private const int OVERVIEW_BUFFER_SIZE = 2048;
        private const int ACQUISITION_DEPTH_MIN = 128; //Size of RAM
        private const int ACQUISITION_DEPTH_MAX = 4 * 1024 * 1024; //Size of RAM
        private const int ACQUISITION_DEPTH_DEFAULT = 512 * 1024;
        private uint acquisitionDepthUserMaximum = ACQUISITION_DEPTH_DEFAULT;
        public uint AcquisitionDepthUserMaximum
        {
            get
            {
                return acquisitionDepthUserMaximum;
            }
            set
            {
                if (value > ACQUISITION_DEPTH_MAX)
                    acquisitionDepthUserMaximum = ACQUISITION_DEPTH_MAX;
                else if (value < ACQUISITION_DEPTH_MIN)
                    acquisitionDepthUserMaximum = ACQUISITION_DEPTH_MIN;
                else
                    acquisitionDepthUserMaximum = value;

                VIEW_DECIMATION_MAX = (int)Math.Log(acquisitionDepthUserMaximum / OVERVIEW_BUFFER_SIZE, 2);
            }
        }
        private const int BYTES_PER_BURST = 64;
        private const int BYTES_PER_SAMPLE = 2;
        private const int SAMPLES_PER_BURST = BYTES_PER_BURST / BYTES_PER_SAMPLE; //one byte per channel
        private const int MAX_COMPLETION_TRIES = 1;
        //FIXME: this should be automatically parsed from VHDL
        internal static int INPUT_DECIMATION_MAX_FOR_FREQUENCY_COMPENSATION = 4;
        private const int INPUT_DECIMATION_MIN_FOR_ROLLING_MODE = 7;
        internal const int INPUT_DECIMATION_MAX = 9;
        private static int VIEW_DECIMATION_MAX = (int)Math.Log(ACQUISITION_DEPTH_DEFAULT / OVERVIEW_BUFFER_SIZE, 2);
        private const int BURSTS_MAX = 64;
        List<byte> adcTimingValues = new List<byte>() { 0, 1, 2, 3, 5, 6, 7 };
        private byte AdcTimingValue { 
            get { return adcTimingValues.Contains(this.rom.AdcTimingValue) ? this.rom.AdcTimingValue : adcTimingValues[0]; }
            set { this.rom.AdcTimingValue = value; }
        }

        private bool acquiring = false;
        private bool stopPending = false;
        private bool awaitingTrigger = false;
        private bool armed = false;
        private bool paused = false;
        private bool acquiringWhenPaused = false;

        private Dictionary<AnalogChannel, GainCalibration> channelSettings = new Dictionary<AnalogChannel,GainCalibration>();
        private TriggerValue triggerValue = new TriggerValue
        {
            source = TriggerSource.Channel,
            channel = AnalogChannel.ChA,
            edge = TriggerEdge.RISING,
            mode = TriggerMode.Edge,
            level = 0.0f,
        };

#if DEBUG
        public bool DebugDigital { get; set; }
#endif

        public string Serial
        {
            get
            {
                if (hardwareInterface == null)
                    return null;
                return hardwareInterface.Serial;
            }
        }

        internal SmartScope(ISmartScopeUsbInterface usbInterface) : base()
        {
            this.hardwareInterface = usbInterface;
            this.SuspendViewportUpdates = false;
            DataOutOfRange = false;
            deviceReady = false;

            foreach (DigitalChannel d in DigitalChannel.List)
                this.triggerDigital[d] = DigitalTriggerValue.X;

            probeSettings = new Dictionary<AnalogChannel, ProbeDivision>();
            yOffset = new Dictionary<AnalogChannel, float>();
            verticalRanges = new Dictionary<AnalogChannel, Range>();
            foreach (AnalogChannel ch in AnalogChannel.List)
            {
                probeSettings[ch] = ProbeDivision.X1;
                yOffset[ch] = 0f;
            }

            dataSourceScope = new DataSources.DataSource(this);
            InitializeHardware();
        }

        public void Pause() 
        {
            //Pause fetch thread
            this.DataSourceScope.Pause();

            DeconfigureAdc();
            EnableEssentials(false);
            CommitSettings();
            hardwareInterface.FlushDataPipe();
            paused = true;
            acquiringWhenPaused = this.acquiring;
        }

        public void Resume() 
        {
            if(!paused) {
                Logger.Warn("Not resuming scope since it wasn't paused");
            }
            paused = false;
            Logger.Debug("Resuming SmartScope");

            EnableEssentials(true);
            ConfigureAdc();
            CommitSettings();
            this.DataSourceScope.Resume();
            this.Running = this.acquiringWhenPaused;
        }

        public void Dispose()
        {
            dataSourceScope.Stop();
            try
            {
                Deconfigure();
            }
            catch { }
            DestroyHardware();
        }

        #region initializers

        private void InitializeHardware()
        {
            InitializeMemories();
            try
            {
                //FIXME: I have to do this synchronously here because there's no blocking on the USB traffic
                //but there should be when flashing the FPGA.

                byte[] response = GetPicFirmwareVersion();
                if (response == null)
                    throw new Exception("Failed to read from device");
                Logger.Debug(String.Format("PIC FW Version readout {0}", String.Join(".", response)));

                //Init ROM
                this.rom = new Rom(hardwareInterface);

                //Init FPGA
                LogWait("Starting fpga flashing...", 0);
                if (!FlashFpga())
                    throw new ScopeIOException("failed to flash FPGA");
                LogWait("FPGA flashed...");
                InitializeMemories();
                LogWait("Memories initialized...");
                Logger.Debug("FPGA ROM MSB:LSB = " + FpgaRom[ROM.FW_MSB].Read().GetByte() + ":" + FpgaRom[ROM.FW_LSB].Read().GetByte());

                Logger.Debug(String.Format("FPGA FW version = 0x{0:x}", GetFpgaFirmwareVersion()));

                Configure();
                deviceReady = true;
            }
            catch (ScopeIOException e)
            {
                Logger.Error("Failure while connecting to device: " + e.Message);
                this.hardwareInterface = null;
                this.flashed = false;
                InitializeMemories();
                throw e;
            }
        }

        private void DestroyHardware() 
        {
            this.dataSourceScope.Stop();
                
            stopPending = false;
            acquiring = false;
            deviceReady = false;

            this.hardwareInterface = null;
            this.flashed = false;
        }

        //master method where all memories, registers etc get defined and linked together
        private void InitializeMemories()
        {
            memories.Clear();
            //Create memories
            PicMemory = new Memories.ScopePicRegisterMemory(hardwareInterface);
            FpgaSettingsMemory = new Memories.ScopeFpgaSettingsMemory(hardwareInterface);
            FpgaRom = new Memories.ScopeFpgaRom(hardwareInterface);
            StrobeMemory = new Memories.ScopeStrobeMemory(FpgaSettingsMemory, FpgaRom);
            AdcMemory = new Memories.MAX19506Memory(FpgaSettingsMemory, StrobeMemory, FpgaRom);
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

        private void ConfigureAdc()
        {
            AdcMemory[MAX19506.SOFT_RESET].WriteImmediate(90);
            AdcMemory[MAX19506.POWER_MANAGEMENT].Set(4);
            AdcMemory[MAX19506.OUTPUT_PWR_MNGMNT].Set(1);
            AdcMemory[MAX19506.FORMAT_PATTERN].Set(16);
            AdcMemory[MAX19506.DATA_CLK_TIMING].Set(AdcTimingValue);
            AdcMemory[MAX19506.CHA_TERMINATION].Set(18);
            AdcMemory[MAX19506.POWER_MANAGEMENT].Set(3);
            AdcMemory[MAX19506.OUTPUT_FORMAT].Set(0x02); //DDR on chA
        }

        /// <summary>
        /// Calibrate ADC timing
        /// </summary>
        /// <returns>Throws when fails</returns>
        private void CalibrateAdc()
        {
            ConfigureAdc();
            AdcMemory[MAX19506.FORMAT_PATTERN].Set(80);
            AcquisitionDepth = 64 * 1024;
            SetViewPort(0, AcquisitionLength);
            AcquisitionMode = Devices.AcquisitionMode.SINGLE;
            SendOverviewBuffer = false;
            PreferPartial = false;
            SetTriggerByte(127);
            //Disable Logic Analyser
            ChannelSacrificedForLogicAnalyser = null;
            Running = true;
            Logger.Info("Calibrating ADC timing");
            CommitSettings();

            //If the adc timing value is not the default (being 0, the first one in the list)
            // it means it was read from ROM. Try working with that value first.
            if (AdcTimingValue != adcTimingValues[0])
            {
                if (TestAdcRamp())
                {
                    Logger.Info("ADC calibration OK with value from ROM = " + AdcTimingValue);
                    return;
                }
            }

            foreach(byte timingValue in adcTimingValues)
            {
                Logger.Info("Testing ADC timing value [" + timingValue + "]");
                AdcMemory[MAX19506.DATA_CLK_TIMING].Set(timingValue);
                CommitSettings();
                //Note: ForceTrigger won't work here yet since Ready is still false
                if (TestAdcRamp())
                {
                    Logger.Info("ADC calibration OK with value " + timingValue);
                    AdcTimingValue = timingValue;
                    return;
                }
            }

            throw new ScopeIOException("failed to calibrate ADC");
        }

        private bool TestAdcRamp()
        {
            int triesLeft = 20;
            while (triesLeft >= 0)
            {
                DataPackageScope p = GetScopeData();

                if (p == null)
                {
                    StrobeMemory[STR.ACQ_START].WriteImmediate(true);
                    StrobeMemory[STR.FORCE_TRIGGER].WriteImmediate(true);
                    triesLeft--;
                    continue;
                }

                if (p.FullAcquisitionFetchProgress < 1f)
                    continue;

                if (p != null && (p.GetData(ChannelDataSourceScope.Acquisition, AnalogChannel.ChA.Raw())) != null)
                {
                    bool allGood = true;
                    foreach (AnalogChannelRaw ch in AnalogChannelRaw.List)
                    {
                        ChannelData d = p.GetData(ChannelDataSourceScope.Acquisition, ch);
                        bool verified = LabNation.Common.Utils.VerifyRamp((byte[])d.array);
                        allGood &= verified;
                    }
                    return allGood;
                }
            }
            Logger.Error("Failed to get ADC calibration data");
            return false;
        }

        private void DeconfigureAdc()
        {
            AdcMemory[MAX19506.POWER_MANAGEMENT].Set(0);
        }

        private void EnableEssentials(bool enable)
        {
            StrobeMemory[STR.ENABLE_ADC].Set(enable);
            StrobeMemory[STR.ENABLE_RAM].Set(enable);
            StrobeMemory[STR.ENABLE_NEG].Set(enable);
            StrobeMemory[STR.SCOPE_ENABLE].Set(enable);
        }

        private void Configure()
        {
            EnableEssentials(true);
            
            //Enable scope controller
            SendOverviewBuffer = false;
            foreach (AnalogChannel ch in AnalogChannel.List)
            {
                SetVerticalRange(ch, -1f, 1f);
                SetCoupling(ch, Coupling.DC);
            }

            DigitalOutput = 0;

            GeneratorStretching = 0;
            SetViewPort(0, 10e-3);
            GeneratorNumberOfSamples = AWG_SAMPLES_MAX;

            //Part 2: perform actual writes                
            StrobeMemory[STR.GLOBAL_RESET].WriteImmediate(true);
            CommitSettings();
            hardwareInterface.FlushDataPipe();

            CalibrateAdc();
            
            Logger.Info("Found good ADC timing value [" + AdcTimingValue + "]");
            AcquisitionDepth = ACQUISITION_DEPTH_DEFAULT;
            CommitSettings();

            //Reconfigure ADC with new timing value
            ConfigureAdc();
        }

        private void Deconfigure()
        {
            DeconfigureAdc();
            StrobeMemory[STR.GLOBAL_RESET].WriteImmediate(true);
            //FIXME: reset FPGA
            hardwareInterface.FlushDataPipe();
        }


#if DEBUG
        public void LoadBootLoader()
        {
            this.DataSourceScope.Stop();
            this.hardwareInterface.LoadBootLoader();
        }
#endif

#if DEBUG
        public 
#else
        private
#endif
        void Reset()
        {
            this.DataSourceScope.Stop();
            try {
                this.hardwareInterface.Reset();
            }
            catch (Exception)
            {
            	Logger.Warn("Reset incomplete - destroying hardware interface");
            	if(hardwareInterface != null)
            		hardwareInterface.Destroy();
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

#if WINDOWS
        SmartScopeHeader ResyncHeader()
        {
            int tries = 64;
            Logger.Warn("Trying to resync header by fetching up to " + tries + " packages");
         
            List<byte[]> crashBuffers = new List<byte[]>();
            byte[] buf;
            while ((buf = hardwareInterface.GetData(BYTES_PER_BURST)) != null && tries > 0)
            {
                if (buf[0] == 'L' && buf[1] == 'N')
                {
                    Logger.Warn("Got " + crashBuffers.Count + " packages before another header came");
                    SmartScopeHeader h = new SmartScopeHeader(buf);
                    return h;
                }
                crashBuffers.Add(buf);
                tries--;
            }
            return null;
        }
#endif

        public bool SendOverviewBuffer
        {
            get { return StrobeMemory[STR.VIEW_SEND_OVERVIEW].GetBool(); }
            set { StrobeMemory[STR.VIEW_SEND_OVERVIEW].Set(value); }
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
            
			try {
				buffer = hardwareInterface.GetData (BYTES_PER_BURST);
			} catch (ScopeIOException) {
				return null;
			} catch (Exception e) {
				Logger.Error ("Error while trying to get scope data: " + e.Message);
				return null;
			}
			if (buffer == null)
				return null;

			try {
				header = new SmartScopeHeader (buffer);
			} catch (Exception e) {
#if WINDOWS
                Logger.Warn("Error parsing header - attempting to fix that");
                header = ResyncHeader();
                if (header == null)
                {
                    Logger.Error("Resync header failed - resetting");
                    Reset();
                    return null;
                }
#else
				Logger.Error ("Failed to parse header - resetting scope: " + e.Message);
				Reset ();
				return null;
#endif
			}

            bool newAcquisition = currentDataPackage == null || currentDataPackage.Identifier != header.AcquisitionId;
            AcquisitionDepthLastPackage = header.AcquisitionDepth;
            SamplePeriodLastPackage = header.SamplePeriod;
			acquiring = header.Acquiring;
			stopPending = header.LastAcquisition;
            awaitingTrigger = header.AwaitingTrigger;
            armed = header.Armed;
            List<AnalogChannel> analogChannels = new List<AnalogChannel>() { AnalogChannel.ChA, AnalogChannel.ChB };
            Dictionary<AnalogChannel, GainCalibration> channelConfig = header.ChannelSettings(this.rom);
            Dictionary<Channel, Array> receivedData;

            //find min and max voltages for each channel, to allow saturation detection
            byte[] minMaxBytes = new byte[] { 0, 255 };
            Dictionary<Channel, float[]> minMaxVoltages = new Dictionary<Channel, float[]>();
            foreach (AnalogChannel ch in analogChannels)
                minMaxVoltages.Add(ch, minMaxBytes.ConvertByteToVoltage(header.ChannelSettings(this.rom)[ch], header.GetRegister(ch.YOffsetRegister()), probeSettings[ch]));

            if (header.OverviewBuffer)
            {
                buffer = hardwareInterface.GetData(OVERVIEW_BUFFER_SIZE * BYTES_PER_SAMPLE);

                if (newAcquisition)
                {
                    //This should not be possible since the overview is always sent *AFTER* the viewport data,
                    //so the last received package's identifier should match with this one
                    Logger.Warn("Got an overview buffer but no data came in for it before. This is wrong");
                    return null;
                }
                if (buffer == null)
                {
                    //This is also pretty bad
                    Logger.Warn("Failed to get overview buffer payload. This is bad");
                    return null;
                }

                receivedData = SplitAndConvert(buffer, analogChannels, header);
                foreach (Channel ch in receivedData.Keys)
                {
                    currentDataPackage.SetData(ChannelDataSourceScope.Overview, ch, receivedData[ch]);
                    if (ch is AnalogChannel)
                    {
                        currentDataPackage.SaturationLowValue[ch] = minMaxVoltages[ch][0];
                        currentDataPackage.SaturationHighValue[ch] = minMaxVoltages[ch][1];
                    }
                }               

                return currentDataPackage;
            }

            if (header.FullAcquisitionDump)
            {
                buffer = hardwareInterface.GetData(header.Samples * BYTES_PER_SAMPLE);
                if (newAcquisition || buffer == null)
                {
                    Logger.Warn("Got an acquisition buffer but no data came in for it before. This is wrong");
                    return null;
                }

                receivedData = SplitAndConvert(buffer, analogChannels, header);

                foreach (Channel ch in receivedData.Keys)
                {
                    //Here we don't use AddData since we want to assign the whole acqbuf in memory
                    // at once instead of growing it as it comes in.
                    // Need to update datapackage timestamp though!
                    ChannelData target = currentDataPackage.GetData(ChannelDataSourceScope.Acquisition, ch);
                    Array targetArray;
                    if (target == null)
                    {
                        targetArray = Array.CreateInstance(receivedData[ch].GetType().GetElementType(), header.AcquisitionDepth);
                        currentDataPackage.SetData(ChannelDataSourceScope.Acquisition, ch, targetArray);
                        if (ch is AnalogChannel)
                        {
                            currentDataPackage.SaturationLowValue[ch] = minMaxVoltages[ch][0];
                            currentDataPackage.SaturationHighValue[ch] = minMaxVoltages[ch][1];
                        }
                    }
                    else
                    {
                        targetArray = target.array;
                    }
                    Array.ConstrainedCopy(receivedData[ch], 0, targetArray, header.PackageOffset * header.Samples, receivedData[ch].Length);                    
                }

                float fullAcquisitionDumpProgress = (header.PackageOffset + 1) * (float)header.Samples / header.AcquisitionDepth;

                //update FullAcquisitionFetchProgress and fire event when finished
                float previousAcqTransferProgress = currentDataPackage.FullAcquisitionFetchProgress;
                currentDataPackage.FullAcquisitionFetchProgress = fullAcquisitionDumpProgress;
                if (currentDataPackage.FullAcquisitionFetchProgress == 1 && previousAcqTransferProgress < 1)
                {
                    currentDataPackage.UpdateTimestamp();
                    if (OnAcquisitionTransferFinished != null)
                        OnAcquisitionTransferFinished(this, new EventArgs());
                }

                return currentDataPackage;
            }

            if (header.ImpossibleDump)
                return null;

			if (header.NumberOfPayloadBursts == 0 || header.TimedOut)
				return null;

			try {
				buffer = hardwareInterface.GetData (BYTES_PER_BURST * header.NumberOfPayloadBursts);
			} catch (Exception e) {
				Logger.Error ("Failed to fetch payload - resetting scope: " + e.Message);
				Reset ();
				return null;
			}
                
			if (buffer == null) {
				Logger.Error ("Failed to get payload - resetting");
				Reset ();
				return null;
			}

            receivedData = SplitAndConvert(buffer, analogChannels, header);

            if (newAcquisition)
            {
                if (header.PackageOffset != 0)
                {
                    Logger.Warn("Got an off-set package but didn't get any date before");
                    return null;
                }
                /* FIXME: integrate into header*/
                AnalogChannel triggerChannel = header.TriggerValue.channel;
                byte[] triggerLevel = new byte[] { header.GetRegister(REG.TRIGGER_LEVEL) };
                float[] triggerLevelFloat = triggerLevel.ConvertByteToVoltage(header.ChannelSettings(this.rom)[triggerChannel], header.GetRegister(triggerChannel.YOffsetRegister()), probeSettings[triggerChannel]);
                header.TriggerValue.level = triggerLevelFloat[0];
                currentDataPackage = new DataPackageScope(this.GetType(),
                    header.AcquisitionDepth, header.SamplePeriod,
                    header.ViewportLength, header.ViewportOffsetSamples,
                    header.TriggerHoldoff, header.TriggerHoldoffSamples, header.Rolling,
                    header.AcquisitionId, header.TriggerValue, header.ViewportExcess);
            }

#if DEBUG
            currentDataPackage.header = header;
            currentDataPackage.Settings["AcquisitionId"] = header.AcquisitionId;
#endif
            currentDataPackage.Settings["InputDecimation"] = header.GetRegister(REG.INPUT_DECIMATION);

            currentDataPackage.offset[ChannelDataSourceScope.Viewport] = header.ViewportOffset;
            currentDataPackage.samplePeriod[ChannelDataSourceScope.Viewport] = header.ViewportSamplePeriod;
            foreach (Channel ch in receivedData.Keys)
            {
                currentDataPackage.AddData(ChannelDataSourceScope.Viewport, ch, receivedData[ch]);
                if (ch is AnalogChannel)
                {
                    currentDataPackage.SaturationLowValue[ch] = minMaxVoltages[ch][0];
                    currentDataPackage.SaturationHighValue[ch] = minMaxVoltages[ch][1];
                }
            }

            foreach (AnalogChannel ch in AnalogChannel.List)
            {
                currentDataPackage.Resolution[ch] = ProbeScaleScopeToHost(ch, (float)channelConfig[ch].coefficients[0]);
                currentDataPackage.Settings["Multiplier" + ch.Name]= channelConfig[ch].multiplier;
#if DEBUG
                currentDataPackage.Settings["Divider" + ch.Name] = channelConfig[ch].divider;
                currentDataPackage.Settings["Offset" + ch.Name] = ConvertYOffsetByteToVoltage(ch, header.GetRegister(ch.YOffsetRegister()));
#endif
            }
            return currentDataPackage;
        }

        private Dictionary<Channel, Array> SplitAndConvert(byte[] buffer, List<AnalogChannel> channels, SmartScopeHeader header)
        {
            int n_channels = channels.Count;
            int n_samples = buffer.Length / n_channels;
            byte[][] splitRaw = new byte[n_channels][];

            for (int j = 0; j < n_channels; j++)
                splitRaw[j] = new byte[n_samples];

            Dictionary<Channel, Array> result = new Dictionary<Channel, Array>();
            for (int i = 0; i < n_samples; i++)
            {
                for (int j = 0; j < n_channels; j++)
                    splitRaw[j][i] = buffer[i * n_channels + j];
            }

            for (int j = 0; j < n_channels; j++)
            {
                AnalogChannel ch = channels[j];
                if (header.GetStrobe(STR.LA_ENABLE) && header.GetStrobe(STR.LA_CHANNEL) == ch.Value > 0)
                {
                    result[LogicAnalyserChannel.LA] = splitRaw[j];
                }
                else
                {
                    result[ch] = splitRaw[j].ConvertByteToVoltage(
                        header.ChannelSettings(this.rom)[ch],
                        header.GetRegister(ch.YOffsetRegister()),
                        probeSettings[ch]);
                }
                result[ch.Raw()] = splitRaw[j];
            }
            return result;
        }

        //FIXME: this needs proper handling
        private bool Connected { get { return this.hardwareInterface != null && !this.hardwareInterface.Destroyed && this.flashed; } }
        public bool Ready { get { return this.Connected && this.deviceReady && !(this.hardwareInterface == null || this.hardwareInterface.Destroyed); } }

        #endregion
    }

    internal static class Helpers
    {
        public static float[] ConvertByteToVoltage(this byte[] buffer, SmartScope.GainCalibration calibration, byte yOffset, ProbeDivision division)
        {
            double[] coefficients = calibration.coefficients;
            float[] voltage = new float[buffer.Length];

            //this section converts twos complement to a physical voltage value
            float totalOffset = (float)(yOffset * coefficients[1] + coefficients[2]);
            float gain = division;

            voltage = buffer.Select(x => (float)(x * coefficients[0] + totalOffset) * gain).ToArray();
            return voltage;
        }
                
        public static REG YOffsetRegister(this AnalogChannel ch)
        {
            return (ch == AnalogChannel.ChA) ? REG.CHA_YOFFSET_VOLTAGE :
                /*(ch == AnalogChannel.ChB) ?*/ REG.CHB_YOFFSET_VOLTAGE;
        }
    }
}
