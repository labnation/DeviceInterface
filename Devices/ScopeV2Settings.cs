using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.DeviceMemories;

namespace ECore.Devices
{
    partial class ScopeV2
    {
        //FIXME: might have to be moved to more general class
        #region helpers
        private byte voltToByte(float volt)
        {
            //FIXME: implement this
            return (byte)((int)volt);
        }
        private void validateChannel(int ch)
        {
            if (ch != 0 && ch != 1) 
                throw new ValidationException("Channel must be 0 or 1");
        }
        private void validateDivider(uint div)
        {
            if (div != 1 && div != 10 && div != 100) 
                throw new ValidationException("Divider must be 1, 10 or 100");
        }
        private void validateMultiplier(uint mul)
        {
            throw new ValidationException("I have no idea what a valid multiplier would be. I do know about dividers. Try that instead...");
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
            //FIXME: convert offset to byte value
            REG r = (channel == 0) ? REG.CHA_YOFFSET_VOLTAGE : REG.CHB_YOFFSET_VOLTAGE;
            Logger.AddEntry(this, LogLevel.Debug, "Set DC coupling for channel " + channel + " to " + offset + "V");
            //Offset: 0V --> 150 - swing +-0.9V
            byte offsetByte = (byte)((offset * 68.2) - 16.5);
            FpgaSettingsMemory.GetRegister(r).Set(offsetByte);
            FpgaSettingsMemory.WriteSingle(r);
        }

        /// <summary>
        /// Set divider of a channel
        /// </summary>
        /// <param name="channel">0 or 1 (channel A or B)</param>
        /// <param name="divider">1, 10 or 100</param>
        public void SetDivider(int channel, uint divider)
        {
            validateChannel(channel);
            validateDivider(divider);
            byte pow = (byte)Math.Log10(divider);
            STR b0   = (channel == 0) ? STR.CHA_DIV_B0   : STR.CHB_DIV_B0;
            STR b1  = (channel == 0) ? STR.CHA_DIV_B1  : STR.CHB_DIV_B1;
            StrobeMemory.GetRegister(b0).Set(Utils.IsBitSet(pow, 0));
            StrobeMemory.GetRegister(b1).Set(Utils.IsBitSet(pow, 1));

            StrobeMemory.WriteSingle(b0);
            StrobeMemory.WriteSingle(b1);
        }

        ///<summary>
        ///Set multiplier of a channel
        ///</summary>
        ///<param name="channel">0 or 1 (channel A or B)</param>
        ///<param name="multiplier">Set input stage multiplier (?? or ??)</param>
        public void SetMultiplier(int channel, uint multiplier)
        {
            validateChannel(channel);
            validateMultiplier(multiplier);
            STR m1 = (channel == 0) ? STR.CHA_MULT1 : STR.CHB_MULT1;
            STR m2 = (channel == 0) ? STR.CHA_MULT2 : STR.CHB_MULT2;
            STR m3 = (channel == 0) ? STR.CHA_MULT3 : STR.CHB_MULT3;
            //FIXME: do something with the registers
            /*
            strobeMemory.GetRegister(m1).Set(?);
            strobeMemory.GetRegister(m2).Set(?);
            strobeMemory.GetRegister(m3).Set(?);
            strobeMemory.WriteSingle(m1);
            strobeMemory.WriteSingle(m2);
            strobeMemory.WriteSingle(m3);
            */
        }

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
            //FIXME
            //throw new NotImplementedException();
        }
        public void SetAcuisitionRunning(bool running)
        {
            //FIXME
            //throw new NotImplementedException();
        }

        public bool GetAcuisitionRunning()
        {
            //FIXME
            //throw new NotImplementedException();
            return false;
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
        /// Enable or disable the calibration voltage
        /// </summary>
        /// <param name="enableCalibration"></param>
        public void SetEnableCalib(bool enableCalibration)
        {
            StrobeMemory.GetRegister(STR.CHB_ENABLECALIB).Set(enableCalibration);
            StrobeMemory.WriteSingle(STR.CHB_ENABLECALIB);
        }

        /// <summary>
        /// Sets the calibration voltage on a channel
        /// </summary>
        /// <param name="voltage">The desired voltage</param>
        public void SetCalibrationVoltage(float voltage)
        {
            FpgaSettingsMemory.GetRegister(REG.CALIB_VOLTAGE).Set(voltToByte(voltage));
            FpgaSettingsMemory.WriteSingle(REG.CALIB_VOLTAGE);
        }
        
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

        public bool GetEnableLogicAnalyser()
        {
            //FIXME: implement this
            return false;
        }
        #endregion
        
        //FIXME: guard this so it's only in internal builds
        #region develop
        /// <summary>
        /// Disable the voltage conversion to have GetVoltages return the raw bytes as sample values (cast to float though)
        /// </summary>
        /// <param name="disable"></param>
        public void SetDisableVoltageConversion(bool disable)
        {
            this.disableVoltageConversion = true;
        }
        #endregion

    }
}
