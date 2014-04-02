using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.DeviceMemories;

namespace ECore.DeviceImplementations
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
            fpgaSettingsMemory.GetRegister(r).InternalValue = (byte)offset;
            fpgaSettingsMemory.WriteSingle(r);
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
            strobeMemory.GetRegister(d1).Set((byte)((divider == 1) ? 1 : 0));
            strobeMemory.GetRegister(d10).Set((byte)((divider == 10) ? 1 : 0));
            strobeMemory.GetRegister(d100).Set((byte)((divider == 100) ? 1 : 0));
            strobeMemory.WriteSingle(d1);
            strobeMemory.WriteSingle(d10);
            strobeMemory.WriteSingle(d100);
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
            strobeMemory.GetRegister(dc).Set((byte)(enableDc ? 1 : 0));
            strobeMemory.WriteSingle(dc);
        }

        #endregion

        #region horizontal
        ///<summary>
        ///Set scope trigger level
        ///</summary>
        ///<param name="level">Trigger level in volt</param>
        public void SetTriggerLevel(float voltage)
        {
            float level = (voltage - fpgaSettingsMemory.GetRegister(REG.CHB_YOFFSET_VOLTAGE).InternalValue * calibrationCoefficients[1] - calibrationCoefficients[2]) / calibrationCoefficients[0];
            if (level < 0) level = 0;
            if (level > 255) level = 255;

            Logger.AddEntry(this, LogMessageType.CommandToDevice, "Set triglevel to " + level);
            fpgaSettingsMemory.GetRegister(REG.TRIGGERLEVEL).Set((byte)level);
            fpgaSettingsMemory.WriteSingle(REG.TRIGGERLEVEL);
        }
        /// <summary>
        /// Choose channel upon which to trigger
        /// </summary>
        /// <param name="channel"></param>
        public void SetTriggerChannel(uint channel)
        {
            validateChannel(channel);
            //FIXME: throw new NotImplementedException();
        }

        /// <summary>
        /// Choose between rising or falling trigger
        /// </summary>
        /// <param name="direction"></param>
        public void SetTriggerDirection(TriggerDirection direction)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Only store every [decimation]-th sample
        /// </summary>
        /// <param name="decimation"></param>
        public void SetDecimation(uint decimation)
        {
            //FIXME: throw new NotImplementedException();
        }

        ///<summary>
        ///Set scope sample decimation
        ///</summary>
        ///<param name="decimation">Store every [decimation]nt sample</param>
        public void SetDecimation(UInt16 decimation)
        {
            //FIXME: validate
            fpgaSettingsMemory.GetRegister(REG.SAMPLECLOCKDIVIDER_B0).Set((byte)(decimation & 0xFF));
            fpgaSettingsMemory.GetRegister(REG.SAMPLECLOCKDIVIDER_B1).Set((byte)((decimation >> 8) & 0xFF));
            fpgaSettingsMemory.WriteSingle(REG.SAMPLECLOCKDIVIDER_B0);
            fpgaSettingsMemory.WriteSingle(REG.SAMPLECLOCKDIVIDER_B1);
        }
        ///<summary>
        ///Enable free running (don't wait for trigger)
        ///</summary>
        ///<param name="freerunning">Whether to enable free running mode</param>
        public void SetEnableFreeRunning(bool freerunning)
        {
            strobeMemory.GetRegister(STR.FREE_RUNNING).Set((byte)(freerunning ? 1 : 0));
            strobeMemory.WriteSingle(STR.FREE_RUNNING);
        }
        ///<summary>
        ///Scope hold off
        ///</summary>
        ///<param name="samples">Store [samples] before trigger</param>
        public void SetTriggerHoldOff(double time)
        {
            //FIXME: throw new NotImplementedException();
            /*
            if (samples < 0 || samples > 2047)
                throw new ValidationException("Trigger hold off must be between 0 and 2047");
            
            fpgaSettingsMemory.GetRegister(REG.TRIGGERHOLDOFF_B0).Set((byte)(samples)); 
            fpgaSettingsMemory.GetRegister(REG.TRIGGERHOLDOFF_B1).Set((byte)(samples >> 8));
            fpgaSettingsMemory.WriteSingle(REG.TRIGGERHOLDOFF_B0);
            fpgaSettingsMemory.WriteSingle(REG.TRIGGERHOLDOFF_B1);
             */
        }

        #endregion

        #region other
        /// <summary>
        /// Enable or disable the calibration voltage
        /// </summary>
        /// <param name="enableCalibration"></param>
        public void SetEnableCalib(bool enableCalibration)
        {
            strobeMemory.GetRegister(STR.CHB_ENABLECALIB).Set((byte)(enableCalibration ? 1 : 0));
            strobeMemory.WriteSingle(STR.CHB_ENABLECALIB);
        }

        /// <summary>
        /// Sets the calibration voltage on a channel
        /// </summary>
        /// <param name="voltage">The desired voltage</param>
        public void SetCalibrationVoltage(float voltage)
        {
            fpgaSettingsMemory.GetRegister(REG.CALIB_VOLTAGE).Set(voltToByte(voltage));
            fpgaSettingsMemory.WriteSingle(REG.CALIB_VOLTAGE);
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
            strobeMemory.GetRegister(STR.GLOBAL_RESET).Set((byte)1);
            strobeMemory.WriteSingle(STR.GLOBAL_RESET);

            //save previous ram config
            fpgaSettingsMemory.ReadSingle(REG.RAM_CONFIGURATION);
            byte previousRamConfiguration = fpgaSettingsMemory.GetRegister(REG.RAM_CONFIGURATION).Get();

            //set ram config to I2C input
            fpgaSettingsMemory.GetRegister(REG.RAM_CONFIGURATION).Set((byte)2); //sets RAM0 to I2C input
            fpgaSettingsMemory.WriteSingle(REG.RAM_CONFIGURATION);

            //lower global reset
            strobeMemory.GetRegister(STR.GLOBAL_RESET).Set((byte)0);
            strobeMemory.WriteSingle(STR.GLOBAL_RESET);

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

                eDevice.DeviceImplementation.hardwareInterface.WriteControlBytes(toSend);

                blockCounter++;
            }

            //set ram config to original state
            fpgaSettingsMemory.GetRegister(REG.RAM_CONFIGURATION).Set(previousRamConfiguration); //sets RAM0 to I2C input
            fpgaSettingsMemory.WriteSingle(REG.RAM_CONFIGURATION);

            //lower global reset
            strobeMemory.GetRegister(STR.GLOBAL_RESET).Set((byte)0);
            strobeMemory.WriteSingle(STR.GLOBAL_RESET);
        }

        public bool GetEnableLogicAnalyser()
        {
            //FIXME: implement this
            return false;
        }
        #endregion

        #region the rest

        //FIXME: turn into a setting getter
        public int FreqDivider
        {
            get
            {
                fpgaSettingsMemory.ReadSingle(REG.SAMPLECLOCKDIVIDER_B1);
                fpgaSettingsMemory.ReadSingle(REG.SAMPLECLOCKDIVIDER_B0);
                return fpgaSettingsMemory.GetRegister(REG.SAMPLECLOCKDIVIDER_B1).InternalValue << 8 + fpgaSettingsMemory.GetRegister(REG.SAMPLECLOCKDIVIDER_B0).InternalValue + 1;
            }
        }

        //FIXME: turn into a setting getter
        /*
        public int GetTriggerHoldoff()
        {
            fpgaSettingsMemory.ReadSingle(REG.TRIGGERHOLDOFF_B1);
            fpgaSettingsMemory.ReadSingle(REG.TRIGGERHOLDOFF_B0);
            int msb = fpgaSettingsMemory.GetRegister(REG.TRIGGERHOLDOFF_B1).InternalValue;
            msb = msb << 8;
            int lsb = fpgaSettingsMemory.GetRegister(REG.TRIGGERHOLDOFF_B0).InternalValue;
            return msb + lsb + 1;
        }
         * */
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
