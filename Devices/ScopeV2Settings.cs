using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.DeviceMemories;

namespace ECore.Devices
{
    partial class ScopeV2
    {
        public static readonly int[] validChannels = { 0, 1 };
        public static readonly double[] validDividers = { 1, 6, 36 };
        public static readonly double[] validMultipliers = { 1.1, 2, 3 };

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
            StrobeMemory.GetRegister(STR.SCOPE_UPDATE).Set(false);
            StrobeMemory.WriteSingle(STR.SCOPE_UPDATE);
            StrobeMemory.GetRegister(STR.SCOPE_UPDATE).Set(true);
            StrobeMemory.WriteSingle(STR.SCOPE_UPDATE);
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
            Logger.AddEntry(this, LogLevel.Debug, "Set DC coupling for channel " + channel + " to " + offset + "V");
            //Offset: 0V --> 150 - swing +-0.9V
            byte offsetByte = (byte)Math.Min(byte.MaxValue, Math.Max(byte.MinValue, ((offset * 68.2) - 16.5)));
            FpgaSettingsMemory.GetRegister(r).Set(offsetByte);
            FpgaSettingsMemory.WriteSingle(r);
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

			byte divMul = FpgaSettingsMemory.GetRegister(REG.DIVIDER_MULTIPLIER).GetByte();
			divMul = (byte)((divMul & ~mask) + (div << bitOffset));
			FpgaSettingsMemory.GetRegister(REG.DIVIDER_MULTIPLIER).Set(divMul);
			FpgaSettingsMemory.WriteSingle(REG.DIVIDER_MULTIPLIER);
            System.Threading.Thread.Sleep(150);
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

			byte divMul = FpgaSettingsMemory.GetRegister(REG.DIVIDER_MULTIPLIER).GetByte();
			divMul = (byte)((divMul & ~mask) + (mul << bitOffset));
			FpgaSettingsMemory.GetRegister(REG.DIVIDER_MULTIPLIER).Set(divMul);
			FpgaSettingsMemory.WriteSingle(REG.DIVIDER_MULTIPLIER);
		}

#if INTERNAL
        public void SetYOffsetByte(int channel, byte offset)
        {
            validateChannel(channel);
            REG r = (channel == 0) ? REG.CHA_YOFFSET_VOLTAGE : REG.CHB_YOFFSET_VOLTAGE;
            Logger.AddEntry(this, LogLevel.Debug, "Set DC coupling for channel " + channel + " to " + offset + "V");
            FpgaSettingsMemory.GetRegister(r).Set(offset);
            FpgaSettingsMemory.WriteSingle(r);
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
            Logger.AddEntry(this, LogLevel.Debug, "Set DC coupling for channel " + channel + (enableDc ? " ON" : " OFF"));
            StrobeMemory.GetRegister(dc).Set(enableDc);
            StrobeMemory.WriteSingle(dc);
        }
        public Coupling GetCoupling(int channel)
        {
            validateChannel(channel);
            STR dc = (channel == 0) ? STR.CHA_DCCOUPLING : STR.CHB_DCCOUPLING;
            StrobeMemory.ReadSingle(dc);
            bool dcEnabled = StrobeMemory.GetRegister(dc).GetBool();
            return dcEnabled ? Coupling.DC : Coupling.AC;
        }

        #endregion

        #region horizontal
        ///<summary>
        ///Set scope trigger level
        ///</summary>
        ///<param name="level">Trigger level in volt</param>
        public void SetTriggerAnalog(float voltage)
        {
            float level = (voltage - FpgaSettingsMemory.GetRegister(REG.CHB_YOFFSET_VOLTAGE).GetByte() * calibrationCoefficients[1] - calibrationCoefficients[2]) / calibrationCoefficients[0];
            if (level < 0) level = 0;
            if (level > 255) level = 255;

            Logger.AddEntry(this, LogLevel.Debug, " Set trigger level to " + voltage + "V (" + level + ")");
            FpgaSettingsMemory.GetRegister(REG.TRIGGERLEVEL).Set((byte)level);
            FpgaSettingsMemory.WriteSingle(REG.TRIGGERLEVEL);
            toggleUpdateStrobe();
        }
        /// <summary>
        /// Choose channel upon which to trigger
        /// </summary>
        /// <param name="channel"></param>
        public void SetTriggerChannel(int channel)
        {
            validateChannel(channel);
            StrobeMemory.GetRegister(STR.TRIGGER_CHB).Set(channel != 0);
            Logger.AddEntry(this, LogLevel.Debug, " Set trigger channel to " + (channel == 0 ? " CH A" : "CH B"));
            StrobeMemory.WriteSingle(STR.TRIGGER_CHB);
            toggleUpdateStrobe();
        }

        /// <summary>
        /// Choose between rising or falling trigger
        /// </summary>
        /// <param name="direction"></param>
        public void SetTriggerDirection(TriggerDirection direction)
        {
            StrobeMemory.GetRegister(STR.TRIGGER_FALLING).Set(direction == TriggerDirection.FALLING);
            Logger.AddEntry(this, LogLevel.Debug, " Set trigger channel to " + Enum.GetName(typeof(TriggerDirection), direction));
            StrobeMemory.WriteSingle(STR.TRIGGER_CHB);
            toggleUpdateStrobe();
        }

        public void SetTriggerMode(TriggerMode mode)
        {
            StrobeMemory.GetRegister(STR.FREE_RUNNING).Set(mode == TriggerMode.FREE_RUNNING);
            StrobeMemory.WriteSingle(STR.FREE_RUNNING);
            toggleUpdateStrobe();
        }

        public void SetAcquisitionMode(AcquisitionMode mode)
        {
            bool single = mode == AcquisitionMode.SINGLE;

            StrobeMemory.GetRegister(STR.ACQ_SINGLE).Set(single);
            StrobeMemory.WriteSingle(STR.ACQ_SINGLE);
        }
        public void SetAcuisitionRunning(bool running)
        {
            STR s;
            if (running)
                s = STR.ACQ_START;
            else
                s = STR.ACQ_STOP;
            acquisitionRunning = running;
            StrobeMemory.GetRegister(s).Set(true);
            StrobeMemory.WriteSingle(s);
        }

        public bool GetAcquisitionRunning()
        {
            return acquisitionRunning;
        }

        public void SetTriggerDigital(Dictionary<DigitalChannel, DigitalTriggerValue> condition)
        {
            //throw new NotImplementedException();
        }

        /// <summary>
        /// Only store every [decimation]-th sample
        ///</summary>
        ///<param name="decimation">Store every [decimation]nt sample</param>
        public void SetTimeRange(double timeRange)
        {
            //throw new NotImplementedException();
            /*
            if (decimation > UInt16.MaxValue)
                throw new ValidationException("Decimation too large");
            //FIXME: validate
            FpgaSettingsMemory.GetRegister(REG.SAMPLECLOCKDIVIDER_B0).Set((byte)(decimation & 0xFF));
            FpgaSettingsMemory.GetRegister(REG.SAMPLECLOCKDIVIDER_B1).Set((byte)((decimation >> 8) & 0xFF));
            FpgaSettingsMemory.WriteSingle(REG.SAMPLECLOCKDIVIDER_B0);
            FpgaSettingsMemory.WriteSingle(REG.SAMPLECLOCKDIVIDER_B1);
            toggleUpdateStrobe();
             */
        }
        ///<summary>
        ///Enable free running (don't wait for trigger)
        ///</summary>
        ///<param name="freerunning">Whether to enable free running mode</param>
        public void SetEnableFreeRunning(bool freerunning)
        {
            StrobeMemory.GetRegister(STR.FREE_RUNNING).Set(freerunning);
            StrobeMemory.WriteSingle(STR.FREE_RUNNING);
            toggleUpdateStrobe();
        }
        ///<summary>
        ///Scope hold off
        ///</summary>
        ///<param name="samples">Store [samples] before trigger</param>
        public void SetTriggerHoldOff(double time)
        {
            Int16 samples = (Int16)(time / SAMPLE_PERIOD);
            Logger.AddEntry(this, LogLevel.Debug, " Set trigger holdoff to " + time * 1e6 + "us or " + samples + " samples " );
            FpgaSettingsMemory.GetRegister(REG.TRIGGERHOLDOFF_B0).Set((byte)(samples)); 
            FpgaSettingsMemory.GetRegister(REG.TRIGGERHOLDOFF_B1).Set((byte)(samples >> 8));
            FpgaSettingsMemory.WriteSingle(REG.TRIGGERHOLDOFF_B0);
            FpgaSettingsMemory.WriteSingle(REG.TRIGGERHOLDOFF_B1);
            toggleUpdateStrobe();
        }

        #endregion

        #region other        
        /// <summary>
        /// Returns the timerange when decimation is 1
        /// </summary>
        /// <returns></returns>
        public double GetDefaultTimeRange() {
            //FIXME: don't hardcode
            return SAMPLE_PERIOD * NUMBER_OF_SAMPLES; 
        }

        public double GetSamplePeriod()
        {
            return SAMPLE_PERIOD;
        }

        public int GetNumberOfSamples()
        {
            return (int)NUMBER_OF_SAMPLES;
        }

        #endregion

        #region AWG/LA
#if false
        /// <summary>
        /// Set the data with which the AWG runs
        /// </summary>
        /// <param name="data">AWG data</param>
        public void setAwgData(byte[] data)
        {
            if (data.Length != 2048)
                throw new ValidationException("While setting AWG data: data buffer needs to be of length 2048, got " + data.Length);

            //raise global reset to reset RAM address counter, and to make sure the RAM switching is safe
            StrobeMemory.GetRegister(STR.GLOBAL_RESET).Set(true);
            StrobeMemory.WriteSingle(STR.GLOBAL_RESET);

            //lower global reset
            StrobeMemory.GetRegister(STR.GLOBAL_RESET).Set(false);
            StrobeMemory.WriteSingle(STR.GLOBAL_RESET);

            //break data up into blocks of 8bytes
            int blockSize = 8;
            int fullLength = data.Length;
            int blockCounter = 0;

            while (blockCounter * blockSize < fullLength) // as long as not all data has been sent
            {

                ///////////////////////////////////////////////////////////////////////////
                //////Start sending data
                byte[] toSend = new byte[5 + blockSize];

                //prep header
                int i = 0;
                toSend[i++] = 123; //message for FPGA
                toSend[i++] = 10; //I2C send
                toSend[i++] = (byte)(blockSize + 2); //data and 2 more bytes: the FPGA I2C address, and the register address inside the FPGA
                toSend[i++] = (byte)(7 << 1); //first I2C byte: FPGA i2c address for RAM writing(7) + '0' as LSB, indicating write operation
                toSend[i++] = (byte)0; //second I2C byte: dummy!

                //append data to be sent
                for (int c = 0; c < blockSize; c++)
                    toSend[i++] = data[blockCounter * blockSize + c];

                hardwareInterface.WriteControlBytes(toSend);

                blockCounter++;
            }

            //lower global reset
            StrobeMemory.GetRegister(STR.GLOBAL_RESET).Set(false);
            StrobeMemory.WriteSingle(STR.GLOBAL_RESET);
        }
#endif

        #endregion

    }
}
