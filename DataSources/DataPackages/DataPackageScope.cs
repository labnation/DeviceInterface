using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.Devices;
using LabNation.Common;

namespace LabNation.DeviceInterface.DataSources
{
    public enum DataSourceType
    {
        Acquisition,
        Viewport,
        Overview,
        ETSVoltages,
        ETSTimestamps
    }

    public class ChannelData
    {
        public DataSourceType source { get; private set; } 
        public Channel channel { get; private set; }
        /// <summary>
        /// Underlying data. WARNING: do not modify its elements. If modification is required,
        /// create a new array and with that, a new ChannelData object
        /// </summary>
        public Array array { get; private set; }
        public bool partial { get; private set; }
        public double samplePeriod { get; private set; }
        public double timeOffset { get; private set; }
        
        public ChannelData(DataSourceType t, Channel channel, Array data, bool partial, double samplePeriod, double timeOffset = 0)
        {
            this.partial = partial;
            this.source = t;
            this.channel = channel;
            this.array = data;
            this.timeOffset = timeOffset;
            this.samplePeriod = samplePeriod;
        }
    }

    /// <summary>
    /// The object containing a scope acquisition's data.
    /// </summary>
    public class DataPackageScope
    {
        readonly object dataLock = new object();
        private static int OVERVIEW_SAMPLES = 2048;
        private static Dictionary<Type, Type> ChannelDataTypes = new Dictionary<Type, Type>() {
            { typeof(AnalogChannel), typeof(float) },
            { typeof(DigitalChannel), typeof(bool) },
            { typeof(LogicAnalyserChannel), typeof(byte) },
            { typeof(AnalogChannelRaw), typeof(byte) }
        };

        public Dictionary<AnalogChannel, float> Resolution { get; private set; }

        public Type ScopeType { get; private set; }

        private Dictionary<DataSourceType, Dictionary<Channel, ChannelData>> data;
        public int LatestChunkSize { get; private set; }        

#if DEBUG
        internal SmartScopeHeader header = null;
#endif

        public Dictionary<string, double> Settings { get; private set; }
        public Dictionary<DataSourceType, double> samplePeriod;
        public Dictionary<DataSourceType, double> offset;
        public Dictionary<Channel, float> SaturationLowValue = new Dictionary<Channel, float>();
        public Dictionary<Channel, float> SaturationHighValue = new Dictionary<Channel, float>();

        internal DataPackageScope(
            Type scopeType,
            uint acquiredSamples, double acqSamplePeriod, 
            int viewportSamples, Int64 viewportOffsetSamples,
            double holdoff, Int64 holdoffSamples, bool rolling, int identifier, double viewportExcess = 0)
        {
            this.ScopeType = scopeType;
            this.Identifier = identifier;
            this.AcquisitionSamples = acquiredSamples;

            this.ViewportSamples = viewportSamples;
            this.ViewportExcess = viewportExcess;
            this.ViewportOffsetSamples = viewportOffsetSamples;

            samplePeriod = new Dictionary<DataSourceType, double>() {
                { DataSourceType.Acquisition, acqSamplePeriod },
                { DataSourceType.Overview, acquiredSamples / OVERVIEW_SAMPLES * acqSamplePeriod },
            };

            offset = new Dictionary<DataSourceType, double>() {
                { DataSourceType.Acquisition, 0 },
                { DataSourceType.Overview, 0 },
            };

            this.Holdoff = holdoff;
            this.Rolling = rolling;
            this.HoldoffSamples = holdoffSamples;

            data = new Dictionary<DataSourceType, Dictionary<Channel, ChannelData>>();
            foreach(DataSourceType t in Enum.GetValues(typeof(DataSourceType)))
                data[t] = new Dictionary<Channel, ChannelData>();

            Settings = new Dictionary<string,double>();
            Resolution = new Dictionary<AnalogChannel, float>() {
                { AnalogChannel.ChA, float.PositiveInfinity },
                { AnalogChannel.ChB, float.PositiveInfinity },
            };
        }       

        internal void SetData(DataSourceType type, Channel ch, Array arr, bool partial = false)
        {
            if (arr == null)
                return;

            //TODO: check with Jasper if this is OK to do!
            //Why this return should be here: when the following condition is true, data which might already be processed (eg timesmoothing) is being overwritten by native data.
            if (data[type].ContainsKey(ch))
                return;

            lock (dataLock)
            {
                if (arr.GetType().GetElementType() != ChannelDataTypes[ch.GetType()])
                    throw new Exception("Invalid data type " + arr.GetType().GetElementType().ToString() + " for channel of type " + ch.GetType().ToString());

                data[type][ch] = new ChannelData(type, ch, arr, partial, samplePeriod[type], offset[type]);
            }
        }

        internal void AddData(DataSourceType type, Channel ch, Array arrayToAdd)
        {           
            ChannelData arrayWeHad = GetData(type, ch);

            int MaxElements;
            switch (type)
            {
                case DataSourceType.Acquisition:
                    MaxElements = (int)AcquisitionSamples;
                    break;
                case DataSourceType.Overview:
                    MaxElements = OVERVIEW_SAMPLES;
                    break;
                case DataSourceType.Viewport:
                    MaxElements = ViewportSamples;
                    break;
                default:
                    throw new Exception("Unhandled type");
            }

            int arrayToAddElements = Math.Min(arrayToAdd.Length, MaxElements);
            int arrayWeHadElements = arrayWeHad == null ? 0 : Math.Min(arrayWeHad.array.Length, MaxElements - arrayToAddElements);
            this.LatestChunkSize = arrayToAddElements;

            //When adding all elements from new array, don't bother copying things togehter
            if (arrayWeHadElements <= 0 && arrayToAddElements == arrayToAdd.Length)
            {
                SetData(type, ch, arrayToAdd, arrayToAdd.Length < MaxElements);
                return;
            }

            Array arrayResult = Array.CreateInstance(arrayToAdd.GetType().GetElementType(), arrayToAddElements + arrayWeHadElements);
            if(arrayWeHadElements > 0)
                Array.Copy(arrayWeHad.array, arrayWeHad.array.Length - arrayWeHadElements, arrayResult, 0, arrayWeHadElements);
            Array.Copy(arrayToAdd, arrayToAdd.Length - arrayToAddElements, arrayResult, arrayWeHadElements, arrayToAddElements);
            SetData(type, ch, arrayResult, arrayResult.Length < MaxElements);
        }
        public ChannelData GetData(DataSourceType type, Channel ch)
        {
            lock (dataLock)
            {
                if (data[type].ContainsKey(ch))
                    return data[type][ch];
                else if (ch is DigitalChannel)
                    return ExtractBitsFromLogicAnalyser((DigitalChannel)ch, type);
            }
            return null;
        }
        private ChannelData ExtractBitsFromLogicAnalyser(DigitalChannel ch, DataSourceType t)
        {
            lock (dataLock)
            {
                //FIXME: expand for more than 1 LA
                if (!data[t].ContainsKey(LogicAnalyserChannel.LA))
                    return null;
                Func<byte, bool> bitFilter = new Func<byte, bool>(x => Utils.IsBitSet(x, ch.Value));
                var laChannel = data[t][LogicAnalyserChannel.LA];
                data[t][ch] = new ChannelData(t, ch, Utils.TransformArray(laChannel.array, bitFilter), laChannel.partial, samplePeriod[t], offset[t]);
                return data[t][ch];
            }
        }

        /// <summary>
        /// Unique identifier for package
        /// </summary>
        public int Identifier { get; private set; }

        public double AcquisitionLength { get { return AcquisitionSamples * samplePeriod[DataSourceType.Acquisition]; } }

        /// <summary>
        /// The number of samples acquired
        /// </summary>
        public uint AcquisitionSamples { get; private set; }

        /// <summary>
        /// The trigger holdoff in seconds
        /// </summary>
        public double Holdoff { get; private set; }
        /// <summary>
        /// The trigger holdoff in samples
        /// </summary>
        public Int64 HoldoffSamples { get; private set; }
        /// <summary>
        /// The holdoff relative to the center of the acquisition buffer
        /// </summary>
        public double HoldoffCenter { get { return Holdoff - AcquisitionLength / 2.0; } }
        /// <summary>
        /// Indicates how far we have fetched the full acquisition of this package
        /// </summary>
        public float FullAcquisitionFetchProgress { get; internal set; }

        /// <summary>
        /// True when the scope is in rolling mode
        /// </summary>
        public bool Rolling { get; private set; }

        /// <summary>
        /// The time of excessive samples leading the viewport buffer
        /// </summary>
        public double ViewportExcess { get; private set; }

        /// <summary>
        /// The number of samples stored per channel
        /// </summary>
        public int ViewportSamples { get; private set; }
        public Int64 ViewportOffsetSamples { get; private set; }
        public double ViewportTimespan { get { return samplePeriod[DataSourceType.Viewport] * ViewportSamples; } } 
    }
}
