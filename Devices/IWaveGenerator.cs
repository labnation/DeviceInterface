using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LabNation.DeviceInterface.Devices
{
    public interface IWaveGenerator : IDevice
    {
        bool DataOutOfRange { get; }
        void SetGeneratorData(double[] data);
        void SetGeneratorData(int[] data);
        void SetGeneratorData(byte[] data);
        void SetGeneratorToAnalogEnabled(bool enable);
        void SetGeneratorToDigitalEnabled(bool enable);
        int GetGeneratorStretcherForFrequency(double frequency);
        int GetGeneratorNumberOfSamplesForFrequency(double frequency);
        void SetGeneratorNumberOfSamples(int n);
        int GetGeneratorNumberOfSamples();
        void SetGeneratorStretching(int decimation);
        int GetGeneratorStretching();
        double GetGeneratorFrequencyMax();
        double GetGeneratorFrequencyMin();
        void SetGeneratorFrequency(double frequency);
        double GetGeneratorFrequency();
    }
}
