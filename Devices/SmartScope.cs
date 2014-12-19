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

        private Dictionary<ProbeDivision, float> ProbeScaleFactors = new Dictionary<ProbeDivision, float>()
        {
            { ProbeDivision.X1,   1f },
            { ProbeDivision.X10, 10f },
        };

        private List<DeviceMemory> memories = new List<DeviceMemory>();
#if DEBUG
        public List<DeviceMemory> GetMemories() { return memories; }
#endif

#if DEBUG 
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
        int triggerAddress;

        internal static double BASE_SAMPLE_PERIOD = 10e-9; //10MHz sample rate
        private const int NUMBER_OF_SAMPLES = 2048;
        private const int BURST_SIZE = 64;
        private const int MAX_COMPLETION_TRIES = 2;
        //FIXME: this should be automatically parsed from VHDL
        internal static int INPUT_DECIMATION_MAX_FOR_FREQUENCY_COMPENSATION = 4;
        private const int INPUT_DECIMATION_MIN_FOR_ROLLING_MODE = 14;
        private const int VIEW_DECIMATION_MAX = 10;

        private bool acquiring = false;
        private bool stopPending = false;
        private bool paused = false;
        private bool acquiringWhenPaused = false;

        private Dictionary<AnalogChannel, GainCalibration> channelSettings = new Dictionary<AnalogChannel,GainCalibration>();
        private AnalogTriggerValue triggerAnalog = new AnalogTriggerValue
        {
            channel = AnalogChannel.ChA,
            direction = TriggerDirection.RISING,
            level = 0.0f
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
            AwgOutOfRange = false;
            deviceReady = false;
            
            coupling = new Dictionary<AnalogChannel, Coupling>();
            probeSettings = new Dictionary<AnalogChannel, ProbeDivision>();
            yOffset = new Dictionary<AnalogChannel, float>();
            verticalRanges = new Dictionary<AnalogChannel, Range>();
            foreach (AnalogChannel ch in AnalogChannel.List)
            {
                coupling[ch] = Coupling.DC;
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
            this.SetAcquisitionRunning(this.acquiringWhenPaused);
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

        private void ConfigureAdc()
        {
            AdcMemory[MAX19506.SOFT_RESET].WriteImmediate(90);
            AdcMemory[MAX19506.POWER_MANAGEMENT].Set(4);
            AdcMemory[MAX19506.OUTPUT_PWR_MNGMNT].Set(1);
            AdcMemory[MAX19506.FORMAT_PATTERN].Set(16);
            AdcMemory[MAX19506.CHA_TERMINATION].Set(18);
            AdcMemory[MAX19506.DATA_CLK_TIMING].Set(0);
            AdcMemory[MAX19506.POWER_MANAGEMENT].Set(3);
            AdcMemory[MAX19506.OUTPUT_FORMAT].Set(0x02); //DDR on chA
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
            //Part 1: Just set all desired memory settings

            /*********
             *  ADC  *
            *********/
            ConfigureAdc();

            /***************************/

            //Enable scope controller
            EnableEssentials(true);
            foreach (AnalogChannel ch in AnalogChannel.List)
            {
                SetVerticalRange(ch, -1f, 1f);
                SetCoupling(ch, coupling[ch]);
            }
                
            SetTriggerWidth(2);
            //SetTriggerThreshold(3);
            FpgaSettingsMemory[REG.TRIGGER_THRESHOLD].Set(3);

            SetAcquisitionDepth(512 * 1024);
            SetAwgStretching(0);
            SetAwgNumberOfSamples(AWG_SAMPLES_MAX);

            //Part 2: perform actual writes                
            StrobeMemory[STR.GLOBAL_RESET].WriteImmediate(true);
            CommitSettings();
            hardwareInterface.FlushDataPipe();
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

        private float[] ConvertByteToVoltage(AnalogChannel ch, double divider, double multiplier, byte[] buffer, byte yOffset, ProbeDivision division)
        {
            double[] coefficients = rom.getCalibration(ch, divider, multiplier).coefficients;
            float[] voltage = new float[buffer.Length];

            //this section converts twos complement to a physical voltage value
            float totalOffset = (float)(yOffset * coefficients[1] + coefficients[2]);
            float gain = ProbeScaleFactors[division];

            voltage = buffer.Select(x => (float)(x * coefficients[0] + totalOffset) * gain).ToArray();
            return voltage;
        }

#if WINDOWS
        SmartScopeHeader ResyncHeader()
        {
            int tries = 64;
            Logger.Warn("Trying to resync header by fetching up to " + tries + " packages");
         
            List<byte[]> crashBuffers = new List<byte[]>();
            byte[] buf;
            while ((buf = hardwareInterface.GetData(BURST_SIZE)) != null && tries > 0)
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

        /// <summary>
        /// Get a package of scope data
        /// </summary>
        /// <returns>Null in case communication failed, a data package otherwise. Might result in disconnecting the device if a sync error occurs</returns>
        public DataPackageScope GetScopeData ()
		{
			if (hardwareInterface == null)
				return null;

			byte[] buffer;
			SmartScopeHeader header;
            
			try {
				buffer = hardwareInterface.GetData (BURST_SIZE);
			} catch (ScopeIOException) {
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

			acquiring = !header.LastAcquisition;
			stopPending = header.ScopeStopPending;
            if (header.ImpossibleDump)
                return null;

			if (header.NumberOfPayloadBursts == 0)
				return null;

			try {
				buffer = hardwareInterface.GetData (BURST_SIZE * header.NumberOfPayloadBursts);
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

			int dataOffset;
			if (header.Rolling) {
				if (chA == null) {
					chA = new byte[header.Samples];
					chB = new byte[header.Samples];
					dataOffset = 0;
				} else { //blow up the array
					byte[] chANew = new byte[chA.Length + header.Samples];
					byte[] chBNew = new byte[chB.Length + header.Samples];
					chA.CopyTo (chANew, 0);
					chB.CopyTo (chBNew, 0);
					dataOffset = chA.Length;
					chA = chANew;
					chB = chBNew;
				}
			} else {
				//If it's part of an acquisition of which we already received
				//samples, add to previously received data
				dataOffset = 0;
				if (header.PackageOffset != 0) {
					//FIXME: this shouldn't be possible
					if (chA == null)
						return null;
                    //In case of a resync or who knows what else went wrong
                    if(triggerAddress != header.TriggerAddress)
                        return null;
					byte[] chANew = new byte[chA.Length + header.Samples];
					byte[] chBNew = new byte[chB.Length + header.Samples];
					chA.CopyTo (chANew, 0);
					chB.CopyTo (chBNew, 0);
					chA = chANew;
					chB = chBNew;
					dataOffset = BURST_SIZE * header.PackageOffset / header.Channels;
				} else { //New acquisition, new buffers
                    triggerAddress = header.TriggerAddress;
					chA = new byte[header.Samples];
					chB = new byte[header.Samples];
				}
			}

			for (int i = 0; i < header.Samples; i++) {
				chA [dataOffset + i] = buffer [header.Channels * i];
				chB [dataOffset + i] = buffer [header.Channels * i + 1];
			}

			//In rolling mode, crop the channel to the display length
			if (chA.Length > NUMBER_OF_SAMPLES) {
				byte[] chANew = new byte[NUMBER_OF_SAMPLES];
				byte[] chBNew = new byte[NUMBER_OF_SAMPLES];
				Array.ConstrainedCopy (chA, chA.Length - NUMBER_OF_SAMPLES, chANew, 0, NUMBER_OF_SAMPLES);
				Array.ConstrainedCopy (chB, chB.Length - NUMBER_OF_SAMPLES, chBNew, 0, NUMBER_OF_SAMPLES);
				chA = chANew;
				chB = chBNew;
			}

			//If we're not decimating a lot, fetch on till the package is complete
			if (!header.Rolling && header.SamplesPerAcquisition > chA.Length && header.GetRegister (REG.INPUT_DECIMATION) < INPUT_DECIMATION_MIN_FOR_ROLLING_MODE) {
				while (false && true) {
					DataPackageScope p = null;
					int tries = 0;
					while (p == null && tries < MAX_COMPLETION_TRIES) {
						tries++;
						p = GetScopeData ();
					}
					if (tries > 1) {
						Logger.Warn("Had to try " +tries+ " times to complete package");
					}
                    if (p == null)
                    {
                        Logger.Info("Failed to complete package. This can be due to a settings update during a require-trigger dump.");
                        return null;
                    }
                    if (p.Partial == false)
                        return p;
                }
            }

            this.coupling[AnalogChannel.ChA] = header.GetStrobe(STR.CHA_DCCOUPLING) ? Coupling.DC : Coupling.AC;
            this.coupling[AnalogChannel.ChB] = header.GetStrobe(STR.CHB_DCCOUPLING) ? Coupling.DC : Coupling.AC;

            //construct data package
            //FIXME: get firstsampletime and samples from FPGA
            //FIXME: parse package header and set DataPackageScope's trigger index
            DataPackageScope data = new DataPackageScope(header.SamplePeriod, chA.Length, header.TriggerHoldoff, chA.Length < header.SamplesPerAcquisition, header.Rolling);
#if DEBUG
            data.AddSetting("TriggerAddress", header.TriggerAddress);
#endif
            //Parse div_mul
            byte divMul = header.GetRegister(REG.DIVIDER_MULTIPLIER);
            double divA = validDividers[(divMul >> 0) & 0x3];
            double mulA = validMultipliers[(divMul >> 2) & 0x3];
            double divB = validDividers[(divMul >> 4) & 0x3];
            double mulB = validMultipliers[(divMul >> 6) & 0x3];

            data.AddSetting("Multiplier" + AnalogChannel.ChA.Name, mulA);
            data.AddSetting("Multiplier" + AnalogChannel.ChB.Name, mulB);
            data.AddSetting("InputDecimation", header.GetRegister(REG.INPUT_DECIMATION));
            data.SetDataRaw(AnalogChannel.ChA, chA);
            data.SetDataRaw(AnalogChannel.ChB, chB);

#if DEBUG
            data.AddSetting("DividerA", divA);
            data.AddSetting("DividerB", divB);
            data.AddSetting("OffsetA", ConvertYOffsetByteToVoltage(AnalogChannel.ChA, header.GetRegister(REG.CHA_YOFFSET_VOLTAGE)));
            data.AddSetting("OffsetB", ConvertYOffsetByteToVoltage(AnalogChannel.ChB, header.GetRegister(REG.CHB_YOFFSET_VOLTAGE)));

            if (this.disableVoltageConversion)
            {
                data.SetData(AnalogChannel.ChA, Utils.CastArray<byte, float>(chA));
                data.SetData(AnalogChannel.ChB, Utils.CastArray<byte, float>(chB));
                data.SetDataDigital(chB);
            }
            else
            {
#endif
                bool logicAnalyserOnChannelA = header.GetStrobe(STR.LA_ENABLE) && !header.GetStrobe(STR.LA_CHANNEL);
                bool logicAnalyserOnChannelB = header.GetStrobe(STR.LA_ENABLE) && header.GetStrobe(STR.LA_CHANNEL);

                bool performFrequencyCompensation = header.GetRegister(REG.INPUT_DECIMATION) <= INPUT_DECIMATION_MAX_FOR_FREQUENCY_COMPENSATION;
            
                if (logicAnalyserOnChannelA)
                    data.SetDataDigital(chA);
                else
                    data.SetData(AnalogChannel.ChA, ConvertByteToVoltage(AnalogChannel.ChA, divA, mulA, chA, header.GetRegister(REG.CHA_YOFFSET_VOLTAGE), probeSettings[AnalogChannel.ChA]));

                if (logicAnalyserOnChannelB)
                    data.SetDataDigital(chB);
                else
                    data.SetData(AnalogChannel.ChB, ConvertByteToVoltage(AnalogChannel.ChB, divB, mulB, chB, header.GetRegister(REG.CHB_YOFFSET_VOLTAGE), probeSettings[AnalogChannel.ChB]));
#if DEBUG                    
            }
#endif
            return data;
        }

        //FIXME: this needs proper handling
        private bool Connected { get { return this.hardwareInterface != null && !this.hardwareInterface.Destroyed && this.flashed; } }
        public bool Ready { get { return this.Connected && this.deviceReady && !(this.hardwareInterface == null || this.hardwareInterface.Destroyed); } }

        #endregion
    }
}
