using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.DeviceMemories;
using Common;
using ECore.HardwareInterfaces;

namespace ECore.Devices
{
    partial class SmartScope
    {

#if DEBUG
        public 
#endif
        static readonly double[] validDividers = { 1, 6, 36 };
#if DEBUG
        public 
#endif
        static readonly double[] validMultipliers = { 1.1, 2, 3 };

        private class Range
        {
            public Range(float minimum, float maximum)
            {
                this.minimum = minimum;
                this.maximum = maximum;
            }
            public float minimum;
            public float maximum;
        }

        private Dictionary<AnalogChannel, Coupling> coupling;
        private Dictionary<AnalogChannel, ProbeDivision> probeSettings;
        private Dictionary<AnalogChannel, Range> verticalRanges;
        private Dictionary<AnalogChannel, float> yOffset;

        private double holdoff;
#if DEBUG
        public 
#endif
        static byte yOffsetMax = 200;
#if DEBUG
        public 
#endif
        static byte yOffsetMin = 10;

        float triggerThreshold = 0f;
        int viewPortSamples = 2048;

        public bool ChunkyAcquisitions { get; private set; }

        #region helpers

        private void validateDivider(double div)
        {
            if (!validDividers.Contains(div))
                throw new ValidationException(
                    "Invalid divider, valid values are: " +
                    String.Join(", ", validDividers.Select(x => x.ToString()).ToArray())
                    );
        }
        private void validateMultiplier(double mul)
        {
            if(!validMultipliers.Contains(mul))
                throw new ValidationException(
                    "Invalid multiplier, valid values are: " + 
                    String.Join(", ", validMultipliers.Select(x => x.ToString()).ToArray())
                    );
        }
        private void toggleAcquisitionUpdateStrobe()
        {
            if (!Connected) return;
            StrobeMemory[STR.SCOPE_UPDATE].WriteImmediate(true);
        }
        private void toggleViewUpdateStrobe()
        {
            if (!Connected) return;
            StrobeMemory[STR.VIEW_UPDATE].WriteImmediate(true);
        }

        private float ProbeScaleHostToScope(AnalogChannel ch, float volt)
        {
            return volt / ProbeScaleFactors[probeSettings[ch]];
        }
        private float ProbeScaleScopeToHost(AnalogChannel ch, float volt)
        {
            return volt * ProbeScaleFactors[probeSettings[ch]];
        }
        public void CommitSettings()
        {
            try
            {
                bool acquisitionUpdateRequired = false;
                bool viewUpdateRequired = false;

                PicMemory.Commit();
                AdcMemory.Commit();
                List<MemoryRegister> FpgaRegisters = FpgaSettingsMemory.Commit();
                List<MemoryRegister> FpgaStrobes = StrobeMemory.Commit();
                
                var FpgaRegistersAddresses = FpgaRegisters.Select(x => (REG)x.Address);
                if (FpgaRegistersAddresses.Where(x => AcquisitionRegisters.Contains(x)).Count() > 0)
                    acquisitionUpdateRequired = true;
                if (FpgaRegistersAddresses.Where(x => DumpRegisters.Contains(x)).Count() > 0)
                    viewUpdateRequired = true;
                if (!acquisitionUpdateRequired && FpgaStrobes.Select(x => (STR)x.Address).Where(x => AcquisitionStrobes.Contains(x)).Count() > 0)
                    acquisitionUpdateRequired = true;
                
                if (acquisitionUpdateRequired)
                    toggleAcquisitionUpdateStrobe();
                if(viewUpdateRequired)
                    toggleViewUpdateStrobe();
                
            }
            catch (ScopeIOException e)
            {
                Logger.Error("I/O failure while committing scope settings (" + e.Message + ")");
            }
        }
        #endregion

        #region vertical

        /// <summary>
        /// Sets vertical offset of a channel
        /// </summary>
        /// <param name="channel">0 or 1 (channel A or B)</param>
        /// <param name="offset">Vertical offset in Volt</param>
        public void SetYOffset(AnalogChannel channel, float offset)
        {
            yOffset[channel] = offset;
            if (!Connected) return;
            //FIXME: convert offset to byte value
            REG r = (channel == AnalogChannel.ChA) ? REG.CHA_YOFFSET_VOLTAGE : REG.CHB_YOFFSET_VOLTAGE;
            //Logger.Debug("Set Y-offset for channel " + channel + " to " + offset + "V");
            //Offset: 0V --> 150 - swing +-0.9V
            
            //Let ADC output of 127 be the zero point of the Yoffset
            double[] c = channelSettings[channel].coefficients;
            int offsetInt = (int)(-(ProbeScaleHostToScope(channel, offset) + c[2] + c[0] * 127) / c[1]);

            FpgaSettingsMemory[r].Set((byte)Math.Max(yOffsetMin, Math.Min(yOffsetMax, -(ProbeScaleHostToScope(channel, offset) + c[2] + c[0] * 127) / c[1])));
            //Logger.Debug(String.Format("Yoffset Ch {0} set to {1} V = byteval {2}", channel, GetYOffset(channel), FpgaSettingsMemory[r].GetByte()));
        }

        private float ConvertYOffsetByteToVoltage(AnalogChannel channel, byte value)
        {
            double[] c = channelSettings[channel].coefficients;
            float voltageSet = (float)(-value * c[1] - c[2] - c[0] * 127.0);
            return ProbeScaleScopeToHost(channel, voltageSet);
        }

		public float GetYOffset(AnalogChannel channel)
		{
            REG r = (channel == AnalogChannel.ChA) ? REG.CHA_YOFFSET_VOLTAGE : REG.CHB_YOFFSET_VOLTAGE;
            byte offsetByte = FpgaSettingsMemory[r].GetByte();
            return ConvertYOffsetByteToVoltage(channel, offsetByte);
        }

        //FIXME: this might be need to be implemented as LUT
        public float GetYOffsetMax(AnalogChannel channel) { return ConvertYOffsetByteToVoltage(channel, yOffsetMax); }
        public float GetYOffsetMin(AnalogChannel channel) { return ConvertYOffsetByteToVoltage(channel, yOffsetMin); }

        /// <summary>
        /// Sets and uploads the divider and multiplier what are optimal for the requested range
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="minimum"></param>
        /// <param name="maximum"></param>
        public void SetVerticalRange(AnalogChannel channel, float minimum, float maximum)
        {
            if (!Connected) return;
            //The voltage range for div/mul = 1/1
            //20140808: these seem to be OK: on div0/mult0 the ADC input range is approx 1.3V
            float baseMin = -0.6345f; //V
            float baseMax = 0.6769f; //V

            //Walk through dividers/multipliers till requested range fits
            //this walk assumes it starts with the smallest range, and that range is only increasing
            int dividerIndex = 0;
            int multIndex = 0;

            verticalRanges[channel] = new Range(minimum, maximum);

            for (int i = 0; i < rom.computedDividers.Length * rom.computedMultipliers.Length; i++)
            {
                dividerIndex= i / rom.computedMultipliers.Length;
                multIndex = rom.computedMultipliers.Length - (i % rom.computedMultipliers.Length) - 1;
                if (
                    (ProbeScaleHostToScope(channel, maximum) < baseMax * rom.computedDividers[dividerIndex] / rom.computedMultipliers[multIndex])
                    &&
                    (ProbeScaleHostToScope(channel, minimum) > baseMin * rom.computedDividers[dividerIndex] / rom.computedMultipliers[multIndex])
                    )
                    break;
            }
            SetDivider(channel, validDividers[dividerIndex]);
            SetMultiplier(channel, validMultipliers[multIndex]);
            channelSettings[channel] = rom.getCalibration(channel, validDividers[dividerIndex], validMultipliers[multIndex]);
            SetYOffset(channel, yOffset[channel]);
            yOffset[channel] = GetYOffset(channel);
            if (channel == triggerAnalog.channel)
            {
                TriggerAnalog = this.triggerAnalog;
                //SetTriggerThreshold(this.triggerThreshold);
            }
        }

        public void SetProbeDivision(AnalogChannel ch, ProbeDivision division)
        {
            probeSettings[ch] = division;
            SetVerticalRange(ch, verticalRanges[ch].minimum, verticalRanges[ch].maximum);
        }

        public ProbeDivision GetProbeDivision(AnalogChannel ch)
        {
            return probeSettings[ch];
        }

		/// <summary>
		/// Set divider of a channel
		/// </summary>
		/// <param name="channel">0 or 1 (channel A or B)</param>
		/// <param name="divider">1, 10 or 100</param>
		#if DEBUG
		public
		#else
		private
		#endif
		void SetDivider(AnalogChannel channel, double divider)
		{
			validateDivider(divider);
            byte div = (byte)(Array.IndexOf(validDividers, divider));
			int bitOffset = channel.Value * 4;
			byte mask = (byte)(0x3 << bitOffset);

			byte divMul = FpgaSettingsMemory[REG.DIVIDER_MULTIPLIER].GetByte();
			divMul = (byte)((divMul & ~mask) + (div << bitOffset));
			FpgaSettingsMemory[REG.DIVIDER_MULTIPLIER].Set(divMul);
		}

		///<summary>
		///Set multiplier of a channel
		///</summary>
		///<param name="channel">0 or 1 (channel A or B)</param>
		///<param name="multiplier">Set input stage multiplier (?? or ??)</param>
		#if DEBUG
		public
		#else
		private
		#endif
		void SetMultiplier(AnalogChannel channel, double multiplier)
		{
			validateMultiplier(multiplier);

			int bitOffset = channel.Value * 4;
			byte mul = (byte)(Array.IndexOf(validMultipliers, multiplier) << 2);
			byte mask = (byte)(0xC << bitOffset);

			byte divMul = FpgaSettingsMemory[REG.DIVIDER_MULTIPLIER].GetByte();
			divMul = (byte)((divMul & ~mask) + (mul << bitOffset));
			FpgaSettingsMemory[REG.DIVIDER_MULTIPLIER].Set(divMul);
		}

#if DEBUG
        public void SetYOffsetByte(AnalogChannel channel, byte offset)
        {
            REG r = channel == AnalogChannel.ChA ? REG.CHA_YOFFSET_VOLTAGE : REG.CHB_YOFFSET_VOLTAGE;
            Logger.Debug("Set Y offset for channel " + channel + " to " + offset + " (int value)");
            FpgaSettingsMemory[r].Set(offset);
        }
#endif

        private bool disableVoltageConversion = false;
        /// <summary>
        /// Disable the voltage conversion to have GetVoltages return the raw bytes as sample values (cast to float though)
        /// </summary>
        /// <param name="disable"></param>
        public void SetDisableVoltageConversion(bool disable)
        {
            this.disableVoltageConversion = disable;
        }

        public void SetCoupling(AnalogChannel channel, Coupling coupling)
        {
            STR dc = channel == AnalogChannel.ChA ? STR.CHA_DCCOUPLING : STR.CHB_DCCOUPLING;
            bool enableDc = coupling == Coupling.DC;
            //Logger.Debug("Set DC coupling for channel " + channel + (enableDc ? " ON" : " OFF"));
            StrobeMemory[dc].Set(enableDc);
        }
        public Coupling GetCoupling(AnalogChannel channel)
        {
            STR dc = channel == AnalogChannel.ChA ? STR.CHA_DCCOUPLING : STR.CHB_DCCOUPLING;
            return StrobeMemory[dc].GetBool() ? Coupling.DC : Coupling.AC;
        }

        #endregion

        #region horizontal
        ///<summary>
        ///Set scope trigger level
        ///</summary>
        ///<param name="trigger">Trigger condition</param>
        public AnalogTriggerValue TriggerAnalog
        {
            set
            {
                if (!Connected) return;
                this.triggerAnalog = value;

                /* Set Level */
                double[] coefficients = channelSettings[value.channel].coefficients;
                REG offsetRegister = value.channel == AnalogChannel.ChB ? REG.CHB_YOFFSET_VOLTAGE : REG.CHA_YOFFSET_VOLTAGE;
                double level = 0;
                if (coefficients != null)
                    level = (ProbeScaleHostToScope(value.channel, value.level) - FpgaSettingsMemory[offsetRegister].GetByte() * coefficients[1] - coefficients[2]) / coefficients[0];
                if (level < 0) level = 0;
                if (level > 255) level = 255;

                //Logger.Debug(" Set trigger level to " + trigger.level + "V (" + level + ")");
                FpgaSettingsMemory[REG.TRIGGER_LEVEL].Set((byte)level);

                /* Set Channel */
                //Logger.Debug(" Set trigger channel to " + (triggerAnalog.channel == AnalogChannel.ChA ? " CH A" : "CH B"));
                FpgaSettingsMemory[REG.TRIGGER_MODE].Set(
                    (byte)(
                        (FpgaSettingsMemory[REG.TRIGGER_MODE].GetByte() & 0xF3) +
                        (triggerAnalog.channel.Value << 2)
                            ));

                /* Set Direction */
                FpgaSettingsMemory[REG.TRIGGER_MODE].Set(
                    (byte)(
                        (FpgaSettingsMemory[REG.TRIGGER_MODE].GetByte() & 0xCF) +
                        (((int)triggerAnalog.direction << 4) & 0x30)
                        )
                );
                //Logger.Debug(" Set trigger channel to " + Enum.GetName(typeof(TriggerDirection), triggerAnalog.direction));
            }
        }

        public void ForceTrigger()
        {
            if(Ready)
                StrobeMemory[STR.FORCE_TRIGGER].WriteImmediate(true);
        }
#if DEBUG
        public void SetTriggerByte(byte level)
        {
            FpgaSettingsMemory[REG.TRIGGER_LEVEL].Set(level);
        }
#endif
        /// <summary>
        /// Choose channel upon which to trigger
        /// </summary>
        /// <param name="channel"></param>
        public void SetTriggerChannel(AnalogChannel channel)
        {
            this.triggerAnalog.channel = channel;
            TriggerAnalog = this.triggerAnalog;
        }


        public AnalogChannel GetTriggerChannel()
        {         
            int chNumber = (FpgaSettingsMemory[REG.TRIGGER_MODE].GetByte() & 0x0C) >> 2;
            return AnalogChannel.List.Single(x => x.Value == chNumber);
        }

        /// <summary>
        /// Choose between rising or falling trigger
        /// </summary>
        /// <param name="direction"></param>
        private void SetTriggerDirection(TriggerDirection direction)
        {
            triggerAnalog.direction = direction;
            TriggerAnalog = this.triggerAnalog;
        }
        public uint TriggerWidth
        {
            set
            {
                FpgaSettingsMemory[REG.TRIGGER_WIDTH].Set((byte)value);
            }
            get
            {
                return (uint)FpgaSettingsMemory[REG.TRIGGER_WIDTH].GetByte();
            }
        }

        public float TriggerThreshold
        {
            set
            {
                Logger.Warn("Trigger threshold is not implemented!");
                return;
                //throw new NotImplementedException("Forget it");
                triggerThreshold = value;
                double level = 0;
                double[] coefficients = channelSettings[GetTriggerChannel()].coefficients;
                if (coefficients != null)
                    level = (ProbeScaleHostToScope(triggerAnalog.channel, triggerThreshold) - coefficients[2]) / coefficients[0];
                if (level < 0) level = 0;
                if (level > 255) level = 255;
                FpgaSettingsMemory[REG.TRIGGER_THRESHOLD].Set((byte)level);
            }
            get
            {
                return triggerThreshold;
            }
        }

        public AcquisitionMode AcquisitionMode
        {
            set
            {
                FpgaSettingsMemory[REG.TRIGGER_MODE].Set(
                    (byte)(
                        (FpgaSettingsMemory[REG.TRIGGER_MODE].GetByte() & 0x3F) +
                        (((int)value << 6) & 0xC0)
                    )
                );
            }
        }

        public bool CanRoll
        {
            get
            {
                return FpgaSettingsMemory[REG.INPUT_DECIMATION].GetByte() >= INPUT_DECIMATION_MIN_FOR_ROLLING_MODE;
            }
        }
        public bool Rolling
        {
            get { return CanRoll && StrobeMemory[STR.ROLL].GetBool(); }
            set { StrobeMemory[STR.ROLL].Set(value); }
        }

        public bool Running {
            set
            {
                if (!Connected) return;
                STR s;
                if (value)
                {
                    s = STR.ACQ_START;
                    acquiring = true;
                    stopPending = false;
                }
                else
                {
                    //Don't assume we'll stop immediately
                    s = STR.ACQ_STOP;
                }

                StrobeMemory[s].WriteImmediate(true);
            }
            get { return Ready && acquiring; } 
        }
        public bool StopPending { get { return Ready && stopPending; } }

        public Dictionary<DigitalChannel, DigitalTriggerValue> TriggerDigital
        {
            set
            {
                int rising = value.Aggregate(0, (r, x) => r + ((x.Value == DigitalTriggerValue.R ? 1 : 0) << x.Key.Value));
                int falling = value.Aggregate(0, (r, x) => r + ((x.Value == DigitalTriggerValue.F ? 1 : 0) << x.Key.Value));
                int high = value.Aggregate(0, (r, x) => r + ((x.Value == DigitalTriggerValue.H ? 1 : 0) << x.Key.Value));
                int low = value.Aggregate(0, (r, x) => r + ((x.Value == DigitalTriggerValue.L ? 1 : 0) << x.Key.Value));
                FpgaSettingsMemory[REG.DIGITAL_TRIGGER_RISING].Set((byte)rising);
                FpgaSettingsMemory[REG.DIGITAL_TRIGGER_FALLING].Set((byte)falling);
                FpgaSettingsMemory[REG.DIGITAL_TRIGGER_HIGH].Set((byte)high);
                FpgaSettingsMemory[REG.DIGITAL_TRIGGER_LOW].Set((byte)low);

                //FIXME: We currently don't support passing the LA data through channel B, so the trigger needs to be set to chA
                TriggerAnalog = new AnalogTriggerValue() { channel = AnalogChannel.ChA, direction = TriggerDirection.RISING, level = 0 };
            }
        }

        public double AcquisitionLengthMax
        {
            get { return ACQUISITION_DEPTH_MAX * BASE_SAMPLE_PERIOD * 255; }
        }

        public double AcquisitionLength
        {
            get { return AcquisitionDepth * SamplePeriod; }
            set 
            {
                uint samples = (uint)(value / BASE_SAMPLE_PERIOD);
                double ratio = samples / OVERVIEW_BUFFER_SIZE;
                int log2OfRatio = (int)Math.Log(ratio, 2);
                if (log2OfRatio < 0)
                    log2OfRatio = 0;
                AcquisitionDepth = (uint)(OVERVIEW_BUFFER_SIZE * Math.Pow(2, log2OfRatio));

                ratio = (double)samples / AcquisitionDepth;
                log2OfRatio = (int)Math.Log(ratio, 2);
                if (log2OfRatio < 0)
                    log2OfRatio = 0;
                SubSampleRate = log2OfRatio;
            }
        }

        public uint AcquisitionDepth
        {
            set
            {
                if (value > ACQUISITION_DEPTH_MAX)
                    value = ACQUISITION_DEPTH_MAX;
                double multiple = Math.Ceiling((double)value / OVERVIEW_BUFFER_SIZE);
                double power = Math.Log(multiple, 2);
                FpgaSettingsMemory[REG.ACQUISITION_DEPTH].Set((int)power);
            }
            get
            {
                return (uint)(OVERVIEW_BUFFER_SIZE * Math.Pow(2, FpgaSettingsMemory[REG.ACQUISITION_DEPTH].GetByte()));
            }
        }
        public double AcquisitionTimeSpan { get { return SamplesToTime(AcquisitionDepth); } } 

        private uint VIEWPORT_SAMPLES_MIN = 128;
        private uint VIEWPORT_SAMPLES_MAX = 2048;

        public void SetViewPort(double offset, double timespan)
        {
            /*                maxTimeSpan
             *            <---------------->
             *  .--------------------------,
             *  |        ||       ||       |
             *  `--------------------------`
             *  <--------><------->
             *    offset   timespan
             */
            double maxTimeSpan = AcquisitionTimeSpan - offset;
            if (timespan > maxTimeSpan)
            {
                if (timespan > AcquisitionTimeSpan)
                {
                    timespan = AcquisitionTimeSpan;
                    offset = 0;
                }
                else
                {
                    //Limit offset so the timespan can fit
                    offset = AcquisitionTimeSpan - timespan;
                }
            }

            //Decrease the number of samples till viewport sample period is larger than 
            //or equal to the full sample rate
            uint samples = VIEWPORT_SAMPLES_MAX;

            int viewDecimation = 0;
            while (true)
            {
                viewDecimation = (int)Math.Ceiling(Math.Log(timespan / samples / SamplePeriod, 2));
                if (viewDecimation >= 0)
                    break;
                samples /= 2;
            }

            if (samples < VIEWPORT_SAMPLES_MIN)
            {
                Logger.Warn("Unfeasible zoom level");
                return;
            }

            if (viewDecimation > VIEW_DECIMATION_MAX)
            {
                Logger.Warn("Clipping view decimation! better decrease the sample rate!");
                viewDecimation = VIEW_DECIMATION_MAX;
            }

            viewPortSamples = (int)(timespan / (SamplePeriod * Math.Pow(2, viewDecimation)));
            int bursts = (int)Math.Pow(2, Math.Ceiling(Math.Log(Math.Ceiling((double)viewPortSamples / SAMPLES_PER_BURST), 2)));

            //Make sure these number of samples are actually available in the acquisition buffer
            
            
            FpgaSettingsMemory[REG.VIEW_DECIMATION].Set(viewDecimation);
            FpgaSettingsMemory[REG.VIEW_BURSTS].Set(bursts);
            
            SetViewPortOffset(offset, ComputeViewportSamplesExcess(AcquisitionDepth, SamplePeriod, offset, SAMPLES_PER_BURST * bursts, viewDecimation));
        }

        void SetViewPortOffset(double time, int samplesExcess)
        {
            Int32 samples = TimeToSamples(time, FpgaSettingsMemory[REG.INPUT_DECIMATION].GetByte()) - samplesExcess;
            if(samples < 0)
                samples = 0;
            FpgaSettingsMemory[REG.VIEW_OFFSET_B0].Set((byte)(samples));
            FpgaSettingsMemory[REG.VIEW_OFFSET_B1].Set((byte)(samples >> 8));
            FpgaSettingsMemory[REG.VIEW_OFFSET_B2].Set((byte)(samples >> 16));

            FpgaSettingsMemory[REG.VIEW_EXCESS_B0].Set((byte)(samplesExcess));
            FpgaSettingsMemory[REG.VIEW_EXCESS_B1].Set((byte)(samplesExcess >> 8));
        }

        public int SubSampleRate { 
            get { return FpgaSettingsMemory[REG.INPUT_DECIMATION].GetByte(); }
            private set { FpgaSettingsMemory[REG.INPUT_DECIMATION].Set((byte)value); } 
        }

        public double SamplePeriod
        {
            get { return BASE_SAMPLE_PERIOD * Math.Pow(2, SubSampleRate); }
        }

        public double SamplesToTime(uint samples)
        {
            return samples * SamplePeriod;
        }

        private Int32 TimeToSamples(double time, int inputDecimation)
        {
            return (Int32)(time / (BASE_SAMPLE_PERIOD * Math.Pow(2, inputDecimation)));
        }

        internal static int ComputeViewportSamplesExcess(uint acqDepth, double samplePeriod, double viewportOffset, int viewportSamples, int viewportDecimation)
        {
            double viewportSamplePeriod = samplePeriod * Math.Pow(2, viewportDecimation);
            double endTime = viewportOffset + viewportSamples * viewportSamplePeriod;
            double acquisitionTimeSpan = acqDepth * samplePeriod;
            if (endTime > acquisitionTimeSpan)
                return (int)((endTime - acquisitionTimeSpan) / samplePeriod);
            else
                return 0;
        }


        public double ViewPortTimeSpan
        {
            get { return viewPortSamples * (SamplePeriod * Math.Pow(2, FpgaSettingsMemory[REG.VIEW_DECIMATION].GetByte())); }
        }

        public double ViewPortOffset {
            get {
                int samples = 
                    FpgaSettingsMemory[REG.VIEW_OFFSET_B0].GetByte() +
                    (FpgaSettingsMemory[REG.VIEW_OFFSET_B1].GetByte() << 8) +
                    (FpgaSettingsMemory[REG.VIEW_OFFSET_B2].GetByte() << 16);

                int samplesExcess =
                    FpgaSettingsMemory[REG.VIEW_EXCESS_B0].GetByte() +
                    (FpgaSettingsMemory[REG.VIEW_EXCESS_B1].GetByte() << 8);

                samples += samplesExcess;
                return SamplesToTime((uint)samples); 
            }
        }

        ///<summary>
        ///Scope hold off
        ///</summary>
        ///<param name="time">Store [time] before trigger</param>
        public double TriggerHoldOff
        {
            set
            {
                this.holdoff = value;
                if (value > AcquisitionTimeSpan)
                    this.holdoff = AcquisitionTimeSpan;
                else if (value <= 0)
                    this.holdoff = 0;
                else
                    this.holdoff = value;
                Int32 samples = TimeToSamples(value, FpgaSettingsMemory[REG.INPUT_DECIMATION].GetByte());
                //Logger.Debug(" Set trigger holdoff to " + time * 1e6 + "us or " + samples + " samples " );
                FpgaSettingsMemory[REG.TRIGGERHOLDOFF_B0].Set((byte)(samples));
                FpgaSettingsMemory[REG.TRIGGERHOLDOFF_B1].Set((byte)(samples >> 8));
                FpgaSettingsMemory[REG.TRIGGERHOLDOFF_B2].Set((byte)(samples >> 16));
                FpgaSettingsMemory[REG.TRIGGERHOLDOFF_B3].Set((byte)(samples >> 24));
            }
            get
            {
                return this.holdoff;
            }
        }

        #endregion

        #region other    
        public double AcquisitionBufferTimeSpan {
            get { return SamplePeriod * AcquisitionDepth; }
        }

        /// <summary>
        /// Print me as HEX
        /// </summary>
        /// <returns></returns>
        public uint GetFpgaFirmwareVersion()
        {
            return (UInt32)(FpgaRom[ROM.FW_GIT0].Read().GetByte() +
                   (UInt32)(FpgaRom[ROM.FW_GIT1].Read().GetByte() << 8) +
                   (UInt32)(FpgaRom[ROM.FW_GIT2].Read().GetByte() << 16) +
                   (UInt32)(FpgaRom[ROM.FW_GIT3].Read().GetByte() << 24));
        }

        public byte[] GetPicFirmwareVersion() {
            hardwareInterface.SendCommand(SmartScopeUsbInterfaceHelpers.PIC_COMMANDS.PIC_VERSION);
            byte[] response = hardwareInterface.ReadControlBytes(16);
            return response.Skip(4).Take(3).Reverse().ToArray();
        }

        #endregion

        #region Logic Analyser

        public bool LogicAnalyserEnabled
        {
            set
            {
                if (!Ready) return;
                StrobeMemory[STR.LA_ENABLE].Set(value);
                if (value)
                    StrobeMemory[STR.AWG_ENABLE].Set(false);
            }
            get
            {
                return StrobeMemory[STR.LA_ENABLE].GetBool();
            }
        }
        public AnalogChannel LogicAnalyserChannel
        {
            set
            {
                StrobeMemory[STR.LA_CHANNEL].Set(value == AnalogChannel.ChB);
            }
        }

        #endregion
    }
}
