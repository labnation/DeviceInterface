using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.Devices;
using LabNation.Common;

namespace LabNation.DeviceInterface.DataSources
{
    public class ChannelDataSource
    {
        public string Name { get; protected set; }
        public int Value { get; protected set; }
        public static explicit operator int(ChannelDataSource ch) { return ch.Value; }

        private static HashSet<ChannelDataSource> list = new HashSet<ChannelDataSource>();
        public static IList<ChannelDataSource> List { get { return list.ToList().AsReadOnly(); } }
        public ChannelDataSource(string name, int value)
        {
            this.Name = name;
            this.Value = value;
            list.Add(this);
        }

        static public implicit operator string(ChannelDataSource ds)
        {
            return ds == null ? null : ds.GetType().Name + "-" + ds.Name;
        }
    }
    public sealed class ChannelDataSourceScope : ChannelDataSource
    {
        private static HashSet<ChannelDataSourceScope> list = new HashSet<ChannelDataSourceScope>();
        new public static IList<ChannelDataSourceScope> List { get { return list.ToList().AsReadOnly(); } }
        private ChannelDataSourceScope(string name, int value)
            : base(name, value)
        {
            list.Add(this);
        }
        public static readonly ChannelDataSourceScope Acquisition = new ChannelDataSourceScope("Acquisition", 0);
        public static readonly ChannelDataSourceScope Viewport = new ChannelDataSourceScope("Viewport", 1);
        public static readonly ChannelDataSourceScope Overview = new ChannelDataSourceScope("Overview", 2);
    }

    public class ChannelData
    {
        public ChannelDataSource source { get; private set; } 
        public Channel channel { get; private set; }
        /// <summary>
        /// Underlying data. WARNING: do not modify its elements. If modification is required,
        /// create a new array and with that, a new ChannelData object
        /// </summary>
        public Array array { get; private set; }
        public bool partial { get; private set; }
        public double samplePeriod { get; private set; }
        public double timeOffset { get; private set; }
        
        public ChannelData(ChannelDataSource t, Channel channel, Array data, bool partial, double samplePeriod, double timeOffset = 0)
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

        private Dictionary<ChannelDataSourceScope, Dictionary<Channel, ChannelData>> data;
        public int LatestChunkSize { get; private set; }
        public DateTime LastDataUpdate { get; private set; }
        public TriggerValue TriggerValue { get; private set; }
        
#if DEBUG
        internal SmartScopeHeader header = null;
#endif

        public Dictionary<string, double> Settings { get; private set; }
        public Dictionary<ChannelDataSource, double> samplePeriod;
        public Dictionary<ChannelDataSource, double> offset;
        public Dictionary<Channel, float> SaturationLowValue = new Dictionary<Channel, float>();
        public Dictionary<Channel, float> SaturationHighValue = new Dictionary<Channel, float>();

        internal DataPackageScope(
            Type scopeType,
            uint acquiredSamples, double acqSamplePeriod, 
            int viewportSamples, Int64 viewportOffsetSamples,
            double holdoff, Int64 holdoffSamples, bool rolling, int identifier, TriggerValue triggerValue, 
            double viewportExcess = 0)
        {
            this.TriggerValue = triggerValue.Copy();
            this.LastDataUpdate = DateTime.Now;
            this.ScopeType = scopeType;
            this.Identifier = identifier;
            this.AcquisitionSamples = acquiredSamples;

            this.ViewportSamples = viewportSamples;
            this.ViewportExcess = viewportExcess;
            this.ViewportOffsetSamples = viewportOffsetSamples;

            samplePeriod = new Dictionary<ChannelDataSource, double>() {
                { ChannelDataSourceScope.Acquisition, acqSamplePeriod },
                { ChannelDataSourceScope.Overview, acquiredSamples / OVERVIEW_SAMPLES * acqSamplePeriod },
            };

            offset = new Dictionary<ChannelDataSource, double>() {
                { ChannelDataSourceScope.Acquisition, 0 },
                { ChannelDataSourceScope.Overview, 0 },
            };

            this.Holdoff = holdoff;
            this.Rolling = rolling;
            this.HoldoffSamples = holdoffSamples;

            data = new Dictionary<ChannelDataSourceScope, Dictionary<Channel, ChannelData>>();
            foreach(ChannelDataSourceScope t in ChannelDataSourceScope.List)
                data[t] = new Dictionary<Channel, ChannelData>();

            Settings = new Dictionary<string,double>();
            Resolution = new Dictionary<AnalogChannel, float>() {
                { AnalogChannel.ChA, float.PositiveInfinity },
                { AnalogChannel.ChB, float.PositiveInfinity },
            };
        }

        internal void UpdateTimestamp()
        {
            LastDataUpdate = DateTime.Now;
        }

        internal void SetData(ChannelDataSourceScope type, Channel ch, Array arr, bool partial = false)
        {
            if (arr == null)
                return;

            lock (dataLock)
            {
                if (arr.GetType().GetElementType() != ChannelDataTypes[ch.GetType()])
                    throw new Exception("Invalid data type " + arr.GetType().GetElementType().ToString() + " for channel of type " + ch.GetType().ToString());

                data[type][ch] = new ChannelData(type, ch, arr, partial, samplePeriod[type], offset[type]);
                this.LastDataUpdate = DateTime.Now;
            }
        }

        internal void AddData(ChannelDataSourceScope type, Channel ch, Array arrayToAdd)
        {           
            ChannelData arrayWeHad = GetData(type, ch);

            int MaxElements;
            if(type == ChannelDataSourceScope.Acquisition)
                MaxElements = (int)AcquisitionSamples;
            else if(type == ChannelDataSourceScope.Overview)
                MaxElements = OVERVIEW_SAMPLES;
            else if (type == ChannelDataSourceScope.Viewport)
                MaxElements = ViewportSamples;
            else
                throw new Exception("Unhandled type");

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
        public ChannelData GetData(ChannelDataSourceScope type, Channel ch)
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
        private ChannelData ExtractBitsFromLogicAnalyser(DigitalChannel ch, ChannelDataSourceScope t)
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

        public double AcquisitionLength { get { return AcquisitionSamples * samplePeriod[ChannelDataSourceScope.Acquisition]; } }

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
        public double ViewportTimespan { get { return samplePeriod[ChannelDataSourceScope.Viewport] * ViewportSamples; } } 
    }
}
