﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.Memories;
using LabNation.Common;
using LabNation.DeviceInterface.Hardware;

namespace LabNation.DeviceInterface.Devices
{
    partial class SmartScope
    {

#if DEBUG
        public
#else
		internal
#endif
        static readonly double[] validDividers = { 1, 6, 36 };
#if DEBUG
        public 
#else
		internal
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
            return volt / probeSettings[ch];
        }
        private float ProbeScaleScopeToHost(AnalogChannel ch, float volt)
        {
            return volt * probeSettings[ch];
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
                {
                    toggleAcquisitionUpdateStrobe();
                    DiscardPreviousAcquisition = true;
                }
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


		//The voltage range for div/mul = 1/1
		float baseVoltageRangeMin = -0.6345f; //V
		float baseVoltageRangeMax = 0.6769f; //V

        /// <summary>
        /// Sets and uploads the divider and multiplier what are optimal for the requested range
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="minimum"></param>
        /// <param name="maximum"></param>
        public void SetVerticalRange(AnalogChannel channel, float minimum, float maximum)
        {
            if (!Connected) return;
            
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
                    (ProbeScaleHostToScope(channel, maximum) < baseVoltageRangeMax * rom.computedDividers[dividerIndex] / rom.computedMultipliers[multIndex])
                    &&
                    (ProbeScaleHostToScope(channel, minimum) > baseVoltageRangeMin * rom.computedDividers[dividerIndex] / rom.computedMultipliers[multIndex])
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
		public float[] GetVerticalRange(AnalogChannel channel)
		{
			int dividerIndex = Array.IndexOf (validDividers, channelSettings [channel].divider);
			int multIndex = Array.IndexOf (validMultipliers, channelSettings [channel].multiplier);
			return new float[] {
				ProbeScaleScopeToHost(channel, (float)(baseVoltageRangeMin * rom.computedDividers[dividerIndex] / rom.computedMultipliers[multIndex])),

				ProbeScaleScopeToHost(channel, (float)(baseVoltageRangeMax * rom.computedDividers[dividerIndex] / rom.computedMultipliers[multIndex]))
			};
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
                level -= FpgaSettingsMemory[REG.TRIGGER_THRESHOLD].GetByte() / 2.0;
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
            get
            {
                AnalogTriggerValue v = this.triggerAnalog.Copy();

                //When clipping
                if (FpgaSettingsMemory[REG.TRIGGER_LEVEL].GetByte() != 0 && FpgaSettingsMemory[REG.TRIGGER_LEVEL].GetByte() != 255)
                    return v;

                TriggerDirection dir = (TriggerDirection)((FpgaSettingsMemory[REG.TRIGGER_MODE].GetByte() & 0x30) >> 4);
                AnalogChannel ch = AnalogChannel.List.Single(x=>x.Value == ((FpgaSettingsMemory[REG.TRIGGER_MODE].GetByte() & 0x0C) >> 2));

                double[] coefficients = channelSettings[ch].coefficients;
                REG offsetRegister = ch == AnalogChannel.ChB ? REG.CHB_YOFFSET_VOLTAGE : REG.CHA_YOFFSET_VOLTAGE;
                double level = 0;
                if (coefficients != null) {
                    level = FpgaSettingsMemory[REG.TRIGGER_LEVEL].GetByte() + FpgaSettingsMemory[REG.TRIGGER_THRESHOLD].GetByte() / 2.0;
                    level = level * coefficients[0] + coefficients[1] * FpgaSettingsMemory[offsetRegister].GetByte() + coefficients[2];
                    level = ProbeScaleScopeToHost(ch, (float)level);
                }

                return new AnalogTriggerValue()
                {
                    channel = ch,
                    direction = dir,
                    level = (float)level
                };
            }
        }

        private Dictionary<DigitalChannel, DigitalTriggerValue> triggerDigital = new Dictionary<DigitalChannel,DigitalTriggerValue>();
        public Dictionary<DigitalChannel, DigitalTriggerValue> TriggerDigital
        {
            set
            {
                this.triggerDigital = value;
                int rising = value.Aggregate(0, (r, x) => r + ((x.Value == DigitalTriggerValue.R ? 1 : 0) << x.Key.Value));
                int falling = value.Aggregate(0, (r, x) => r + ((x.Value == DigitalTriggerValue.F ? 1 : 0) << x.Key.Value));
                int high = value.Aggregate(0, (r, x) => r + ((x.Value == DigitalTriggerValue.H ? 1 : 0) << x.Key.Value));
                int low = value.Aggregate(0, (r, x) => r + ((x.Value == DigitalTriggerValue.L ? 1 : 0) << x.Key.Value));
                FpgaSettingsMemory[REG.DIGITAL_TRIGGER_RISING].Set((byte)rising);
                FpgaSettingsMemory[REG.DIGITAL_TRIGGER_FALLING].Set((byte)falling);
                FpgaSettingsMemory[REG.DIGITAL_TRIGGER_HIGH].Set((byte)high);
                FpgaSettingsMemory[REG.DIGITAL_TRIGGER_LOW].Set((byte)low);

                //FIXME: We currently don't support passing the LA data through channel B, so the trigger needs to be set to chA
                TriggerAnalog = new AnalogTriggerValue() { channel = ChannelSacrificedForLogicAnalyser, direction = TriggerDirection.RISING, level = 0 };
            }
            get
            {
                return this.triggerDigital;
            }
        }

        public AnalogTriggerValue AnalogTriggerValue { get; private set; }

        public TriggerModes TriggerMode
        {
            get
            {
                if (LogicAnalyserEnabled && triggerAnalog.channel.Equals(ChannelSacrificedForLogicAnalyser))
                    return TriggerModes.Digital;
                return TriggerModes.Analog;

            }
        }

        public void ForceTrigger()
        {
            if(Ready)
                StrobeMemory[STR.FORCE_TRIGGER].WriteImmediate(true);
        }
#if DEBUG
        public 
#else
        private
#endif
        void SetTriggerByte(byte level)
        {
            FpgaSettingsMemory[REG.TRIGGER_LEVEL].Set(level);
        }

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
                TriggerHoldOff = this.holdoff;
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
            get
            {
                return (AcquisitionMode)((FpgaSettingsMemory[REG.TRIGGER_MODE].GetByte() & 0xC0) >> 6);
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
            set { 
                StrobeMemory[STR.ROLL].Set(value && CanRoll);
                if (Rolling)
                {
                    SetViewPort(0, AcquisitionLength);
                }
            }
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

                StrobeMemory[s].Set(true);

                //FIXME - From VHDL (ScopeController.vhd:871)
                /*
                    -- The condition beneath makes it so that when running in continuous
				    -- mode (AUTO or REQUIRE), pressing stop before the acquisition
				    -- is finishing leaves us with a useless acquisition buffer.
				    -- Until a ping-pong scheme is implemented, we don't stop the
				    -- acquisition in this case, yet send a FORCE-TRIGGER right after
				    -- sending the STOP request in software
                 */
                //if(s == STR.ACQ_STOP && (AcquisitionMode == Devices.AcquisitionMode.NORMAL || AcquisitionMode == Devices.AcquisitionMode.SINGLE))
                //    ForceTrigger();
            }
            get { return Ready && (acquiring || stopPending); } 
        }
        public bool StopPending { get { return Ready && stopPending; } }
        public bool AwaitingTrigger { get { return Ready && awaitingTrigger; } }
        public bool Armed { get { return Ready && armed; } }

        public double AcquisitionLengthMin
        {
            get { return ACQUISITION_DEPTH_MIN * BASE_SAMPLE_PERIOD; }
        }
        public double AcquisitionLengthMax
        {
            get { return ACQUISITION_DEPTH_MAX * BASE_SAMPLE_PERIOD * Math.Pow(2, INPUT_DECIMATION_MAX); }
        }

        public bool PreferPartial { 
            get { return StrobeMemory[STR.VIEW_SEND_PARTIAL].GetBool(); }
            set { StrobeMemory[STR.VIEW_SEND_PARTIAL].Set(value); }
        }

        public double AcquisitionLength
        {
            get { return AcquisitionDepth * SamplePeriod; }
            set 
            {
                ulong samples = (ulong)(value / BASE_SAMPLE_PERIOD);
                double ratio = (double)samples / OVERVIEW_BUFFER_SIZE;
                int acquisitionDepthPower = (int)Math.Ceiling(Math.Log(ratio, 2));
                
                if (acquisitionDepthPower < 0)
                    acquisitionDepthPower = 0;

                if (samples > ACQUISITION_DEPTH_MAX)
                    AcquisitionDepth = ACQUISITION_DEPTH_MAX;
                else
                    AcquisitionDepth = (uint)(OVERVIEW_BUFFER_SIZE * Math.Pow(2, acquisitionDepthPower));
                acquisitionDepthPower = (int)Math.Log(AcquisitionDepth / OVERVIEW_BUFFER_SIZE, 2);

                ratio = (double)samples / AcquisitionDepth;
                int inputDecimationPower = (int)Math.Ceiling(Math.Log(ratio, 2));
                if (inputDecimationPower < 0)
                    inputDecimationPower = 0;
                if (inputDecimationPower > INPUT_DECIMATION_MAX)
                    inputDecimationPower = INPUT_DECIMATION_MAX;
                SubSampleRate = inputDecimationPower;


                if (PreferPartial && acquisitionDepthPower >= INPUT_DECIMATION_MIN_FOR_ROLLING_MODE && SubSampleRate < INPUT_DECIMATION_MIN_FOR_ROLLING_MODE)
                {
                    int adjustment = INPUT_DECIMATION_MIN_FOR_ROLLING_MODE - SubSampleRate;
                    acquisitionDepthPower -= adjustment;
                    AcquisitionDepth = (uint)(OVERVIEW_BUFFER_SIZE * Math.Pow(2, acquisitionDepthPower));
                    SubSampleRate += adjustment;
                }
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

        private int BURSTS_MIN = 2;
        private uint VIEWPORT_SAMPLES_MAX = 2048;

        uint? AcquisitionDepthLastPackage = null;
        private double AcquisitionLengthCurrent
        {
            get
            {
                if (Running || !AcquisitionDepthLastPackage.HasValue)
                    return AcquisitionLength;
                return AcquisitionDepthLastPackage.Value * SamplePeriodCurrent;
            }
        }

        double? SamplePeriodLastPackage = null;
        private double SamplePeriodCurrent
        {
            get
            {
                if (Running || !SamplePeriodLastPackage.HasValue)
                    return SamplePeriod;
                return SamplePeriodLastPackage.Value;
            }
        }

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
            double maxTimeSpan = AcquisitionLengthCurrent - offset;
            if (timespan > maxTimeSpan)
            {
                if (timespan > AcquisitionLengthCurrent)
                {
                    timespan = AcquisitionLengthCurrent;
                    offset = 0;
                }
                else
                {
                    //Limit offset so the timespan can fit
                    offset = AcquisitionLengthCurrent - timespan;
                }
            }

            //Decrease the number of samples till viewport sample period is larger than 
            //or equal to the full sample rate
            uint samples = VIEWPORT_SAMPLES_MAX;

            int viewDecimation = 0;
            while (true)
            {
                viewDecimation = (int)Math.Ceiling(Math.Log(timespan / samples / SamplePeriodCurrent, 2));
                if (viewDecimation >= 0)
                    break;
                samples /= 2;
            }

            if (viewDecimation > VIEW_DECIMATION_MAX)
            {
                Logger.Warn("Clipping view decimation! better decrease the sample rate!");
                viewDecimation = VIEW_DECIMATION_MAX;
            }

            viewPortSamples = (int)(timespan / (SamplePeriodCurrent * Math.Pow(2, viewDecimation)));
            int burstsLog2 = (int)Math.Ceiling(Math.Log(Math.Ceiling((double)viewPortSamples / SAMPLES_PER_BURST), 2));
            if (burstsLog2 < BURSTS_MIN)
                burstsLog2 = BURSTS_MIN;
            //Make sure these number of samples are actually available in the acquisition buffer
            
            
            FpgaSettingsMemory[REG.VIEW_DECIMATION].Set(viewDecimation);
            FpgaSettingsMemory[REG.VIEW_BURSTS].Set(burstsLog2);
            
            SetViewPortOffset(offset, ComputeViewportSamplesExcess(AcquisitionLengthCurrent, SamplePeriodCurrent, offset, (int)(SAMPLES_PER_BURST * Math.Pow(2, burstsLog2)), viewDecimation));
        }

        void SetViewPortOffset(double time, int samplesExcess)
        {
            Int32 samples = (int)(time / SamplePeriodCurrent) - samplesExcess;
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
            private set { 
                FpgaSettingsMemory[REG.INPUT_DECIMATION].Set((byte)value);
                TriggerHoldOff = this.holdoff;
            } 
        }

        public double SamplePeriod
        {
            get { return BASE_SAMPLE_PERIOD * Math.Pow(2, SubSampleRate); }
        }

        public double SamplesToTime(uint samples)
        {
            return samples * SamplePeriod;
        }

        internal static int ComputeViewportSamplesExcess(double acquisitionTimeSpan, double samplePeriod, double viewportOffset, int viewportSamples, int viewportDecimation)
        {
            double viewportSamplePeriod = samplePeriod * Math.Pow(2, viewportDecimation);
            double endTime = viewportOffset + viewportSamples * viewportSamplePeriod;
            if (endTime > acquisitionTimeSpan)
                return (int)((endTime - acquisitionTimeSpan) / samplePeriod);
            else
                return 0;
        }


        public double ViewPortTimeSpan
        {
            get { return viewPortSamples * (SamplePeriodCurrent * Math.Pow(2, FpgaSettingsMemory[REG.VIEW_DECIMATION].GetByte())); }
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
                return samples * SamplePeriodCurrent; 
            }
        }

        internal static int TriggerDelay(TriggerModes mode, uint triggerWidth, int inputDecimation)
        {
            if(mode == TriggerModes.Digital)
                return (((int)triggerWidth) >> inputDecimation) + 1;
            return (((int)triggerWidth) >> inputDecimation) + 4;
        }

        public double TriggerHoldOff
        {
            set
            {
                if (value > AcquisitionLengthCurrent)
                    this.holdoff = AcquisitionLengthCurrent;
                else
                    this.holdoff = value;
                Int32 samples = (int)(this.holdoff / SamplePeriod);
                samples += TriggerDelay(TriggerMode, TriggerWidth, SubSampleRate);
                //FIXME-FPGA bug
                if (samples >= AcquisitionDepth)
                {
                    samples = (int)AcquisitionDepth - 1;
                }
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

        public byte DigitalOutput
        {
            get { return FpgaSettingsMemory[REG.DIGITAL_OUT].GetByte(); }
            set { FpgaSettingsMemory[REG.DIGITAL_OUT].WriteImmediate(value); }
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
        public AnalogChannel ChannelSacrificedForLogicAnalyser
        {
            set
            {
                StrobeMemory[STR.LA_CHANNEL].Set(value == AnalogChannel.ChB);
                TriggerDigital = this.TriggerDigital;
            }
            get
            {
                return StrobeMemory[STR.LA_CHANNEL].GetBool() ? AnalogChannel.ChB : AnalogChannel.ChA;
            }
        }

        #endregion
    }
}
