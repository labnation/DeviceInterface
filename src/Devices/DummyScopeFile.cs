using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MatlabFileIO;
using System.IO;
using LabNation.Common;

namespace LabNation.DeviceInterface.Devices
{
    partial class DummyScope
    {
        private static Dictionary<AnalogChannel, float[]> readChannel = null;
        private static double[] readTime = null;
        private static double samplePeriodOriginal = 0;
        private static int sequenceLength = 0;

        public static float[] GetWaveFromFile(AnalogChannel channel, uint waveLength, double samplePeriod, double timeOffset)
        {
            if(readChannel == null || readTime == null) 
            {
                String filename = Path.GetTempFileName();
                FileStream f = new FileStream(filename, FileMode.Create, FileAccess.Write);
				byte[] i2cSequence = Resources.Load ("i2c_sequence.mat");
				f.Write(i2cSequence, 0, i2cSequence.Length);
                f.Close();

                MatfileReader matfileReader = new MatlabFileIO.MatfileReader(filename);
                readChannel = new Dictionary<AnalogChannel, float[]>() {
                    { AnalogChannel.ChA, Utils.CastArray<double, float>(matfileReader.Variables["chA"].data as double[]) },
                    { AnalogChannel.ChB, Utils.CastArray<double, float>(matfileReader.Variables["chB"].data as double[]) },
                };
                readTime = matfileReader.Variables["time"].data as double[];
                samplePeriodOriginal = readTime[1] - readTime[0];
                sequenceLength = readChannel[AnalogChannel.ChA].Length;
                matfileReader.Close();
            }

            if (samplePeriod / samplePeriodOriginal % 1.0 != 0.0)
                throw new Exception("Data from file doesn't suit the dummy scope sampling frequency");

            uint decimation = (uint)Math.Ceiling(samplePeriod / samplePeriodOriginal);
            float[] wave = Utils.DecimateArray(readChannel[channel], decimation);
            
            int sampleOffset = (int)Math.Ceiling(timeOffset / samplePeriodOriginal % sequenceLength);

            int requiredRepetitions = (int)Math.Ceiling(waveLength / (double)wave.Length);
            if (requiredRepetitions > 1)
            { 
                List<float> concat = new List<float>();
                for(int i = 0; i < requiredRepetitions; i++)
                    concat.AddRange(wave);
                wave = concat.Take((int)waveLength).ToArray();
            }
            return wave;
        }
    }
}
