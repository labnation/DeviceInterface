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
using System.Runtime.InteropServices;
#if ANDROID
using Android.Content;
#endif

namespace LabNation.DeviceInterface.Devices
{
    public partial class SmartScope : IScope, IWaveGenerator, IDisposable
    {
        public IHardwareInterface HardwareInterface { get { return hardwareInterface; } }
        private ISmartScopeInterface hardwareInterface;
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

        private const byte FPGA_I2C_ADDRESS_SETTINGS = 0x0C;
        private const byte FPGA_I2C_ADDRESS_ROM = 0x0D;

        public Memories.ByteMemoryEnum<REG> FpgaSettingsMemory { get; private set; }
        public Memories.ScopeFpgaRom FpgaRom { get; private set; }
        public Memories.ScopeStrobeMemory StrobeMemory { get; private set; }
        public Memories.MAX19506Memory AdcMemory { get; private set; }
        public Memories.ScopePicRegisterMemory PicMemory { get; private set; }

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
                if (value > Constants.ACQUISITION_DEPTH_MAX)
                    acquisitionDepthUserMaximum = Constants.ACQUISITION_DEPTH_MAX;
                else if (value < Constants.ACQUISITION_DEPTH_MIN)
                    acquisitionDepthUserMaximum = Constants.ACQUISITION_DEPTH_MIN;
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
                if (HardwareInterface == null)
                    return null;
                return HardwareInterface.Serial;
            }
        }

        public SmartScope(ISmartScopeInterface hwInterface) : base()
        {
            this.hardwareInterface = hwInterface;
            this.SuspendViewportUpdates = false;
            DataOutOfRange = false;
            deviceReady = false;

            foreach (DigitalChannel d in DigitalChannel.List)
                this.triggerDigital[d] = DigitalTriggerValue.X;

            probeSettings = new Dictionary<AnalogChannel, Probe>();
            yOffset = new Dictionary<AnalogChannel, float>();
            verticalRanges = new Dictionary<AnalogChannel, Range>();
            foreach (AnalogChannel ch in AnalogChannel.List)
            {
                probeSettings[ch] = Probe.DefaultX1Probe;
                yOffset[ch] = 0f;
            }

            dataSourceScope = new DataSources.DataSource(this);
            try {
                InitializeHardware();
            } catch(Exception e)
            {
                Logger.Error("Failed to initialize hardware, resetting scope: " + e.Message);
                hardwareInterface.Reset();
                throw e;
            }
            
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

        private static uint FPGA_VERSION_UNFLASHED = 0xffffffff;
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
                
                this.flashed = false;
                string fwName;
                byte[] firmware = null;
                
                //Get FW contents
                try
                {
                    LabNation.Common.SerialNumber s = new SerialNumber(this.Serial);
	    			fwName = String.Format("blobs.SmartScope_{0}.bin", Base36.Encode((long)s.model, 3).ToUpper());
                    firmware = Resources.Load(fwName);
                }
                catch (Exception e)
                {
                    throw new ScopeIOException("Opening FPGA FW file failed\n" + e.Message);
                }
                if (firmware == null)
                    throw new ScopeIOException("Failed to read FW");

                Logger.Info("Got firmware of length " + firmware.Length);
                if (!this.hardwareInterface.FlashFpga(firmware))
                    throw new ScopeIOException("failed to flash FPGA");
                if (GetFpgaFirmwareVersion() == FPGA_VERSION_UNFLASHED)
                    throw new ScopeIOException("Got firmware version of unflashed FPGA");
                LogWait("FPGA flashed...");
                this.flashed = true;

                InitializeMemories();
                LogWait("Memories initialized...");

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
            FpgaSettingsMemory = new Memories.ByteMemoryEnum<REG>(new ScopeFpgaI2cMemory(hardwareInterface, FPGA_I2C_ADDRESS_SETTINGS));
            FpgaRom = new Memories.ScopeFpgaRom(hardwareInterface, FPGA_I2C_ADDRESS_ROM);
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
            AdcMemory[MAX19506.OUTPUT_PWR_MNGMNT].Set(0);
            AdcMemory[MAX19506.FORMAT_PATTERN].Set(16);
            AdcMemory[MAX19506.DATA_CLK_TIMING].Set(24);
            AdcMemory[MAX19506.CHA_TERMINATION].Set(0);
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
            AcquisitionDepth = 4 * 1024;
            SetViewPort(0, AcquisitionLength);
            AcquisitionMode = Devices.AcquisitionMode.SINGLE;
            SendOverviewBuffer = false;
            PreferPartial = false;
            SetTriggerByte(127);
            //Disable Logic Analyser
            ChannelSacrificedForLogicAnalyser = null;

            bool runningBeforeTest = Running;
            Running = true;
            Logger.Info("Calibrating ADC timing");
            CommitSettings();
            if (TestAdcRamp())
            {
                Logger.Info("ADC calibration OK with value from ROM = " + AdcTimingValue);

                //Get the overview, so the scope doesn't want to send this afterwards
                SendOverviewBuffer = true;

                if (!runningBeforeTest)
                    Running = false;
                CommitSettings();
                StrobeMemory[STR.FORCE_TRIGGER].WriteImmediate(true);
                DataPackageScope p = GetScopeData();
                return;
            }
#if !DEBUG
            throw new ScopeIOException("failed to calibrate ADC");
#endif
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
                    if (!allGood)
                    {
                        triesLeft--;
                        continue;
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
            if (!(this.hardwareInterface is SmartScopeInterfaceUsb))
                throw new ScopeIOException("Can only load bootloader through USB interface");
            DataSourceScope.Stop();
            ((SmartScopeInterfaceUsb)hardwareInterface).LoadBootLoader();
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
                throw new ScopeIOException("Reset incomplete - destroying hardware interface");
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

        public bool SendOverviewBuffer
        {
            get { return StrobeMemory[STR.VIEW_SEND_OVERVIEW].GetBool(); }
            set { StrobeMemory[STR.VIEW_SEND_OVERVIEW].Set(value); }
        }

        /// <summary>
        /// Get a package of scope data
        /// </summary>
        /// <returns>Null in case communication failed, a data package otherwise. Might result in disconnecting the device if a sync error occurs</returns>
        private byte[] rxBuffer = new byte[Constants.SZ_HDR + Constants.FETCH_SIZE_MAX]; // Max received = header + full acq buf
        private Hardware.SmartScopeHeader hdr;
        List<AnalogChannel> analogChannels = new List<AnalogChannel>() { AnalogChannel.ChA, AnalogChannel.ChB };
        public DataPackageScope GetScopeData()
		{
			if (HardwareInterface == null)
				return null;
            int received = 0;
            try
            {
                received = hardwareInterface.GetAcquisition(rxBuffer);
            } catch (ScopeIOException e)
            {
                Logger.Error("Failed to get acquisition: " + e.Message);
                return null;
            }
            
            if (received < Constants.SZ_HDR)
                return null;

            GCHandle handle = GCHandle.Alloc(rxBuffer, GCHandleType.Pinned);
            hdr = (SmartScopeHeader)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(SmartScopeHeader));
            handle.Free();

            acquiring       = hdr.flags.HasFlag(HeaderFlags.Acquiring);
            stopPending     = hdr.flags.HasFlag(HeaderFlags.IsLastAcquisition);
            awaitingTrigger = hdr.flags.HasFlag(HeaderFlags.AwaitingTrigger);
            armed           = hdr.flags.HasFlag(HeaderFlags.Armded);

            if (hdr.flags.HasFlag(HeaderFlags.TimedOut))
                return null;

            if (received <= Constants.SZ_HDR)
                throw new ScopeIOException("Too little data");
            Dictionary<AnalogChannel, GainCalibration> channelConfig = hdr.ChannelSettings(this.rom);
            Dictionary<Channel, Array> receivedData = SplitAndConvert(rxBuffer, analogChannels, hdr, channelConfig, Constants.SZ_HDR, received - Constants.SZ_HDR);

            ChannelDataSourceScope source = hdr.flags.HasFlag(HeaderFlags.IsOverview) ? ChannelDataSourceScope.Overview :
                                            hdr.flags.HasFlag(HeaderFlags.IsFullAcqusition) ? ChannelDataSourceScope.Acquisition :
                                            ChannelDataSourceScope.Viewport;

            bool newAcquisition = currentDataPackage == null || currentDataPackage.Identifier != hdr.acquisition_id;

            if (newAcquisition && source == ChannelDataSourceScope.Overview)
                Logger.Error(String.Format("Acquisition source for new acqusition is Overview, but {0:s}", source));

            Int64 ViewportOffsetSamples = hdr.GetRegister(REG.VIEW_OFFSET_B0) +
                                    (hdr.GetRegister(REG.VIEW_OFFSET_B1) << 8) +
                                    (hdr.GetRegister(REG.VIEW_OFFSET_B2) << 16);
            double samplePeriod = BASE_SAMPLE_PERIOD * Math.Pow(2, hdr.GetRegister(REG.INPUT_DECIMATION));

            int n_samples = hdr.n_bursts * hdr.bytes_per_burst / 2;
            if (newAcquisition)
            {
                if (hdr.offset != 0) {
                    Logger.Error("Got an off-set package but didn't get any date before");
                    return null;
                }
                    

                /* FIXME: integrate into header*/
                uint acquisitionDepth = (uint)(2048 << hdr.GetRegister(REG.ACQUISITION_DEPTH));
                int viewportExcessiveSamples = hdr.GetRegister(REG.VIEW_EXCESS_B0) + (hdr.GetRegister(REG.VIEW_EXCESS_B1) << 8);
                double ViewportExcess = viewportExcessiveSamples * samplePeriod;
                Int64 holdoffSamples = hdr.GetRegister(REG.TRIGGERHOLDOFF_B0) +
                                    (hdr.GetRegister(REG.TRIGGERHOLDOFF_B1) << 8) +
                                    (hdr.GetRegister(REG.TRIGGERHOLDOFF_B2) << 16) +
                                    (hdr.GetRegister(REG.TRIGGERHOLDOFF_B3) << 24) - TriggerDelay(TriggerValue.mode, hdr.GetRegister(REG.INPUT_DECIMATION));
                double TriggerHoldoff = holdoffSamples * (SmartScope.BASE_SAMPLE_PERIOD * Math.Pow(2, hdr.GetRegister(REG.INPUT_DECIMATION)));
                int ViewportLength = (hdr.bytes_per_burst / 2) << hdr.GetRegister(REG.VIEW_BURSTS);

                currentDataPackage = new DataPackageScope(this.GetType(),
                    acquisitionDepth, samplePeriod,
                    ViewportLength, ViewportOffsetSamples,
                    TriggerHoldoff, holdoffSamples, 
                    hdr.flags.HasFlag(HeaderFlags.Rolling), hdr.acquisition_id, 
                    hdr.TriggerValue(channelConfig, probeSettings), 
                    ViewportExcess);

                currentDataPackage.Settings["InputDecimation"] = hdr.GetRegister(REG.INPUT_DECIMATION);
                foreach (AnalogChannel ch in analogChannels)
                {
                    currentDataPackage.SaturationLowValue[ch] = ((byte)0).ConvertByteToVoltage(channelConfig[ch], hdr.GetRegister(ch.YOffsetRegister()), probeSettings[ch]);
                    currentDataPackage.SaturationHighValue[ch] = ((byte)255).ConvertByteToVoltage(channelConfig[ch], hdr.GetRegister(ch.YOffsetRegister()), probeSettings[ch]);
                    currentDataPackage.Resolution[ch] = probeSettings[ch].RawToUser((float)channelConfig[ch].coefficients[0]);
                    currentDataPackage.Settings["Multiplier" + ch.Name] = channelConfig[ch].multiplier;
#if DEBUG
                    currentDataPackage.Settings["Divider" + ch.Name] = channelConfig[ch].divider;
                    currentDataPackage.Settings["Offset" + ch.Name] = ConvertYOffsetByteToVoltage(ch, hdr.GetRegister(ch.YOffsetRegister()));
#endif
                }
            }

            currentDataPackage.offset[ChannelDataSourceScope.Viewport] = samplePeriod * ViewportOffsetSamples;
            currentDataPackage.samplePeriod[ChannelDataSourceScope.Viewport] = BASE_SAMPLE_PERIOD * Math.Pow(2, hdr.GetRegister(REG.INPUT_DECIMATION) + hdr.GetRegister(REG.VIEW_DECIMATION));

            foreach (Channel ch in receivedData.Keys)
            {
                if(source == ChannelDataSourceScope.Acquisition)
                {
                    //Here we don't use AddData since we want to assign the whole acqbuf in memory
                    // at once instead of growing it as it comes in.
                    // Need to update datapackage timestamp though!
                    ChannelData target = currentDataPackage.GetData(ChannelDataSourceScope.Acquisition, ch);
                    Array targetArray;
                    if (target == null)
                    {
                        targetArray = Array.CreateInstance(receivedData[ch].GetType().GetElementType(), currentDataPackage.AcquisitionSamples);
                        currentDataPackage.SetData(ChannelDataSourceScope.Acquisition, ch, targetArray);
                    }
                    else
                    {
                        targetArray = target.array;
                    }
                    Array.ConstrainedCopy(receivedData[ch], 0, targetArray, hdr.offset * n_samples, receivedData[ch].Length);
                }
                else
                    currentDataPackage.AddData(source, ch, receivedData[ch]);
            }

            if(source == ChannelDataSourceScope.Acquisition)
            {
                float fullAcquisitionDumpProgress = (hdr.offset + 1) * (float)n_samples / currentDataPackage.AcquisitionSamples;

                //update FullAcquisitionFetchProgress and fire event when finished
                float previousAcqTransferProgress = currentDataPackage.FullAcquisitionFetchProgress;
                currentDataPackage.FullAcquisitionFetchProgress = fullAcquisitionDumpProgress;
                if (currentDataPackage.FullAcquisitionFetchProgress == 1 && previousAcqTransferProgress < 1)
                {
                    currentDataPackage.UpdateTimestamp();
                    if(OnAcquisitionTransferFinished != null)
                        OnAcquisitionTransferFinished(this, new EventArgs());
                }
            }

            return currentDataPackage;
        }

        private Dictionary<Channel, Array> SplitAndConvert(byte[] buffer, List<AnalogChannel> channels, SmartScopeHeader header, Dictionary<AnalogChannel, SmartScope.GainCalibration> channelSettings, int offset, int length)
        {
            int n_channels = channels.Count;
            int n_samples = length / n_channels;
            byte[][] splitRaw = new byte[n_channels][];
            float[][] splitVolt = new float[n_channels][];

            for (int j = 0; j < n_channels; j++)
            {
                splitRaw[j] = new byte[n_samples];
                splitVolt[j] = new float[n_samples];
            }

            //this section converts twos complement to a physical voltage value
            Dictionary<Channel, Array> result = new Dictionary<Channel, Array>();

            for (int j = 0; j < n_channels; j++)
            {
                AnalogChannel ch = channels[j];
                double[] coeff = channelSettings[ch].coefficients;
				if(coeff.Length != 3)
					throw new Exception(String.Format("Calibration coefficients are not of length 3, but {0} for ch {1} (n_ch:{2}", coeff.Length, ch.Name, n_channels));
                byte yOffset = header.GetRegister(ch.YOffsetRegister());
                float probeGain = probeSettings[ch].Gain;
                float probeOffset = probeSettings[ch].Offset;
                float totalOffset = (float)(yOffset * coeff[1] + coeff[2]);

                int k = j;
				if(offset + n_channels * (n_samples - 1) + k >= buffer.Length)
					throw new Exception(String.Format("Buffer will be addressed out of bounds. [offset:{0}][n_chan:{1}][length:{2}][n_samp:{3}][buf_len:{4}]", offset, n_channels, length, n_samples, buffer.Length));
                for (int i = 0; i < n_samples; i++)
                {
                    byte b = buffer[offset + k];
                    splitRaw[j][i] = b;
                    splitVolt[j][i] = (float)(b * coeff[0] + totalOffset) * probeGain + probeOffset;
                    k += n_channels;
                }
            }

            for (int j = 0; j < n_channels; j++)
            {
                AnalogChannel ch = channels[j];
                if (header.GetStrobe(STR.LA_ENABLE) && header.GetStrobe(STR.LA_CHANNEL) == ch.Value > 0)
                {
					if(j >= splitRaw.Length)
						throw new Exception(String.Format("Assigning LA channel failing due to oob index {1} (len = {2})", j, splitRaw.Length));
                    result[LogicAnalyserChannel.LA] = splitRaw[j];
                }
                else
                {
					if(j >= splitVolt.Length)
						throw new Exception(String.Format("Assigning Voltage channel failing due to oob index {1} (len = {2})", j, splitVolt.Length));
                    result[ch] = splitVolt[j];
                }
				if(j >= splitRaw.Length)
					throw new Exception(String.Format("Assigning RAW failing due to oob index {1} (len = {2})", j, splitRaw.Length));
                result[ch.Raw()] = splitRaw[j];
            }
            return result;
        }

        //FIXME: this needs proper handling
        private bool Connected { get { return this.HardwareInterface != null && !this.hardwareInterface.Destroyed && this.flashed; } }
        public bool Ready { get { return this.Connected && this.deviceReady; } }

        #endregion
    }

    internal static class Helpers
    {
        public static TriggerValue TriggerValue(this SmartScopeHeader hdr, Dictionary<AnalogChannel, SmartScope.GainCalibration> channelConfig, Dictionary<AnalogChannel, Probe> probeSettings)
        {
            byte modeByte = hdr.GetRegister(REG.TRIGGER_MODE);
            TriggerValue tv = new TriggerValue()
            {
                mode = (TriggerMode)(modeByte & 0x03),
                channel = AnalogChannel.List.Single(x => x.Value == ((modeByte >> 2) & 0x01)),
                source = (TriggerSource)((modeByte >> 3) & 0x01),
                edge = (TriggerEdge)((modeByte >> 4) & 0x03),
            };
            tv.pulseWidthMin = (
                    (hdr.GetRegister(REG.TRIGGER_PW_MIN_B0) << 0) &
                    (hdr.GetRegister(REG.TRIGGER_PW_MIN_B1) << 8) &
                    (hdr.GetRegister(REG.TRIGGER_PW_MIN_B2) << 16)
                    ) * SmartScope.BASE_SAMPLE_PERIOD;
            tv.pulseWidthMax = (
                    (hdr.GetRegister(REG.TRIGGER_PW_MAX_B0) << 0) &
                    (hdr.GetRegister(REG.TRIGGER_PW_MAX_B1) << 8) &
                    (hdr.GetRegister(REG.TRIGGER_PW_MAX_B2) << 16)
                    ) * SmartScope.BASE_SAMPLE_PERIOD;
            tv.level = hdr.GetRegister(REG.TRIGGER_LEVEL).ConvertByteToVoltage(channelConfig[tv.channel], hdr.GetRegister(tv.channel.YOffsetRegister()), probeSettings[tv.channel]);
            return tv;
        }
        public static Dictionary<AnalogChannel, SmartScope.GainCalibration> ChannelSettings(this SmartScopeHeader h, SmartScope.Rom r)
        {
            Dictionary<AnalogChannel, SmartScope.GainCalibration> settings = new Dictionary<AnalogChannel, SmartScope.GainCalibration>();

            foreach (AnalogChannel ch in AnalogChannel.List)
            {
                //Parse div_mul
                byte divMul = h.GetRegister(REG.DIVIDER_MULTIPLIER);
                int chOffset = ch.Value * 4;
                double div = SmartScope.validDividers[(divMul >> (0 + chOffset)) & 0x3];
                double mul = SmartScope.validMultipliers[(divMul >> (2 + chOffset)) & 0x3];

                settings.Add(ch, r.getCalibration(ch, div, mul));
            }
            return settings;
        }

        public static float ConvertByteToVoltage(this byte b, SmartScope.GainCalibration calibration, byte yOffset, Probe division)
        {
            return (new byte[] { b }).ConvertByteToVoltage(calibration, yOffset, division)[0];
        }
        public static float[] ConvertByteToVoltage(this byte[] buffer, SmartScope.GainCalibration calibration, byte yOffset, Probe probe)
        {
            double[] coefficients = calibration.coefficients;
            float[] voltage = new float[buffer.Length];

            //this section converts twos complement to a physical voltage value
            float totalOffset = (float)(yOffset * coefficients[1] + coefficients[2]);
            float probeGain = probe.Gain;
            float probeOffset = probe.Offset;

            voltage = buffer.Select(x => (float)(x * coefficients[0] + totalOffset) * probeGain + probeOffset).ToArray();
            return voltage;
        }
                
        public static REG YOffsetRegister(this AnalogChannel ch)
        {
            return (ch == AnalogChannel.ChA) ? REG.CHA_YOFFSET_VOLTAGE :
                /*(ch == AnalogChannel.ChB) ?*/ REG.CHB_YOFFSET_VOLTAGE;
        }
    }
}
