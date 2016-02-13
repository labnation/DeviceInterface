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
        public double[] GeneratorDataDouble
        {
            set
            {
                int[] converted = value.Select(x => (int)(x / 3.3 * 255)).ToArray();
                GeneratorDataInt = converted;
            }
        }

        public int[] GeneratorDataInt
        {
            set
            {
                if (!Connected) return;
                if (value.Length < AWG_SAMPLES_MIN)
                    throw new ValidationException(String.Format("While setting AWG data: data buffer can't be shorter than {0} samples, got {1}", AWG_SAMPLES_MIN, value.Length));

                if (value.Length > AWG_SAMPLES_MAX)
                    throw new ValidationException(String.Format("While setting AWG data: data buffer can't be longer than {0} samples, got {1}", AWG_SAMPLES_MAX, value.Length));

                DataOutOfRange = value.Where(x => x > byte.MaxValue || x < byte.MinValue).Count() > 0;
                byte[] convertedBytes = value.Select(x => (byte)Math.Min(255, Math.Max(0, x))).ToArray();
                GeneratorDataByte = convertedBytes;
            }
        }

        public byte[] GeneratorDataByte
        {
            set
            {
                DataOutOfRange = false;
                GeneratorData = value;
            }
        }

        private byte[] GeneratorData
        {
            set
            {
                GeneratorNumberOfSamples = value.Length;
                hardwareInterface.SetControllerRegister(Hardware.ScopeController.AWG, 0, value);
            }
        }

        public int GeneratorNumberOfSamples
        {
            set
            {
                if (value < AWG_SAMPLES_MIN)
                    throw new ValidationException(String.Format("While setting AWG data: data buffer can't be shorter than {0} samples, got {1}", AWG_SAMPLES_MIN, value));

                if (value > AWG_SAMPLES_MAX)
                    throw new ValidationException(String.Format("While setting AWG data: data buffer can't be longer than {0} samples, got {1}", AWG_SAMPLES_MAX, value));

                FpgaSettingsMemory[REG.GENERATOR_SAMPLES_B0].WriteImmediate((byte)(value - 1));
                FpgaSettingsMemory[REG.GENERATOR_SAMPLES_B1].WriteImmediate((byte)((value - 1) >> 8));
            }
            get
            {
                return (FpgaSettingsMemory[REG.GENERATOR_SAMPLES_B0].GetByte() + (FpgaSettingsMemory[REG.GENERATOR_SAMPLES_B1].GetByte() << 8)) + 1;
            }
        }

        public int GeneratorStretching
        {
            set
            {
                if (value > 255 || value < 0)
                {
                    throw new ValidationException(String.Format("AWG stretching out of range [0,255] - got {0}", value));
                }
                FpgaSettingsMemory[REG.GENERATOR_DECIMATION].Set((byte)value);
            }
            get
            {
                return FpgaSettingsMemory[REG.GENERATOR_DECIMATION].GetByte();
            }
        }

        public bool GeneratorToAnalogEnabled
        {
            set
            {
                //Disable logic analyser in case AWG is being enabled
                if (!Connected) return;
                if (value)
                    StrobeMemory[STR.LA_ENABLE].WriteImmediate(false);
                StrobeMemory[STR.GENERATOR_TO_AWG].WriteImmediate(value);
            }
        }

        public bool GeneratorToDigitalEnabled
        {
            set
            {
                //Disable logic analyser in case AWG is being enabled
                if (!Connected) return;
                StrobeMemory[STR.GENERATOR_TO_DIGITAL].WriteImmediate(value);
            }
        }

        public double GeneratorFrequencyMax
        {
            get
            {
                return 1.0 / (AWG_SAMPLES_MIN * AWG_SAMPLE_PERIOD_0);
            }
        }
        public double GeneratorFrequencyMin
        {
            get
            {
                return 1.0 / ((AWG_SAMPLES_MAX - 1) * AWG_SAMPLE_PERIOD_0 * (AWG_STRETCHER_MAX + 1));
            }
        }
        public int GeneratorStretcherForFrequency(double frequency)
        {
            if (frequency > GeneratorFrequencyMax || frequency < GeneratorFrequencyMin)
                throw new ValidationException(String.Format("AWG frequency {0} out of range [{1},{2}]", frequency, GeneratorFrequencyMin, GeneratorFrequencyMax));

            double numberOfSamplesAtFullRate = Math.Floor(1 / (AWG_SAMPLE_PERIOD_0 * frequency));
            return (int)Math.Floor(numberOfSamplesAtFullRate / AWG_SAMPLES_MAX); ;
        }
        public int GeneratorNumberOfSamplesForFrequency(double frequency)
        {
            if (frequency > GeneratorFrequencyMax || frequency < GeneratorFrequencyMin)
                throw new ValidationException(String.Format("AWG frequency {0} out of range [{1},{2}]", frequency, GeneratorFrequencyMin, GeneratorFrequencyMax));

            double numberOfSamplesAtFullRate = Math.Floor(1 / (AWG_SAMPLE_PERIOD_0 * frequency));
            int stretcher = GeneratorStretcherForFrequency(frequency);
            return (int)Math.Floor(numberOfSamplesAtFullRate / (stretcher + 1));
        }
        public double GeneratorFrequency
        {
            set
            {
                GeneratorStretching = GeneratorStretcherForFrequency(value);
                GeneratorNumberOfSamples = GeneratorNumberOfSamplesForFrequency(value);
            }
            get
            {
                return 1 / (AWG_SAMPLE_PERIOD_0 * (GeneratorStretching + 1) * GeneratorNumberOfSamples);
            }
        }
    }
}
