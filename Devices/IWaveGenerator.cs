using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LabNation.DeviceInterface.Devices
{
    public interface IWaveGenerator : IDevice
    {
        bool DataOutOfRange { get; }
        double[] GeneratorDataDouble { set; }
        int[] GeneratorDataInt { set; }
        byte[] GeneratorDataByte { set; }
        bool GeneratorToAnalogEnabled { set; get; }
        bool GeneratorToDigitalEnabled { set; get; }
        UInt32 GeneratorStretcherForFrequency(double frequency);
        int GeneratorNumberOfSamplesForFrequency(double frequency);
        int GeneratorNumberOfSamples { set; get; }
        UInt32 GeneratorStretching { set; get; }
        double GeneratorFrequencyMax { get; }
        double GeneratorFrequencyMin { get; }
        double GeneratorFrequency { get; set;  }
        double GeneratorSamplePeriodMin { get; }
        double GeneratorSamplePeriodMax { get; }
        double GeneratorSamplePeriod { set; get; }
    }
}
