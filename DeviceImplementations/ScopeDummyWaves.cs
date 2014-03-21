using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.DeviceImplementations
{
    public enum WaveForm { SINE, SQUARE, SAWTOOTH, TRIANGLE };

    partial class ScopeDummy
    {
        private static float[] GenerateWave(WaveForm waveForm, uint waveLength, double samplePeriod, double timeOffset, double frequency, double amplitude, double phase)
        {
            float[] wave = new float[waveLength];
            switch(waveForm) {
                case WaveForm.SINE:
                    wave = ScopeDummy.WaveSine(waveLength, samplePeriod, timeOffset, frequency, amplitude, phase);
                    break;
                case WaveForm.SQUARE:
                    wave = ScopeDummy.WaveSquare(waveLength, samplePeriod, timeOffset, frequency, amplitude, phase);
                    break;
                case WaveForm.SAWTOOTH:
                    wave = ScopeDummy.WaveSawTooth(waveLength, samplePeriod, timeOffset, frequency, amplitude, phase);
                    break;
                case WaveForm.TRIANGLE:
                    wave = ScopeDummy.WaveTriangle(waveLength, samplePeriod, timeOffset, frequency, amplitude, phase);
                    break;
                default:
                    throw new NotImplementedException();
            }
            return wave;
        }

        private static bool Trigger(float[] wave, int holdoff, float level, out int triggerIndex)
        {
            //Hold off:
            // - if positive, start looking for trigger at that index, so we are sure to have that many samples before the trigger
            // - if negative, start looking at index 0, but add abs(holdoff) to returned index
            triggerIndex = 0;
            for (int i = Math.Max(0, holdoff); i < wave.Length - 1; i++)
            {
                if (wave[i] <= level && wave[i + 1] > level)
                {
                    triggerIndex = i;
                    return true;
                }
            }
            return false;
        }
        private static float[] CropWave(uint outputLength, float[] sourceWave, int triggerIndex, int holdoff)
        {
            float[] output = new float[outputLength];
            try
            {
                Array.Copy(sourceWave, triggerIndex - holdoff, output, 0, outputLength);
                return output;
            } catch (ArgumentException e) {
                Logger.AddEntry(null, LogMessageType.ECoreInfo, "trigger too close to source wave edge to return wave [" + e.Message + "]");
                return null;
            }
        }



        private static float[] WaveSine(uint nSamples, double samplePeriod, double timeOffset, double frequency, double amplitude, double phase)
        {
            float[] wave = new float[nSamples];
            for (int i = 0; i < wave.Length; i++)
                wave[i] = (float)(amplitude * Math.Sin(2.0 * Math.PI * frequency * ((double)i * samplePeriod + timeOffset) + phase));
            return wave;
        }

        private static float[] WaveSquare(uint nSamples, double samplePeriod, double timeOffset, double frequency, double amplitude, double phase)
        {
            float[] wave = new float[nSamples];
            for (int i = 0; i < wave.Length; i++)
                wave[i] = (((double)i * samplePeriod + timeOffset) % (1.0/frequency)) * frequency > 0.5 ? (float)amplitude : -1f*(float)amplitude;
            return wave;
        }
        private static float[] WaveSawTooth(uint nSamples, double samplePeriod, double timeOffset, double frequency, double amplitude, double phase)
        {
            float[] wave = new float[nSamples];
            for (int i = 0; i < wave.Length; i++)
                wave[i] = (float)((((double)i * samplePeriod + timeOffset) % (1.0 / frequency)) * frequency * amplitude);
            return wave;
        }
        private static float[] WaveTriangle(uint nSamples, double samplePeriod, double timeOffset, double frequency, double amplitude, double phase)
        {
            float[] wave = new float[nSamples];
            for (int i = 0; i < wave.Length; i++)
            {
                //Number between 0 and 1 indicating which part of the period we're in
                double periodSection = (((double)i * samplePeriod + timeOffset) % (1.0 / frequency)) * frequency;
                double scaler = periodSection < 1f/2 ? (periodSection - 1f/4) * 4 : (periodSection - 3f/4) * -4;
                wave[i] = (float)(scaler * amplitude);
            }
            return wave;
        }
    }
}
