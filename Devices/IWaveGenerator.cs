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
        bool GeneratorToAnalogEnabled { set; }
        bool GeneratorToDigitalEnabled { set; }
        int GeneratorStretcherForFrequency(double frequency);
        int GeneratorNumberOfSamplesForFrequency(double frequency);
        int GeneratorNumberOfSamples { set; get; }
        int GeneratorStretching { set; get; }
        double GeneratorFrequencyMax { get; }
        double GeneratorFrequencyMin { get; }
        double GeneratorFrequency { get; set;  }
    }
}
