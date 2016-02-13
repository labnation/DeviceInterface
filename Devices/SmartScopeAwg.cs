using System;
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
        const double AWG_SAMPLE_PERIOD_0 = 10e-9; //10ns
        const int AWG_SAMPLES_MAX = 2048;
        const int AWG_SAMPLES_MIN = 20;
        const int AWG_STRETCHER_MAX = 255;
        public bool DataOutOfRange { get; private set; }

        /// <summary>
        /// Set the data with which the AWG runs
        /// </summary>
        /// <param name="data">AWG data</param>
        public void SetGeneratorData(double[] data)
        {
            int[] converted = data.Select(x => (int)(x / 3.3 * 255)).ToArray();
            SetGeneratorData(converted);
        }
        
        public void SetGeneratorData(int[] data)
        {
            if (!Connected) return;
            if (data.Length < AWG_SAMPLES_MIN)
                throw new ValidationException(String.Format("While setting AWG data: data buffer can't be shorter than {0} samples, got {1}", AWG_SAMPLES_MIN, data.Length));

            if (data.Length > AWG_SAMPLES_MAX)
                throw new ValidationException(String.Format("While setting AWG data: data buffer can't be longer than {0} samples, got {1}", AWG_SAMPLES_MAX, data.Length));
            
            DataOutOfRange = data.Where(x => x > byte.MaxValue || x < byte.MinValue).Count() > 0;
            byte[] convertedBytes = data.Select(x => (byte)Math.Min(255, Math.Max(0, x))).ToArray();
            pSetGeneratorData(convertedBytes);
        }

        public void SetGeneratorData(byte[] data)
        {
            DataOutOfRange = false;
            pSetGeneratorData(data);
        }

        private void pSetGeneratorData(byte[] data)
        {
            SetGeneratorNumberOfSamples(data.Length);
            hardwareInterface.SetControllerRegister(Hardware.ScopeController.AWG, 0, data);
        }

        public void SetGeneratorNumberOfSamples(int n)
        {
            if (n < AWG_SAMPLES_MIN)
                throw new ValidationException(String.Format("While setting AWG data: data buffer can't be shorter than {0} samples, got {1}", AWG_SAMPLES_MIN, n));

            if (n > AWG_SAMPLES_MAX)
                throw new ValidationException(String.Format("While setting AWG data: data buffer can't be longer than {0} samples, got {1}", AWG_SAMPLES_MAX, n));

            FpgaSettingsMemory[REG.GENERATOR_SAMPLES_B0].WriteImmediate((byte)(n - 1));
            FpgaSettingsMemory[REG.GENERATOR_SAMPLES_B1].WriteImmediate((byte)((n - 1) >> 8));
        }

        public int GetGeneratorNumberOfSamples()
        {
            return (FpgaSettingsMemory[REG.GENERATOR_SAMPLES_B0].GetByte() + (FpgaSettingsMemory[REG.GENERATOR_SAMPLES_B1].GetByte() << 8)) + 1;
        }

        public void SetGeneratorStretching(int stretching)
        {
            if (stretching > 255 || stretching < 0)
            {
                throw new ValidationException(String.Format("AWG stretching out of range [0,255] - got {0}", stretching));
            }
            FpgaSettingsMemory[REG.GENERATOR_DECIMATION].Set((byte)stretching);
        }

        public int GetGeneratorStretching()
        {
            return FpgaSettingsMemory[REG.GENERATOR_DECIMATION].GetByte();
        }

        public void SetGeneratorToAnalogEnabled(bool enable)
        {
            //Disable logic analyser in case AWG is being enabled
            if (!Connected) return;
            if (enable)
                StrobeMemory[STR.LA_ENABLE].WriteImmediate(false);
            StrobeMemory[STR.GENERATOR_TO_AWG].WriteImmediate(enable);
        }

        public void SetGeneratorToDigitalEnabled(bool enable)
        {
            //Disable logic analyser in case AWG is being enabled
            if (!Connected) return;
            StrobeMemory[STR.GENERATOR_TO_DIGITAL].WriteImmediate(enable);
        }

        public double GetGeneratorFrequencyMax()
        {
            return 1.0 / (AWG_SAMPLES_MIN * AWG_SAMPLE_PERIOD_0);
        }
        public double GetGeneratorFrequencyMin()
        {
            return 1.0 / ((AWG_SAMPLES_MAX - 1) * AWG_SAMPLE_PERIOD_0 * (AWG_STRETCHER_MAX + 1));
        }
        public int GetGeneratorStretcherForFrequency(double frequency)
        {
            if (frequency > GetGeneratorFrequencyMax() || frequency < GetGeneratorFrequencyMin())
                throw new ValidationException(String.Format("AWG frequency {0} out of range [{1},{2}]", frequency, GetGeneratorFrequencyMin(), GetGeneratorFrequencyMax()));

            double numberOfSamplesAtFullRate = Math.Floor(1 / (AWG_SAMPLE_PERIOD_0 * frequency));
            return (int)Math.Floor(numberOfSamplesAtFullRate / AWG_SAMPLES_MAX); ;
        }
        public int GetGeneratorNumberOfSamplesForFrequency(double frequency)
        {
            if (frequency > GetGeneratorFrequencyMax() || frequency < GetGeneratorFrequencyMin())
                throw new ValidationException(String.Format("AWG frequency {0} out of range [{1},{2}]", frequency, GetGeneratorFrequencyMin(), GetGeneratorFrequencyMax()));

            double numberOfSamplesAtFullRate = Math.Floor(1 / (AWG_SAMPLE_PERIOD_0 * frequency));
            int stretcher = GetGeneratorStretcherForFrequency(frequency);
            return (int)Math.Floor(numberOfSamplesAtFullRate / (stretcher + 1));
        }
        public void SetGeneratorFrequency(double frequency)
        {
            SetGeneratorStretching(GetGeneratorStretcherForFrequency(frequency));
            SetGeneratorNumberOfSamples(GetGeneratorNumberOfSamplesForFrequency(frequency));
        }
        public double GetGeneratorFrequency()
        {
            return 1 / (AWG_SAMPLE_PERIOD_0 * (GetGeneratorStretching() + 1) * GetGeneratorNumberOfSamples());
        }
    }
}
