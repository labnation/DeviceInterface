using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.Devices
{
    public enum WaveForm { SINE, SQUARE, SAWTOOTH, TRIANGLE, SAWTOOTH_SINE };

    partial class ScopeDummy
    {
        private static float[] GenerateWave(WaveForm waveForm, uint waveLength, double samplePeriod, double timeOffset, double frequency, double amplitude, double phase, double dcOffset)
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
                case WaveForm.SAWTOOTH_SINE:
                    wave = ScopeDummy.WaveSawtoothSine(waveLength, samplePeriod, timeOffset, frequency, amplitude, phase);
                    break;
                default:
                    throw new NotImplementedException();
            }
            Func<float, float> offsetAdder = x => (float)(x + dcOffset);
            wave = Utils.TransformArray(wave, offsetAdder);
            return wave;
        }

        private static T[] CropWave<T>(uint outputLength, T[] sourceWave, int triggerIndex, int holdoff)
        {
            if (triggerIndex - holdoff + outputLength > sourceWave.Length) return null;
            
            T[] output = new T[outputLength];
            Array.Copy(sourceWave, triggerIndex - holdoff, output, 0, outputLength);
            return output;
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

        private static float[] WaveSawtoothSine(uint nSamples, double samplePeriod, double timeOffset, double frequency, double amplitude, double phase)
        {
            Func<float, float, float> sumFloat = (x, y) => (x + y);
            float[] wave1 = WaveSawTooth(nSamples, samplePeriod, timeOffset, frequency, amplitude, phase);
            float[] wave2 = WaveSine(nSamples, samplePeriod, timeOffset, frequency * 7.0, amplitude * 0.1, phase);
            float[] wave = Utils.CombineArrays(wave1, wave2, ref sumFloat);
            return wave;
        }

        public override bool Connected { get { return true; } }

        private static void AddNoise(float[] output, double noiseAmplitude)
        {
            Random r = new Random();
            for (int i = 0; i < output.Length; i++)
            {
                output[i] = (float)(output[i] + (r.NextDouble() - 0.5) * noiseAmplitude);
            }

        }
        
    }
}
