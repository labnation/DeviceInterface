using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.DeviceImplementations
{
    public enum WaveForm { SINE, BLOCK, TRIANGLE };

    partial class ScopeDummy
    {
        private static float[] GenerateWave(WaveForm waveForm, uint waveLength, double samplePeriod, double timeOffset, double frequency, double amplitude, double phase)
        {
            float[] wave = new float[waveLength];
            switch(waveForm) {
                case WaveForm.SINE:
                    //FIXME: use timeOffset based on dateTime
                    wave = ScopeDummy.WaveSine(waveLength, samplePeriod, timeOffset, 100e3, 1.0, 0.0);
                    break;
                default:
                    throw new NotImplementedException();
            }
            return wave;
        }

        private static int Trigger(float[] wave, uint acquisitionLength, uint holdoff, float level)
        {
            for (int i = 0; i < wave.Length - acquisitionLength - 1; i++)
            {
                if (wave[i] <= level && wave[i + 1] > level)
                {
                    return i;
                }
            }
            return -1;
        }



        private static float[] WaveSine(uint nSamples, double samplePeriod, double timeOffset, double frequency, double amplitude, double phase)
        {
            float[] wave = new float[nSamples];
            for (int i = 0; i < wave.Length; i++)
                wave[i] = (float)(amplitude * Math.Sin(2.0 * Math.PI * frequency * ((double)i * samplePeriod + timeOffset) + phase));
            return wave;
        }
    }
}
