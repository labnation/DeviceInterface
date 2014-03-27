using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MatlabFileIO;

namespace ECore.DeviceImplementations
{
    partial class ScopeDummy
    {
        private string sequenceFilename = "i2c_sequence.mat";

        public bool GetWaveFromFile(ref float[][] output, ref double samplePeriod, double triggerHoldoff)
        {
            string filename = Utils.ApplicationDataPath + sequenceFilename;
            MatlabFileReader matfileReader = new MatlabFileIO.MatlabFileReader(filename);
            MatlabFileArrayReader arrayReader;
            
            float[][] waves = new float[2][];

            arrayReader = matfileReader.OpenArray("chA");
            waves[0] = Utils.CastArray<double, float>(arrayReader.ReadRowDouble());

            arrayReader = matfileReader.OpenArray("chB");
            waves[1] = Utils.CastArray<double, float>(arrayReader.ReadRowDouble());
            
            arrayReader = matfileReader.OpenArray("time");
            double[] time = arrayReader.ReadRowDouble();

            matfileReader.Close();

            samplePeriod = time[1] - time[0];
            int triggerHoldoffInSamples = (int)(triggerHoldoff / samplePeriod);
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
