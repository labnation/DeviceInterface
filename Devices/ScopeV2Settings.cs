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
            StrobeMemory.GetRegister(STR.SCOPE_UPDATE).Set(false).Write();
            StrobeMemory.GetRegister(STR.SCOPE_UPDATE).Set(true).Write();
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
            FpgaSettingsMemory.GetRegister(r).Set(offsetByte).Write();
            Logger.Debug(String.Format("Yoffset Ch {0} set to {1} V = byteval {2}", channel, offset, offsetByte));
        }

        public void SetVerticalRange(int channel, float minimum, float maximum)
        {
            //The voltage range for div/mul = 1/1
            float baseMin = -0.6345f; //V
            float baseMax = 0.6769f; //V

            //Walk through dividers/multipliers till requested range fits
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

			byte divMul = FpgaSettingsMemory.GetRegister(REG.DIVIDER_MULTIPLIER).GetByte();
			divMul = (byte)((divMul & ~mask) + (div << bitOffset));
			FpgaSettingsMemory.GetRegister(REG.DIVIDER_MULTIPLIER).Set(divMul).Write();
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
			FpgaSettingsMemory.GetRegister(REG.DIVIDER_MULTIPLIER).Set(divMul).Write();
		}

#if INTERNAL
        public void SetYOffsetByte(int channel, byte offset)
        {
            validateChannel(channel);
            REG r = (channel == 0) ? REG.CHA_YOFFSET_VOLTAGE : REG.CHB_YOFFSET_VOLTAGE;
            Logger.Debug("Set DC coupling for channel " + channel + " to " + offset + "V");
            FpgaSettingsMemory.GetRegister(r).Set(offset).Write();
        }

        /// <summary>
        /// Disable the voltage conversion to have GetVoltages return the raw bytes as sample values (cast to float though)
        /// </summary>
        /// <param name="disable"></param>
#endif
#if DEBUG
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
            StrobeMemory.GetRegister(dc).Set(enableDc).Write();
        }
        public Coupling GetCoupling(int channel)
        {
            validateChannel(channel);
            STR dc = (channel == 0) ? STR.CHA_DCCOUPLING : STR.CHB_DCCOUPLING;
            bool dcEnabled = StrobeMemory.GetRegister(dc).Read().GetBool();
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
            this.triggerLevel = voltage;
            double[] coefficients = channelSettings[StrobeMemory.GetRegister(STR.TRIGGER_CHB).GetBool() ? 1 : 0].coefficients;
            REG offsetRegister = StrobeMemory.GetRegister(STR.TRIGGER_CHB).GetBool() ? REG.CHB_YOFFSET_VOLTAGE : REG.CHA_YOFFSET_VOLTAGE;
            double level = (voltage - FpgaSettingsMemory.GetRegister(offsetRegister).GetByte() * coefficients[1] - coefficients[2]) / coefficients[0];
            if (level < 0) level = 0;
            if (level > 255) level = 255;

            Logger.Debug(" Set trigger level to " + voltage + "V (" + level + ")");
            FpgaSettingsMemory[REG.TRIGGERLEVEL].Write((byte)level);
            toggleUpdateStrobe();
        }
        /// <summary>
        /// Choose channel upon which to trigger
        /// </summary>
        /// <param name="channel"></param>
        public void SetTriggerChannel(int channel)
        {
            validateChannel(channel);
            StrobeMemory.GetRegister(STR.TRIGGER_CHB).Set(channel != 0).Write();
            Logger.Debug(" Set trigger channel to " + (channel == 0 ? " CH A" : "CH B"));
            SetTriggerAnalog(this.triggerLevel);
            //toggleUpdateStrobe();
        }

        /// <summary>
        /// Choose between rising or falling trigger
        /// </summary>
        /// <param name="direction"></param>
        public void SetTriggerDirection(TriggerDirection direction)
        {
            StrobeMemory.GetRegister(STR.TRIGGER_FALLING).Set(direction == TriggerDirection.FALLING).Write();
            Logger.Debug(" Set trigger channel to " + Enum.GetName(typeof(TriggerDirection), direction));
            toggleUpdateStrobe();
        }

        public void SetTriggerMode(TriggerMode mode)
        {
            StrobeMemory.GetRegister(STR.FREE_RUNNING).Set(mode == TriggerMode.FREE_RUNNING).Write();
            toggleUpdateStrobe();
        }

        public void SetAcquisitionMode(AcquisitionMode mode)
        {
            bool single = mode == AcquisitionMode.SINGLE;

            StrobeMemory.GetRegister(STR.ACQ_SINGLE).Set(single).Write();
        }
        public void SetAcuisitionRunning(bool running)
        {
            STR s;
            if (running)
                s = STR.ACQ_START;
            else
                s = STR.ACQ_STOP;
            acquisitionRunning = running;
            StrobeMemory.GetRegister(s).Set(true).Write();
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
            StrobeMemory.GetRegister(STR.FREE_RUNNING).Set(freerunning).Write();
            toggleUpdateStrobe();
        }
        ///<summary>
        ///Scope hold off
        ///</summary>
        ///<param name="samples">Store [samples] before trigger</param>
        public void SetTriggerHoldOff(double time)
        {
            Int16 samples = (Int16)(time / SAMPLE_PERIOD);
            Logger.Debug(" Set trigger holdoff to " + time * 1e6 + "us or " + samples + " samples " );
            FpgaSettingsMemory.GetRegister(REG.TRIGGERHOLDOFF_B0).Set((byte)(samples)).Write(); 
            FpgaSettingsMemory.GetRegister(REG.TRIGGERHOLDOFF_B1).Set((byte)(samples >> 8)).Write();
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

        public uint GetFpgaFirmwareVersion()
        {
            return
                (uint)(
                (FpgaRom.GetRegister(ROM.FW_GIT0).Read().GetByte() <<  0) +
                (FpgaRom.GetRegister(ROM.FW_GIT1).Read().GetByte() <<  8) +
                (FpgaRom.GetRegister(ROM.FW_GIT2).Read().GetByte() << 16) +
                (FpgaRom.GetRegister(ROM.FW_GIT3).Read().GetByte() << 24));
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
