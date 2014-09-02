using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.Devices;

namespace ECore.DataSources
{
    public class RecordingScope : IDisposable
    {
#if INTERNAL
        public
#else
        internal
#endif            
        Dictionary<Channel, IChannelBuffer> channelBuffers;
#if INTERNAL
        public
#else
        internal
#endif            
        List<AcquisitionInfo> acqInfo;
#if INTERNAL
        public
#else
        internal
#endif
        Dictionary<string, List<double>> settings;
        public int AcquisitionsRecorded { get; private set; }
        public long DataStorageSize { get; private set; }
        bool disposed = false;
        public bool Busy { get; private set; }
        private object busyLock = new object();

        public RecordingScope() {
            Busy = true;
            acqInfo = new List<AcquisitionInfo>();
            channelBuffers = new Dictionary<Channel, IChannelBuffer>();
            settings = new Dictionary<string, List<double>>();

            foreach (AnalogChannel ch in AnalogChannel.List)
                channelBuffers.Add(ch, new ChannelBufferFloat("Channel" + ch.Name));

            foreach (LogicAnalyserChannel ch in LogicAnalyserChannel.List)
                channelBuffers.Add(ch, new ChannelBufferByte("LogicAnalyser" + ch.Name));
        }

        ~RecordingScope()
        {
            Dispose(false);
        }
#if INTERNAL
        public
#else
        internal
#endif
        struct AcquisitionInfo
        {
            public int samples;
            public double samplePeriod;
            public UInt64 firstSampleTime;
        }

        protected void Dispose(bool disposing)
        {
            if (!disposed)
            {
                foreach (IChannelBuffer b in this.channelBuffers.Values)
                    b.Destroy();
                if (disposing)
                    this.channelBuffers = null;
                disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        //FIXME: this needs to go
#if INTERNAL
        public Dictionary<string, object> matlabVariables = new Dictionary<string, object>();
        public void AddMatlabVariable(object o, string name)
        {
            matlabVariables.Add(name, o);
        }
#endif
        internal void SetNotBusy()
        {
            lock (busyLock)
            {
                Busy = false;
            }
        }

        public void Record(DataPackageScope ScopeData, EventArgs e)
        {
            lock (busyLock)
            {
                foreach (var kvp in channelBuffers)
                {
                    if (kvp.Key is AnalogChannel)
                        kvp.Value.AddData(ScopeData.GetData(kvp.Key as AnalogChannel));
                    if (kvp.Key is LogicAnalyserChannel)
                        kvp.Value.AddData(ScopeData.GetDataDigital());
                }
                DataStorageSize = channelBuffers.Select(x => x.Value.BytesStored()).Sum();

                acqInfo.Add(
                    new AcquisitionInfo()
                    {
                        firstSampleTime = ScopeData.FirstSampleTime,
                        samples = ScopeData.Samples,
                        samplePeriod = ScopeData.SamplePeriod
                    });
                foreach (var kvp in ScopeData.Settings)
                {
                    if (!settings.Keys.Contains(kvp.Key))
                        settings.Add(kvp.Key, new List<double>());

                    settings[kvp.Key].Add(kvp.Value);
                }

                AcquisitionsRecorded++;
            }
        }
    }
}
