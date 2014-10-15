using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MatlabFileIO;
using System.IO;
using Common;

namespace ECore.Devices
{
    partial class DummyScope
    {
        private static float[] readChannelA = null;
        private static float[] readChannelB = null;
        private static double[] readTime = null;

        public static bool GetWaveFromFile(AcquisitionMode acqMode, double triggerHoldoff, AnalogChannel triggerChannel, TriggerDirection triggerDirection, float triggerLevel, uint decimation, double samplePeriod, ref float[][] output)
        {
            if(readChannelA == null || readChannelB == null || readTime == null) 
            {
                String filename = Path.GetTempFileName();
                FileStream f = new FileStream(filename, FileMode.Create, FileAccess.Write);
				byte[] i2cSequence = Resources.Load ("i2c_sequence.mat");
				f.Write(i2cSequence, 0, i2cSequence.Length);
                f.Close();

                MatfileReader matfileReader = new MatlabFileIO.MatfileReader(filename);
                readChannelA = Utils.CastArray<double, float>(matfileReader.Variables["chA"].data as double[]);
                readChannelB = Utils.CastArray<double, float>(matfileReader.Variables["chB"].data as double[]);
                readTime = matfileReader.Variables["time"].data as double[];
                matfileReader.Close();
            }
    
            float[][] waves = new float[2][];

            waves[0] = Utils.DecimateArray(readChannelA, decimation);
            waves[1] = Utils.DecimateArray(readChannelB, decimation);
            double[] time = readTime;

            double samplePeriodMeasured = time[decimation] - time[0];
            if (samplePeriodMeasured != samplePeriod)
                throw new Exception("Data from file doesn't suit the dummy scope sampling frequency");

            int triggerHoldoffInSamples = (int)Math.Min(triggerHoldoff / samplePeriod, int.MaxValue);
            int triggerIndex;
            if (acqMode == AcquisitionMode.AUTO)
            {
                //Crop out a random piece of the wave
                Random r = new Random();
                triggerIndex = triggerHoldoffInSamples + Math.Max(0, (int)((waves[0].Length - triggerHoldoffInSamples - outputWaveLength) * r.NextDouble()));

                output = new float[channels][];
                for (int i = 0; i < channels; i++)
                {
                    output[i] = DummyScope.CropWave(outputWaveLength, waves[i], triggerIndex, triggerHoldoffInSamples);
                    if (output[i] == null) return false;
                }
                return true;
            }
            else if (acqMode == AcquisitionMode.NORMAL || acqMode == AcquisitionMode.SINGLE)
            {
                if (DummyScope.TriggerAnalog(acqMode, waves[triggerChannel.Value], triggerDirection, triggerHoldoffInSamples, triggerLevel, 0f, outputWaveLength, out triggerIndex))
                {
                    output = new float[channels][];
                    for (int i = 0; i < channels; i++)
                    {
                        output[i] = DummyScope.CropWave(outputWaveLength, waves[i], triggerIndex, triggerHoldoffInSamples);
                        if (output[i] == null) return false;
                    }
                    return true;
                }
                else
                    return false;
            }
            else
                return false;
        }
    }
}
