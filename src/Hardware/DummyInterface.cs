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
        private List<byte[]> dataLA;
        private int currentRecord = 0;

        public double SamplePeriod { get; private set; }
        public int NrSamples { get; private set; }
        public double AcquisitionLenght { get; private set; }
        public float RelativeFilePosition { get; private set; }
        public int NrWaveforms { get; private set; }

        public DummyInterfaceFromFile(string filename) : base(DummyInterface.File)
        {
            this.LoadDataFromFile(filename);
        }

        private void LoadDataFromFile(string filename)
        {
            MatfileReader matfileReader = new MatlabFileIO.MatfileReader(filename);
            if (!matfileReader.Variables.ContainsKey("SamplePeriodInSeconds"))
                throw new Exception(".mat file not compatible");

            //load data
            dataChA = LoadAnalogChannelFromMatlabFile<float>("ChannelA", matfileReader);
            dataChB = LoadAnalogChannelFromMatlabFile<float>("ChannelB", matfileReader);
            dataLA = LoadAnalogChannelFromMatlabFile<byte>("ChannelLA", matfileReader);
            SamplePeriod = (double)matfileReader.Variables["SamplePeriodInSeconds"].data;

            //calc fixed values
            if (dataChA != null)
            {
                NrSamples = dataChA[0].Length;
                NrWaveforms = dataChA.Count;
            }
            else
            {
                NrSamples = dataLA[0].Length;
                NrWaveforms = dataLA.Count;
            }
            
            if (NrWaveforms > 0)
                this.AcquisitionLenght = SamplePeriod * (double)NrSamples;
            else
                this.AcquisitionLenght = 0;
        }

        private List<T[]> LoadAnalogChannelFromMatlabFile<T>(string channelName, MatfileReader matfileReader)
        {
            //if ChannelA data exists: extract slowly into memory in a format which is fast to look up
            if (!matfileReader.Variables.ContainsKey(channelName))
            {
                return null;
            }
            else
            {
                List<T[]> dataCh = new List<T[]>();
                var readValues = matfileReader.Variables[channelName].data;

                if (readValues is T[])
                {
                    dataCh.Add((T[])readValues);
                }
                else if (readValues is T[,])
                {
                    T[,] values2d = (T[,])readValues;
                    for (int i = 0; i < values2d.GetLength(0); i++)
                    {
                        T[] temp = new T[values2d.GetLength(1)];
                        for (int j = 0; j < temp.Length; j++)
                            temp[j] = values2d[i, j];
                        dataCh.Add(temp);
                    }
                }

                return dataCh;
            }            
        }

        public T[] GetWaveFromFile<T>(Channel channel, ref uint waveLength, ref double samplePeriod)
        {
            Array wave = null;
            if (channel == AnalogChannel.ChA)
            {
                if (dataChA == null) return new T[0];
                if (dataChA.Count < currentRecord + 1) return new T[0];
                wave = dataChA[currentRecord];
            }
            else if (channel == AnalogChannel.ChB)
            {
                if (dataChB == null) return new T[0];
                if (dataChB.Count < currentRecord + 1) return new T[0];
                wave = dataChB[currentRecord];
            }
            else if (channel == LogicAnalyserChannel.LA)
            {
                if (dataLA == null) return new T[0];
                if (dataLA.Count < currentRecord + 1) return new T[0];
                wave = dataLA[currentRecord];
            }

            //since this wave was read from file: file dictates following settings
            samplePeriod = this.SamplePeriod;
            waveLength = (uint)wave.Length;

            return (T[])wave;
        }

        public void IncrementRecord()
        {
            //increment to next line
            if (++currentRecord >= NrWaveforms)
                currentRecord = 0;
            RelativeFilePosition = (float)currentRecord / (float)NrWaveforms;
        }

        public override List<AnalogChannel> ChannelsToAcquireDataFor()
        {
            return AnalogChannel.List.ToList();
        }
    }
}
