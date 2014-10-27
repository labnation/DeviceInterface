using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.Devices
{
    public interface IWaveGenerator : IDevice
    {
        void SetAwgData(double[] data);
        void SetAwgEnabled(bool enable);
        int GetAwgStretcherForFrequency(double frequency);
        int GetAwgNumberOfSamplesForFrequency(double frequency);
        void SetAwgNumberOfSamples(int n);
        int GetAwgNumberOfSamples();
        void SetAwgStretching(int decimation);
        int GetAwgStretching();
        double GetAwgFrequencyMax();
        double GetAwgFrequencyMin();
        void SetAwgFrequency(double frequency);
        double GetAwgFrequency();
    }
}
