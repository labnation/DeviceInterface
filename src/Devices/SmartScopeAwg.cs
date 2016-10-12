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
        const int AWG_SAMPLES_MIN = 1;
        const Int32 AWG_STRETCHER_MAX = 256*256*256-1;
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

        public Int32 GeneratorStretching
        {
            set
            {
                if (value > AWG_STRETCHER_MAX || value < 0)
                {
                    throw new ValidationException(String.Format("AWG stretching out of range [0,{0}] - got {1}", AWG_STRETCHER_MAX, value));
                }
                FpgaSettingsMemory[REG.GENERATOR_DECIMATION_B0].Set((byte)(value & 0xFF));
                FpgaSettingsMemory[REG.GENERATOR_DECIMATION_B1].Set((byte)((value >> 8) & 0xFF));
                FpgaSettingsMemory[REG.GENERATOR_DECIMATION_B2].Set((byte)((value >> 16) & 0xFF));
            }
            get
            {
                return 
                    (Int32)(FpgaSettingsMemory[REG.GENERATOR_DECIMATION_B0].GetByte()      ) +
                    (Int32)(FpgaSettingsMemory[REG.GENERATOR_DECIMATION_B1].GetByte() <<  8) +
                    (Int32)(FpgaSettingsMemory[REG.GENERATOR_DECIMATION_B2].GetByte() << 16);
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
            get
            {
                return StrobeMemory[STR.GENERATOR_TO_AWG].GetBool();
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
            get
            {
                return StrobeMemory[STR.GENERATOR_TO_DIGITAL].GetBool();
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
                return 1.0 / ((AWG_SAMPLES_MAX - 1) * AWG_SAMPLE_PERIOD_0 * ((double)(AWG_STRETCHER_MAX) + 1));
            }
        }
        public Int32 GeneratorStretcherForFrequency(double frequency)
        {
            if (frequency > GeneratorFrequencyMax || frequency < GeneratorFrequencyMin)
                throw new ValidationException(String.Format("AWG frequency {0} out of range [{1},{2}]", frequency, GeneratorFrequencyMin, GeneratorFrequencyMax));

            double numberOfSamplesAtFullRate = Math.Floor(1 / (AWG_SAMPLE_PERIOD_0 * frequency));
            return (Int32)Math.Floor(numberOfSamplesAtFullRate / AWG_SAMPLES_MAX); ;
        }
        public int GeneratorNumberOfSamplesForFrequency(double frequency)
        {
            if (frequency > GeneratorFrequencyMax || frequency < GeneratorFrequencyMin)
                throw new ValidationException(String.Format("AWG frequency {0} out of range [{1},{2}]", frequency, GeneratorFrequencyMin, GeneratorFrequencyMax));

            double numberOfSamplesAtFullRate = Math.Floor(1 / (AWG_SAMPLE_PERIOD_0 * frequency));
            Int32 stretcher = GeneratorStretcherForFrequency(frequency);
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

        public double GeneratorSamplePeriodMin { get { return AWG_SAMPLE_PERIOD_0; } }
        public double GeneratorSamplePeriodMax { get { return AWG_SAMPLE_PERIOD_0 * AWG_STRETCHER_MAX; } }
        public double GeneratorSamplePeriod
        {
            set {
                double samples = value / AWG_SAMPLE_PERIOD_0;
                Int32 samplesRounded = (Int32)Math.Floor(samples);
                GeneratorStretching = samplesRounded;
            }
            get {
            return GeneratorStretching * AWG_SAMPLE_PERIOD_0;
        }
        }
    }
}
