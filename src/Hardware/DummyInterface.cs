using System;
using System.Collections.Generic;
using System.Linq;
using MatlabFileIO;
using System.IO;
using LabNation.Common;
using LabNation.DeviceInterface.Devices;
using System.Text;
using System.Threading.Tasks;

namespace LabNation.DeviceInterface.Hardware
{
    public class DummyInterface : IHardwareInterface
    { 
        public string Serial { get; private set; }
        public static string Audio = "Audio";
        public static string Generator = "Generator";
        public static string File = "File";
        protected DummyInterface(string serial)
        {
            this.Serial = serial; 
        }

        public virtual List<AnalogChannel> ChannelsToAcquireDataFor()
        {
            if (Serial == "Audio")
                return new List<AnalogChannel>() { AnalogChannel.ChA };
            else
                return AnalogChannel.List.ToList();
        }
    }

    public class DummyInterfaceAudio : DummyInterface { public DummyInterfaceAudio() : base(DummyInterface.Audio) { } }
    public class DummyInterfaceGenerator : DummyInterface { public DummyInterfaceGenerator() : base(DummyInterface.Generator) { } }
    public class DummyInterfaceFromFile : DummyInterface
    {
        private List<float[]> dataChA;
        private List<float[]> dataChB;
        private Dictionary<Channel, int> indexers;
        private double samplePeriod;

        public double AcquisitionLenght { get; private set; }

        public DummyInterfaceFromFile(string filename) : base(DummyInterface.File)
        {
            this.LoadDataFromFile(filename);
        }

        private void LoadDataFromFile(string filename)
        {
            MatfileReader matfileReader = new MatlabFileIO.MatfileReader(@"C:\Recording44.mat");

            //load data
            dataChA = LoadAnalogChannelFromMatlabFile("ChannelA", matfileReader);
            dataChB = LoadAnalogChannelFromMatlabFile("ChannelB", matfileReader);
            samplePeriod = (double)matfileReader.Variables["SamplePeriodInSeconds"].data;

            //init
            indexers = new Dictionary<Devices.Channel, int>();
            indexers.Add(AnalogChannel.ChA, 0);
            indexers.Add(AnalogChannel.ChB, 0);

            //calc fixed values
            if (dataChA.Count > 0)
                this.AcquisitionLenght = samplePeriod * dataChA[0].Length;
            else
                this.AcquisitionLenght = 0;
        }

        private List<float[]> LoadAnalogChannelFromMatlabFile(string channelName, MatfileReader matfileReader)
        {
            //if ChannelA data exists: extract slowly into memory in a format which is fast to look up
            if (!matfileReader.Variables.ContainsKey("ChannelA"))
            {
                return null;
            }
            else
            {
                List<float[]> dataCh = new List<float[]>();
                var voltages = matfileReader.Variables[channelName].data;
                float[,] voltages2d = (float[,])voltages;
                for (int i = 0; i < voltages2d.GetLength(0); i++)
                {
                    float[] temp = new float[voltages2d.GetLength(1)];
                    for (int j = 0; j < temp.Length; j++)
                        temp[j] = voltages2d[i, j];
                    dataCh.Add(temp);
                }
                return dataCh;
            }            
        }

        public float[] GetWaveFromFile(AnalogChannel channel, ref uint waveLength, ref double samplePeriod, ref double timeOffset)
        {
            float[] wave = null;
            if (channel == AnalogChannel.ChA)
                wave = dataChA[indexers[channel]];
            else if (channel == AnalogChannel.ChB)
                wave = dataChB[indexers[channel]];

            //increment to next line
            if (++indexers[channel] >= dataChA.Count)
                indexers[channel] = 0;

            //since this wave was read from file: file dictates following settings
            samplePeriod = this.samplePeriod;
            waveLength = (uint)wave.Length;
            timeOffset = 0;

            return wave;
        }

        public override List<AnalogChannel> ChannelsToAcquireDataFor()
        {
            return AnalogChannel.List.ToList();
        }
    }
}
