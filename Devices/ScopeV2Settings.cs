using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.DeviceMemories;
using Common;

namespace ECore.Devices
{
    partial class ScopeV2
    {
        public static readonly int[] validChannels = { 0, 1 };
        public static readonly double[] validDividers = { 1, 6, 36 };
        public static readonly double[] validMultipliers = { 1.1, 2, 3 };
        private Dictionary<int, Coupling> coupling = new Dictionary<int, Coupling>() {
            {0, Coupling.DC}, {1, Coupling.DC}
        };
        private double holdoff;
#if INTERNAL
        public 
#endif
        static byte yOffsetMax = 200;
#if INTERNAL
        public 
#endif
        static byte yOffsetMin = 10;

        #region helpers

        private byte voltToByte(float volt)
        {
            //FIXME: implement this
            return (byte)((int)volt);
        }
        private void validateChannel(int ch)
        {
            if (!validChannels.Contains(ch))
                throw new ValidationException(
                    "Invalid channel, valid values are: " +
                    String.Join(", ", validChannels.Select(x => x.ToString()).ToArray())
                    );
        }
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
        private void toggleUpdateStrobe()
        {
            StrobeMemory[STR.SCOPE_UPDATE].WriteImmediate(false);
            StrobeMemory[STR.SCOPE_UPDATE].WriteImmediate(true);
        }

        public void CommitSettings()
        {
            int registersWritten = 0;
            foreach (DeviceMemory mem in memories)
            {
                registersWritten += mem.Commit();
            }
            if (registersWritten > 0)
                toggleUpdateStrobe();
        }
        #endregion

        #region vertical

        /// <summary>
        /// Sets vertical offset of a channel
        /// </summary>
        /// <param name="channel">0 or 1 (channel A or B)</param>
        /// <param name="offset">Vertical offset in Volt</param>
        public void SetYOffset(int channel, float offset)
        {
            validateChannel(channel);
            //FIXME: convert offset to byte value
            REG r = (channel == 0) ? REG.CHA_YOFFSET_VOLTAGE : REG.CHB_YOFFSET_VOLTAGE;
            Logger.Debug("Set DC coupling for channel " + channel + " to " + offset + "V");
            //Offset: 0V --> 150 - swing +-0.9V
            double[] c = channelSettings[channel].coefficients;
            //Let ADC output of 127 be the zero point of the Yoffset
            byte offsetByte = (byte)Math.Min(yOffsetMax, Math.Max(yOffsetMin, -(offset + c[2] + c[0]*127)/c[1]));
            FpgaSettingsMemory[r].Set(offsetByte);
            Logger.Debug(String.Format("Yoffset Ch {0} set to {1} V = byteval {2}", channel, offset, offsetByte));
        }

        /// <summary>
        /// Sets&uploads the divider and multiplier what are optimal for the requested range
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="minimum"></param>
        /// <param name="maximum"></param>
        public void SetVerticalRange(int channel, float minimum, float maximum)
        {
            //The voltage range for div/mul = 1/1
            //20140808: these seem to be OK: on div0/mult0 the ADC input range is approx 1.3V
            float baseMin = -0.6345f; //V
            float baseMax = 0.6769f; //V

            //Walk through dividers/multipliers till requested range fits
            //this walk assumes it starts with the smallest range, and that range is only increasing
            int dividerIndex = 0;
            int multIndex = 0;
            for (int i = 0; i < rom.computedDividers.Length * rom.computedMultipliers.Length; i++)
            {
                dividerIndex= i / rom.computedMultipliers.Length;
                multIndex = rom.computedMultipliers.Length - (i % rom.computedMultipliers.Length) - 1;
                if (
                    (maximum < baseMax * rom.computedDividers[dividerIndex] / rom.computedMultipliers[multIndex])
                    &&
                    (minimum > baseMin * rom.computedDividers[dividerIndex] / rom.computedMultipliers[multIndex])
                    )
                    break;
            }
            SetDivider(channel, validDividers[dividerIndex]);
            SetMultiplier(channel, validMultipliers[multIndex]);
            channelSettings[channel] = rom.getCalibration(AnalogChannel.list.Where(x => x.Value == channel).First(), validDividers[dividerIndex], validMultipliers[multIndex]);
            SetTriggerAnalog(this.triggerLevel);
        }

		/// <summary>
		/// Set divider of a channel
		/// </summary>
		/// <param name="channel">0 or 1 (channel A or B)</param>
		/// <param name="divider">1, 10 or 100</param>
		#if INTERNAL
		public
		#else
		private
		#endif
		void SetDivider(int channel, double divider)
		{
			validateChannel(channel);
			validateDivider(divider);
            byte div = (byte)(Array.IndexOf(validDividers, divider));
			int bitOffset = channel * 4;
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
		#if INTERNAL
		public
		#else
		private
		#endif
		void SetMultiplier(int channel, double multiplier)
		{
			validateChannel(channel);
			validateMultiplier(multiplier);

			int bitOffset = channel * 4;
			byte mul = (byte)(Array.IndexOf(validMultipliers, multiplier) << 2);
			byte mask = (byte)(0xC << bitOffset);

			byte divMul = FpgaSettingsMemory[REG.DIVIDER_MULTIPLIER].GetByte();
			divMul = (byte)((divMul & ~mask) + (mul << bitOffset));
			FpgaSettingsMemory[REG.DIVIDER_MULTIPLIER].Set(divMul);
		}

#if INTERNAL
        public void SetYOffsetByte(int channel, byte offset)
        {
            validateChannel(channel);
            REG r = (channel == 0) ? REG.CHA_YOFFSET_VOLTAGE : REG.CHB_YOFFSET_VOLTAGE;
            Logger.Debug("Set Y offset for channel " + channel + " to " + offset + " (int value)");
            FpgaSettingsMemory[r].Set(offset);
        }

        /// <summary>
        /// Disable the voltage conversion to have GetVoltages return the raw bytes as sample values (cast to float though)
        /// </summary>
        /// <param name="disable"></param>
        public void SetDisableVoltageConversion(bool disable)
        {
            this.disableVoltageConversion = disable;
        }
#endif

        public void SetCoupling(int channel, Coupling coupling)
        {
            validateChannel(channel);
            STR dc = (channel == 0) ? STR.CHA_DCCOUPLING : STR.CHB_DCCOUPLING;
            bool enableDc = coupling == Coupling.DC;
            Logger.Debug("Set DC coupling for channel " + channel + (enableDc ? " ON" : " OFF"));
            StrobeMemory[dc].Set(enableDc);
        }
        public Coupling GetCoupling(int channel)
        {
            //FIXME: make this part of the header instead of reading it
            validateChannel(channel);
            return this.coupling[channel];
        }

        #endregion

        #region horizontal
        ///<summary>
        ///Set scope trigger level
        ///</summary>
        ///<param name="level">Trigger level in volt</param>
        public void SetTriggerAnalog(float voltage)
        {
            this.triggerLevel = voltage;
            double[] coefficients = channelSettings[GetTriggerChannel()].coefficients;
            REG offsetRegister = GetTriggerChannel() == 1 ? REG.CHB_YOFFSET_VOLTAGE : REG.CHA_YOFFSET_VOLTAGE;
            double level = 0;
            if(coefficients != null)
                level = (voltage - FpgaSettingsMemory[offsetRegister].GetByte() * coefficients[1] - coefficients[2]) / coefficients[0];
            if (level < 0) level = 0;
            if (level > 255) level = 255;

            Logger.Debug(" Set trigger level to " + voltage + "V (" + level + ")");
            FpgaSettingsMemory[REG.TRIGGER_LEVEL].Set((byte)level);
        }
        public void SetForceTrigger()
        {
            StrobeMemory[STR.FORCE_TRIGGER].WriteImmediate(true);
        }
#if INTERNAL
        public void SetTriggerByte(byte level)
        {
            FpgaSettingsMemory[REG.TRIGGER_LEVEL].Set(level);
        }
#endif
        /// <summary>
        /// Choose channel upon which to trigger
        /// </summary>
        /// <param name="channel"></param>
        public void SetTriggerChannel(int channel)
        {
            validateChannel(channel);
            FpgaSettingsMemory[REG.TRIGGER_MODE].Set(
                (byte)(
                    (FpgaSettingsMemory[REG.TRIGGER_MODE].GetByte() & 0xF3) + 
                    (channel << 2)
                )
            );
            Logger.Debug(" Set trigger channel to " + (channel == 0 ? " CH A" : "CH B"));
            SetTriggerAnalog(this.triggerLevel);
        }


        public int GetTriggerChannel()
        {         
            return (FpgaSettingsMemory[REG.TRIGGER_MODE].GetByte() & 0x0C) >> 2;
        }

        /// <summary>
        /// Choose between rising or falling trigger
        /// </summary>
        /// <param name="direction"></param>
        public void SetTriggerDirection(TriggerDirection direction)
        {
            FpgaSettingsMemory[REG.TRIGGER_MODE].Set(
                (byte)(
                    (FpgaSettingsMemory[REG.TRIGGER_MODE].GetByte() & 0xCF) + 
                    (((int)direction << 4) & 0x30)
                    )
            );
            Logger.Debug(" Set trigger channel to " + Enum.GetName(typeof(TriggerDirection), direction));
        }
        public void SetTriggerWidth(uint width)
        {
            FpgaSettingsMemory[REG.TRIGGER_WIDTH].Set((byte)width);
        }
        public uint GetTriggerWidth()
        {
            return (uint)FpgaSettingsMemory[REG.TRIGGER_WIDTH].GetByte();
        }
        public void SetTriggerThreshold(uint threshold)
        {
            FpgaSettingsMemory[REG.TRIGGER_THRESHOLD].Set((byte)threshold);
        }
        public uint GetTriggerThreshold()
        {
            return (uint)FpgaSettingsMemory[REG.TRIGGER_THRESHOLD].GetByte();
        }

        public void SetTriggerMode(TriggerMode mode)
        {
            //Not implemented
        }

        public void SetAcquisitionMode(AcquisitionMode mode)
        {
            FpgaSettingsMemory[REG.TRIGGER_MODE].Set(
                (byte)(
                    (FpgaSettingsMemory[REG.TRIGGER_MODE].GetByte() & 0x3F) +
                    (((int)mode << 6) & 0xC0)
                )
            );
        }
        public void SetAcquisitionRunning(bool running)
        {
            STR s;
            if (running)
                s = STR.ACQ_START;
            else
                s = STR.ACQ_STOP;
            acquisitionRunning = running;
            StrobeMemory[s].WriteImmediate(true);
        }

        public bool GetAcquisitionRunning()
        {
            return Connected && acquisitionRunning;
        }

        public void SetTriggerDigital(Dictionary<DigitalChannel, DigitalTriggerValue> condition)
        {
            //throw new NotImplementedException();
        }

        public void SetTimeRange(double timeRange)
        {
            double defaultTimeRange = GetDefaultTimeRange();
            double timeScaler = timeRange / defaultTimeRange;
            byte acquisitionMultiplePower;
            if (timeScaler > 1)
                acquisitionMultiplePower = (byte)Math.Ceiling(Math.Log(timeScaler, 2));
            else
                acquisitionMultiplePower = 0;
            FpgaSettingsMemory[REG.ACQUISITION_MULTIPLE_POWER].Set(acquisitionMultiplePower);
            //FIXME: REG_VIEW_DECIMATION disabled (always equals ACQUISITION_MULTIPLE_POWER)
            /*
            if(acquisitionMultiplePower > 0)
                FpgaSettingsMemory[REG.VIEW_DECIMATION].Set(acquisitionMultiplePower - 1);
            else
                FpgaSettingsMemory[REG.VIEW_DECIMATION].Set(0);
             */
        }
        ///<summary>
        ///Scope hold off
        ///</summary>
        ///<param name="samples">Store [samples] before trigger</param>
        public void SetTriggerHoldOff(double time)
        {
            holdoff = time;
            Int32 samples = (Int32)(time / (BASE_SAMPLE_PERIOD * Math.Pow(2, FpgaSettingsMemory[REG.INPUT_DECIMATION].GetByte()) ));
            Logger.Debug(" Set trigger holdoff to " + time * 1e6 + "us or " + samples + " samples " );
            FpgaSettingsMemory[REG.TRIGGERHOLDOFF_B0].Set((byte)(samples)); 
            FpgaSettingsMemory[REG.TRIGGERHOLDOFF_B1].Set((byte)(samples >> 8));
            FpgaSettingsMemory[REG.TRIGGERHOLDOFF_B2].Set((byte)(samples >> 16));
            FpgaSettingsMemory[REG.TRIGGERHOLDOFF_B3].Set((byte)(samples >> 24));
        }

        #endregion

        #region other        
        /// <summary>
        /// Returns the timerange when decimation is 1
        /// </summary>
        /// <returns></returns>
        public double GetDefaultTimeRange() {
            //FIXME: don't hardcode
            return BASE_SAMPLE_PERIOD * NUMBER_OF_SAMPLES; 
        }

        public uint GetFpgaFirmwareVersion()
        {
            return (UInt32)(FpgaRom[ROM.FW_GIT0].Read().GetByte() +
                   (UInt32)(FpgaRom[ROM.FW_GIT1].Read().GetByte() << 8) +
                   (UInt32)(FpgaRom[ROM.FW_GIT2].Read().GetByte() << 16) +
                   (UInt32)(FpgaRom[ROM.FW_GIT3].Read().GetByte() << 24));
        }

        #endregion

        #region AWG/LA

        /// <summary>
        /// Set the data with which the AWG runs
        /// </summary>
        /// <param name="data">AWG data</param>
        public void setAwgData(byte[] data)
        {
            if (data.Length != 2048)
                throw new ValidationException("While setting AWG data: data buffer needs to be of length 2048, got " + data.Length);

            hardwareInterface.SetControllerRegister(HardwareInterfaces.ScopeController.AWG, 0, data);
        }

        public void setAwgEnabled(bool enable)
        {
            //Disable logic analyser in case AWG is being enabled
            if(enable)
                StrobeMemory[STR.LA_ENABLE].WriteImmediate(false);
            StrobeMemory[STR.AWG_ENABLE].WriteImmediate(enable);
        }

        #endregion

    }
}
