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
        const double AWG_SAMPLE_PERIOD_0 = 10e-9; //10ns
        const int AWG_SAMPLES_MAX = 2048;
        const int AWG_SAMPLES_MIN = 128;
        const int AWG_STRETCHER_MAX = 255;
        public bool AwgOutOfRange { get; private set; }

        /// <summary>
        /// Set the data with which the AWG runs
        /// </summary>
        /// <param name="data">AWG data</param>
        public void SetAwgData(double[] data)
        {
            if (!Connected) return;
            if (data.Length < AWG_SAMPLES_MIN)
                throw new ValidationException(String.Format("While setting AWG data: data buffer can't be shorter than {0} samples, got {1}", AWG_SAMPLES_MIN, data.Length));

            if (data.Length > AWG_SAMPLES_MAX)
                throw new ValidationException(String.Format("While setting AWG data: data buffer can't be longer than {0} samples, got {1}", AWG_SAMPLES_MAX, data.Length));

            int[] converted = data.Select(x => (int)(x / 3.3 * 255)).ToArray();
            AwgOutOfRange = converted.Where(x => x > byte.MaxValue || x < byte.MinValue).Count() > 0;
            byte[] convertedBytes = converted.Select(x => (byte)Math.Min(255, Math.Max(0, x))).ToArray();

            SetAwgNumberOfSamples(data.Length);
            hardwareInterface.SetControllerRegister(HardwareInterfaces.ScopeController.AWG, 0, convertedBytes);
        }

        public void SetAwgNumberOfSamples(int n)
        {
            if (n < AWG_SAMPLES_MIN)
                throw new ValidationException(String.Format("While setting AWG data: data buffer can't be shorter than {0} samples, got {1}", AWG_SAMPLES_MIN, n));

            if (n > AWG_SAMPLES_MAX)
                throw new ValidationException(String.Format("While setting AWG data: data buffer can't be longer than {0} samples, got {1}", AWG_SAMPLES_MAX, n));

            FpgaSettingsMemory[REG.AWG_SAMPLES_B0].WriteImmediate((byte)(n - 1));
            FpgaSettingsMemory[REG.AWG_SAMPLES_B1].WriteImmediate((byte)((n - 1) >> 8));
        }

        public int GetAwgNumberOfSamples()
        {
            return (FpgaSettingsMemory[REG.AWG_SAMPLES_B0].GetByte() + (FpgaSettingsMemory[REG.AWG_SAMPLES_B1].GetByte() << 8)) + 1;
        }

        public void SetAwgStretching(int decimation)
        {
            if (decimation > 255 || decimation < 0)
            {
                throw new ValidationException(String.Format("AWG stretching out of range [0,255] - got {0}", decimation));
            }
            FpgaSettingsMemory[REG.AWG_DECIMATION].Set((byte)decimation);
        }

        public int GetAwgStretching()
        {
            return FpgaSettingsMemory[REG.AWG_DECIMATION].GetByte();
        }

        public void SetAwgEnabled(bool enable)
        {
            //Disable logic analyser in case AWG is being enabled
            if (!Connected) return;
            if (enable)
                StrobeMemory[STR.LA_ENABLE].WriteImmediate(false);
            StrobeMemory[STR.AWG_ENABLE].WriteImmediate(enable);
        }

        public double GetAwgFrequencyMax()
        {
            return 1.0 / (AWG_SAMPLES_MIN * AWG_SAMPLE_PERIOD_0);
        }
        public double GetAwgFrequencyMin()
        {
            return 1.0 / ((AWG_SAMPLES_MAX - 1) * AWG_SAMPLE_PERIOD_0 * (AWG_STRETCHER_MAX + 1));
        }
        public int GetAwgStretcherForFrequency(double frequency)
        {
            if (frequency > GetAwgFrequencyMax() || frequency < GetAwgFrequencyMin())
                throw new ValidationException(String.Format("AWG frequency {0} out of range [{1},{2}]", frequency, GetAwgFrequencyMin(), GetAwgFrequencyMax()));

            double numberOfSamplesAtFullRate = Math.Floor(1 / (AWG_SAMPLE_PERIOD_0 * frequency));
            return (int)Math.Floor(numberOfSamplesAtFullRate / AWG_SAMPLES_MAX); ;
        }
        public int GetAwgNumberOfSamplesForFrequency(double frequency)
        {
            if (frequency > GetAwgFrequencyMax() || frequency < GetAwgFrequencyMin())
                throw new ValidationException(String.Format("AWG frequency {0} out of range [{1},{2}]", frequency, GetAwgFrequencyMin(), GetAwgFrequencyMax()));

            double numberOfSamplesAtFullRate = Math.Floor(1 / (AWG_SAMPLE_PERIOD_0 * frequency));
            int stretcher = GetAwgStretcherForFrequency(frequency);
            return (int)Math.Floor(numberOfSamplesAtFullRate / (stretcher + 1));
        }
        public void SetAwgFrequency(double frequency)
        {
            SetAwgStretching(GetAwgStretcherForFrequency(frequency));
            SetAwgNumberOfSamples(GetAwgNumberOfSamplesForFrequency(frequency));
        }
        public double GetAwgFrequency()
        {
            return 1 / (AWG_SAMPLE_PERIOD_0 * (GetAwgStretching() + 1) * GetAwgNumberOfSamples());
        }
    }
}
