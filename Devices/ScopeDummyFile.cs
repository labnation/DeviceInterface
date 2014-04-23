using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MatlabFileIO;

namespace ECore.Devices
{
    partial class ScopeDummy
    {
        private static string sequenceFilename = "i2c_sequence.mat";

        public static bool GetWaveFromFile(TriggerMode triggerMode, double triggerHoldoff, uint triggerChannel, TriggerDirection triggerDirection, float triggerLevel, uint decimation, double samplePeriod, ref float[][] output)
        {
            string filename = Utils.ApplicationDataPath + sequenceFilename;
            MatfileReader matfileReader = new MatlabFileIO.MatfileReader(filename);
            
            float[][] waves = new float[2][];

            waves[0] = Utils.DecimateArray(Utils.CastArray<double, float>(matfileReader.Variables["chA"].data as double[]), decimation);

            waves[1] = Utils.DecimateArray(Utils.CastArray<double, float>(matfileReader.Variables["chB"].data as double[]), decimation);
            
            double[] time = matfileReader.Variables["time"].data as double[];

            matfileReader.Close();

            double samplePeriodMeasured = time[decimation] - time[0];
            if (samplePeriodMeasured != samplePeriod)
                throw new Exception("Data from file doesn't suit the dummy scope sampling frequency");

            int triggerHoldoffInSamples = (int)Math.Min(triggerHoldoff / samplePeriod, int.MaxValue);
            int triggerIndex;
            if (triggerMode == TriggerMode.FREE_RUNNING)
            {
                //Crop out a random piece of the wave
                Random r = new Random();
                triggerIndex = triggerHoldoffInSamples + Math.Max(0, (int)((waves[0].Length - triggerHoldoffInSamples - outputWaveLength) * r.NextDouble()));

                output = new float[channels][];
                for (int i = 0; i < channels; i++)
                {
                    output[i] = ScopeDummy.CropWave(outputWaveLength, waves[i], triggerIndex, triggerHoldoffInSamples);
                    if (output[i] == null) return false;
                }
                return true;
            }
            else if (triggerMode == TriggerMode.ANALOG)
            {
                if (ScopeDummy.TriggerAnalog(waves[triggerChannel], triggerDirection, triggerHoldoffInSamples, triggerLevel, 0f, outputWaveLength, out triggerIndex))
                {
                    output = new float[channels][];
                    for (int i = 0; i < channels; i++)
                    {
                        output[i] = ScopeDummy.CropWave(outputWaveLength, waves[i], triggerIndex, triggerHoldoffInSamples);
                        if (output[i] == null) return false;
                    }
                    return true;
                }
                else
                    return false;
            }
            else
            {
                return false;
            }
        }
    }
}
