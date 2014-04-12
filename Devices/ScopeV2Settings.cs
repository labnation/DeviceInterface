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
        private void validateChannel(uint ch)
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
            StrobeMemory.GetRegister(STR.SCOPE_UPDATE).Set(0);
            StrobeMemory.WriteSingle(STR.SCOPE_UPDATE);
            StrobeMemory.GetRegister(STR.SCOPE_UPDATE).Set(1);
            StrobeMemory.WriteSingle(STR.SCOPE_UPDATE);
        }
        #endregion

        #region vertical

        /// <summary>
        /// Sets vertical offset of a channel
        /// </summary>
        /// <param name="channel">0 or 1 (channel A or B)</param>
        /// <param name="offset">Vertical offset in Volt</param>
        public void SetYOffset(uint channel, float offset)
        {
            //FIXME: convert offset to byte value
            REG r = (channel == 0) ? REG.CHA_YOFFSET_VOLTAGE : REG.CHB_YOFFSET_VOLTAGE;
            Logger.AddEntry(this, LogMessageType.ScopeSettings, "Set DC coupling for channel " + channel + " to " + offset + "V");
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
        public void SetDivider(uint channel, uint divider)
        {
            validateChannel(channel);
            validateDivider(divider);
            STR d1   = (channel == 0) ? STR.CHA_DIV1   : STR.CHB_DIV1;
            STR d10  = (channel == 0) ? STR.CHA_DIV10  : STR.CHB_DIV10;
            STR d100 = (channel == 0) ? STR.CHA_DIV100 : STR.CHB_DIV100;
            StrobeMemory.GetRegister(d1).Set((byte)((divider == 1) ? 1 : 0));
            StrobeMemory.GetRegister(d10).Set((byte)((divider == 10) ? 1 : 0));
            StrobeMemory.GetRegister(d100).Set((byte)((divider == 100) ? 1 : 0));
            StrobeMemory.WriteSingle(d1);
            StrobeMemory.WriteSingle(d10);
            StrobeMemory.WriteSingle(d100);
        }

        ///<summary>
        ///Set multiplier of a channel
        ///</summary>
        ///<param name="channel">0 or 1 (channel A or B)</param>
        ///<param name="multiplier">Set input stage multiplier (?? or ??)</param>
        public void SetMultiplier(uint channel, uint multiplier)
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

        ///<summary>
        ///Enable DC coupling
        ///</summary>
        ///<param name="channel">0 or 1 (channel A or B)</param>
        ///<param name="enableDc">true for DC coupling, false for AC coupling</param>
        public void SetEnableDcCoupling(uint channel, bool enableDc)
        {
            validateChannel(channel);            
            STR dc = (channel == 0) ? STR.CHA_DCCOUPLING: STR.CHB_DCCOUPLING;
            Logger.AddEntry(this, LogMessageType.ScopeSettings, "Set DC coupling for channel " + channel + (enableDc ? " ON" : " OFF"));
            StrobeMemory.GetRegister(dc).Set((byte)(enableDc ? 1 : 0));
            StrobeMemory.WriteSingle(dc);
        }

        #endregion

        #region horizontal
        ///<summary>
        ///Set scope trigger level
        ///</summary>
        ///<param name="level">Trigger level in volt</param>
        public void SetTriggerLevel(float voltage)
        {
            float level = (voltage - FpgaSettingsMemory.GetRegister(REG.CHB_YOFFSET_VOLTAGE).GetByte() * calibrationCoefficients[1] - calibrationCoefficients[2]) / calibrationCoefficients[0];
            if (level < 0) level = 0;
            if (level > 255) level = 255;

            Logger.AddEntry(this, LogMessageType.ScopeSettings, " Set trigger level to " + voltage + "V (" + level + ")");
            FpgaSettingsMemory.GetRegister(REG.TRIGGERLEVEL).Set((byte)level);
            FpgaSettingsMemory.WriteSingle(REG.TRIGGERLEVEL);
            toggleUpdateStrobe();
        }
        /// <summary>
        /// Choose channel upon which to trigger
        /// </summary>
        /// <param name="channel"></param>
        public void SetTriggerChannel(uint channel)
        {
            validateChannel(channel);
            StrobeMemory.GetRegister(STR.TRIGGER_CHB).Set((byte)(channel == 0 ? 0 : 1));
            Logger.AddEntry(this, LogMessageType.ScopeSettings, " Set trigger channel to " + (channel == 0 ? " CH A" : "CH B"));
            StrobeMemory.WriteSingle(STR.TRIGGER_CHB);
            toggleUpdateStrobe();
        }

        /// <summary>
        /// Choose between rising or falling trigger
        /// </summary>
        /// <param name="direction"></param>
        public void SetTriggerDirection(TriggerDirection direction)
        {
            StrobeMemory.GetRegister(STR.TRIGGER_FALLING).Set((byte)(direction == TriggerDirection.FALLING ? 1 : 0));
            Logger.AddEntry(this, LogMessageType.ScopeSettings, " Set trigger channel to " + Enum.GetName(typeof(TriggerDirection), direction));
            StrobeMemory.WriteSingle(STR.TRIGGER_CHB);
            toggleUpdateStrobe();
        }

        /// <summary>
        /// Only store every [decimation]-th sample
        ///</summary>
        ///<param name="decimation">Store every [decimation]nt sample</param>
        public void SetDecimation(uint decimation)
        {
            if (decimation > UInt16.MaxValue)
                throw new ValidationException("Decimation too large");
            //FIXME: validate
            FpgaSettingsMemory.GetRegister(REG.SAMPLECLOCKDIVIDER_B0).Set((byte)(decimation & 0xFF));
            FpgaSettingsMemory.GetRegister(REG.SAMPLECLOCKDIVIDER_B1).Set((byte)((decimation >> 8) & 0xFF));
            FpgaSettingsMemory.WriteSingle(REG.SAMPLECLOCKDIVIDER_B0);
            FpgaSettingsMemory.WriteSingle(REG.SAMPLECLOCKDIVIDER_B1);
            toggleUpdateStrobe();
        }
        ///<summary>
        ///Enable free running (don't wait for trigger)
        ///</summary>
        ///<param name="freerunning">Whether to enable free running mode</param>
        public void SetEnableFreeRunning(bool freerunning)
        {
            StrobeMemory.GetRegister(STR.FREE_RUNNING).Set((byte)(freerunning ? 1 : 0));
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
            Logger.AddEntry(this, LogMessageType.ScopeSettings, " Set trigger holdoff to " + time * 1e6 + "us or " + samples + " samples " );
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
            StrobeMemory.GetRegister(STR.CHB_ENABLECALIB).Set((byte)(enableCalibration ? 1 : 0));
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

            //raise global reset to reset RAM address counter, and to make sure the RAM switching is safe
            StrobeMemory.GetRegister(STR.GLOBAL_RESET).Set((byte)1);
            StrobeMemory.WriteSingle(STR.GLOBAL_RESET);

            //lower global reset
            StrobeMemory.GetRegister(STR.GLOBAL_RESET).Set((byte)0);
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
            StrobeMemory.GetRegister(STR.GLOBAL_RESET).Set((byte)0);
            StrobeMemory.WriteSingle(STR.GLOBAL_RESET);
        }

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
