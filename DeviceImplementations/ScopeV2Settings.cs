using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.EFuctionalities;
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

        /// <summary>Sets vertical offset of a channel</summary
        /// <param name="channel">0 or 1 (channel A or B)</param>
        /// <param name="offset">Vertical offset in Volt</param>
        public void SetYOffset(uint channel, float offset)
        {
            REG r = (channel == 0) ? REG.CHA_YOFFSET_VOLTAGE : REG.CHB_YOFFSET_VOLTAGE;
            fpgaSettingsMemory.GetRegister(r).InternalValue = (byte)offset;
            fpgaSettingsMemory.WriteSingle(r);
        }

        /// <summary>Set divider of a channel</summary
        /// <param name="channel">0 or 1 (channel A or B)</param>
        /// <param name="divider">1, 10 or 100</param>
        public void SetDivider(uint channel, uint divider)
        {
            validateChannel(channel);
            validateDivider(divider);
            STR d1   = (channel == 0) ? STR.CHA_DIV1   : STR.CHB_DIV1;
            STR d10  = (channel == 0) ? STR.CHA_DIV10  : STR.CHB_DIV10;
            STR d100 = (channel == 0) ? STR.CHA_DIV100 : STR.CHB_DIV100;
            strobeMemory.GetRegister(d1).set((divider == 1) ? 0 : 1);
            strobeMemory.GetRegister(d10).set((divider == 10) ? 0 : 1);
            strobeMemory.GetRegister(d100).set((divider == 100) ? 0 : 1);
            strobeMemory.WriteSingle(d1);
            strobeMemory.WriteSingle(d10);
            strobeMemory.WriteSingle(d100);
        }

        ///<summary>Multiplier(unsigned integer channel, unsigned integer multiplier)</summary>
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

        #endregion
        
        #region the rest

        public void SetTriggerPos(int trigPos)
        {
            Logger.AddEntry(this, LogMessageType.CommandToDevice, "Set triglevel to " + trigPos);
            fpgaSettingsMemory.GetRegister(REG.TRIGGERLEVEL).InternalValue = (byte)trigPos;
            fpgaSettingsMemory.WriteSingle(REG.TRIGGERLEVEL);
        }

        public void SetTriggerPosBasedOnVoltage(float triggerVoltage)
        {
            float fTriggerLevel = (triggerVoltage - fpgaSettingsMemory.GetRegister(REG.CHB_YOFFSET_VOLTAGE).InternalValue * calibrationCoefficients[1] - calibrationCoefficients[2]) / calibrationCoefficients[0];
            if (fTriggerLevel < 0) fTriggerLevel = 0;
            if (fTriggerLevel > 255) fTriggerLevel = 255;

            fpgaSettingsMemory.GetRegister(REG.TRIGGERLEVEL).InternalValue = (byte)fTriggerLevel;
            fpgaSettingsMemory.WriteSingle(REG.TRIGGERLEVEL);
        }


        public void EnableCalib()
        {
            strobeMemory.GetRegister(STR.CHB_ENABLECALIB).InternalValue = 1;
            strobeMemory.WriteSingle(STR.CHB_ENABLECALIB);
        }

        public void DecreaseReadoutSpead()
        {
            fpgaSettingsMemory.GetRegister(REG.SAMPLECLOCKDIVIDER_B1).InternalValue = 1;
            fpgaSettingsMemory.WriteSingle(REG.SAMPLECLOCKDIVIDER_B1);
        }

        public void ChangeCalibVoltage()
        {
            int orig = fpgaSettingsMemory.GetRegister(REG.CALIB_VOLTAGE).InternalValue + 1;
            if (orig > 120) orig = 20;

            fpgaSettingsMemory.GetRegister(REG.CALIB_VOLTAGE).InternalValue = (byte)orig;
            fpgaSettingsMemory.WriteSingle(REG.CALIB_VOLTAGE);
        }

        public void ToggleFreeRunning()
        {

            //fpgaMemory.RegisterByName(REG.SAMPLECLOCKDIV_B1).InternalValue = 1;
            //fpgaMemory.WriteSingle(REG.SAMPLECLOCKDIV_B1);

            if (strobeMemory.GetRegister(STR.FREE_RUNNING).InternalValue == 0)
                strobeMemory.GetRegister(STR.FREE_RUNNING).InternalValue = 1;
            else
                strobeMemory.GetRegister(STR.FREE_RUNNING).InternalValue = 0;
            strobeMemory.WriteSingle(STR.FREE_RUNNING);
        }

        public int FreqDivider
        {
            get
            {
                fpgaSettingsMemory.ReadSingle(REG.SAMPLECLOCKDIVIDER_B1);
                fpgaSettingsMemory.ReadSingle(REG.SAMPLECLOCKDIVIDER_B0);
                return fpgaSettingsMemory.GetRegister(REG.SAMPLECLOCKDIVIDER_B1).InternalValue << 8 + fpgaSettingsMemory.GetRegister(REG.SAMPLECLOCKDIVIDER_B0).InternalValue + 1;
            }
        }

        public int GetTriggerPos()
        {
            fpgaSettingsMemory.ReadSingle(REG.TRIGGERHOLDOFF_B1);
            fpgaSettingsMemory.ReadSingle(REG.TRIGGERHOLDOFF_B0);
            int msb = fpgaSettingsMemory.GetRegister(REG.TRIGGERHOLDOFF_B1).InternalValue;
            msb = msb << 8;
            int lsb = fpgaSettingsMemory.GetRegister(REG.TRIGGERHOLDOFF_B0).InternalValue;
            return msb + lsb + 1;
        }

        public void SetTriggerHorPos(int value)
        {
            value--;
            if (value < 0) value = 0;
            if (value > 2047) value = 2047;

            fpgaSettingsMemory.GetRegister(REG.TRIGGERHOLDOFF_B1).InternalValue = (byte)((value) >> 8);
            fpgaSettingsMemory.WriteSingle(REG.TRIGGERHOLDOFF_B1);
            fpgaSettingsMemory.GetRegister(REG.TRIGGERHOLDOFF_B0).InternalValue = (byte)((value));
            fpgaSettingsMemory.WriteSingle(REG.TRIGGERHOLDOFF_B0);
        }

        //private nested classes, shielding this from the outside.
        //only ScopeV2 can instantiate this class!
        private class ScopeV2CalibrationVoltage : EInterfaces.ICalibrationVoltage
        {
            //private EFunctionality calibrationEnabled;
            private EFunctionality calibrationVoltage;

            //public EFunctionality CalibrationEnabled { get { return calibrationEnabled; } }
            public EFunctionality CalibrationVoltage { get { return calibrationVoltage; } }

            public ScopeV2CalibrationVoltage(ScopeV2 deviceImplementation)
            {
                this.calibrationVoltage = new EFCalibrationVoltage("Calibration voltage", "V", 0, deviceImplementation.fpgaSettingsMemory.GetRegister(REG.CALIB_VOLTAGE), 3.3f);
                //this.calibrationEnabled = new EFunctionality("Calibration enabled", "", deviceImplementation.strobeMemory, new string[] { STR.CHB_DIV1" }, F2H_CalibEnabled, H2F_CalibEnabled);
            }
        }

        private class ScopeV2TriggerPosition : EInterfaces.ITriggerPosition
        {
            private EFunctionality triggerPosition;
            public EFunctionality TriggerPosition { get { return triggerPosition; } }

            public ScopeV2TriggerPosition(ScopeV2 deviceImplementation)
            {
                this.triggerPosition = new EFTriggerPosition("Trigger position", "", 140, deviceImplementation.fpgaSettingsMemory.GetRegister(REG.TRIGGERLEVEL));
                //this.calibrationEnabled = new EFunctionality("Calibration enabled", "", deviceImplementation.strobeMemory, new string[] { STR.CHB_DIV1" }, F2H_CalibEnabled, H2F_CalibEnabled);
            }
        }

        private class ScopeV2ScopeChannelB : EInterfaces.IScopeChannel
        {
            private EFunctionality multiplicationFactor;
            private EFunctionality divisionFactor;
            private EFunctionality samplingFrequency;
            private EFOffset channelOffset;

            public EFunctionality MultiplicationFactor { get { return multiplicationFactor; } }
            public EFunctionality DivisionFactor { get { return divisionFactor; } }
            public EFunctionality SamplingFrequency { get { return samplingFrequency; } }
            public EFunctionality ChannelOffset { get { return channelOffset; } }

            public ScopeV2ScopeChannelB(ScopeV2 deviceImplementation)
            {
                EDeviceMemoryRegister[] multiplicationStrobes = new EDeviceMemoryRegister[3];
                multiplicationStrobes[0] = deviceImplementation.strobeMemory.GetRegister(STR.CHB_MULT1);
                multiplicationStrobes[1] = deviceImplementation.strobeMemory.GetRegister(STR.CHB_MULT2);
                multiplicationStrobes[2] = deviceImplementation.strobeMemory.GetRegister(STR.CHB_MULT3);

                int[] multiplyingResistors = new int[3] { 0, 1000, 6200 };

                //this.channelOffset = new EFOffset("Channel offset", "V", 0);
                this.multiplicationFactor = new EFMultiplicationFactor("Multiplication factor", "", 1, multiplicationStrobes, 1000, multiplyingResistors, 24, channelOffset);
                //this.divisionFactor = new EFDivisionFactor("Division factor", "", 1);
                this.samplingFrequency = new EFSamplingFrequency("Sampling Frequency", "Hz", 100000000);
            }

            public float MaxRange { get { return 255f; } }
            public float ScalingFactor { get { return multiplicationFactor.InternalValue / divisionFactor.InternalValue; } }
        }

        public void UploadToRAM(byte[] inData)
        {
            //raise global reset to reset RAM address counter, and to make sure the RAM switching is safe
            strobeMemory.GetRegister(STR.GLOBAL_RESET).InternalValue = 1;
            strobeMemory.WriteSingle(STR.GLOBAL_RESET);

            //save previous ram config
            fpgaSettingsMemory.ReadSingle(REG.RAM_CONFIGURATION);
            byte previousRamConfiguration = fpgaSettingsMemory.GetRegister(REG.RAM_CONFIGURATION).InternalValue;

            //set ram config to I2C input
            fpgaSettingsMemory.GetRegister(REG.RAM_CONFIGURATION).InternalValue = 2; //sets RAM0 to I2C input
            fpgaSettingsMemory.WriteSingle(REG.RAM_CONFIGURATION);

            //lower global reset
            strobeMemory.GetRegister(STR.GLOBAL_RESET).InternalValue = 0;
            strobeMemory.WriteSingle(STR.GLOBAL_RESET);

            //break data up into blocks of 8bytes
            int blockSize = 8;
            int fullLength = inData.Length;
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
                    toSend[i++] = inData[blockCounter * blockSize + c];

                eDevice.HWInterface.WriteControlBytes(toSend);

                blockCounter++;
            }

            //set ram config to original state
            fpgaSettingsMemory.GetRegister(REG.RAM_CONFIGURATION).InternalValue = previousRamConfiguration; //sets RAM0 to I2C input
            fpgaSettingsMemory.WriteSingle(REG.RAM_CONFIGURATION);

            //lower global reset
            strobeMemory.GetRegister(STR.GLOBAL_RESET).InternalValue = 0;
            strobeMemory.WriteSingle(STR.GLOBAL_RESET);
        }
        #endregion

    }
}
