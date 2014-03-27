using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MatlabFileIO;

namespace ECore.DeviceImplementations
{
    partial class ScopeDummy
    {
        private static string sequenceFilename = "i2c_sequence.mat";

        public static bool GetWaveFromFile(double triggerHoldoff, uint triggerChannel, TriggerDirection triggerDirection, float triggerLevel, uint decimation, double samplePeriod, ref float[][] output)
        {
            string filename = Utils.ApplicationDataPath + sequenceFilename;
            MatlabFileReader matfileReader = new MatlabFileIO.MatlabFileReader(filename);
            MatlabFileArrayReader arrayReader;
            
            float[][] waves = new float[2][];

            arrayReader = matfileReader.OpenArray("chA");
            waves[0] = Utils.DecimateArray(Utils.CastArray<double, float>(arrayReader.ReadRowDouble()), decimation);

            arrayReader = matfileReader.OpenArray("chB");
            waves[1] = Utils.DecimateArray(Utils.CastArray<double, float>(arrayReader.ReadRowDouble()), decimation);
            
            arrayReader = matfileReader.OpenArray("time");
            double[] time = arrayReader.ReadRowDouble();

            matfileReader.Close();

            double samplePeriodMeasured = time[decimation] - time[0];
            if (samplePeriodMeasured != samplePeriod)
                throw new Exception("Data from file doesn't suit the dummy scope sampling frequency");

            int triggerHoldoffInSamples = (int)Math.Min(triggerHoldoff / samplePeriod, int.MaxValue);
            int triggerIndex;
            if (ScopeDummy.Trigger(waves[triggerChannel], triggerDirection, triggerHoldoffInSamples, triggerLevel, outputWaveLength, out triggerIndex))
            {
                output = new float[channels][];
                for (int i = 0; i < channels; i++)
                {
                    output[i] = ScopeDummy.CropWave(outputWaveLength, waves[i], triggerIndex, triggerHoldoffInSamples);
                }
                return true;
            }
            else
            {
                return false;
            }
            
        }
    }
}
