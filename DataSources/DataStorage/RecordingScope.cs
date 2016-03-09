using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.Devices;

namespace LabNation.DeviceInterface.DataSources
{
    public class RecordingScope : IDisposable
    { 
        public Dictionary<Channel, IChannelBuffer> channelBuffers;
        public List<AcquisitionInfo> acqInfo;
        public Dictionary<string, List<double>> settings;
        public int AcquisitionsRecorded { get; private set; }
        public long DataStorageSize { get; private set; }
        bool disposed = false;
        private bool busy;
        public bool IsRollingRecording { get; private set; }
        private object busyLock = new object();
        public bool Busy
        {
            get { return busy; }
            set
            {
                lock (busyLock)
                {
                    if (value == true)
                        throw new Exception("The Busy flag cannot be set to true, once marked not busy, always not busy");
                    else
                        busy = false;
                }
            }
        }

        public RecordingScope(bool scopeIsRolling) {
            this.IsRollingRecording = scopeIsRolling;
            busy = true;
            acqInfo = new List<AcquisitionInfo>();
            channelBuffers = new Dictionary<Channel, IChannelBuffer>();
            settings = new Dictionary<string, List<double>>();

            Type t = typeof(Channel);
            foreach (Channel ch in Channel.List)
                channelBuffers.Add(ch, new ChannelBuffer("Channel" + ch.Name, ch));
        }

        ~RecordingScope()
        {
            Dispose(false);
        }
        
        public struct AcquisitionInfo
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

        public void Record(Channel ch, Array data, int chunkSize)
        {
            if (!channelBuffers.ContainsKey(ch))
                return;

            AcquisitionsRecorded = (int)Math.Max(AcquisitionsRecorded, channelBuffers[ch].AddData(data, data.Length));
        }

        public void Record(DataPackageScope ScopeData, EventArgs e)
        {
            lock (busyLock)
            {
                if (!Busy)
                {
                    throw new Exception("Can't record because the Busy flag is false");
                }
                foreach (var kvp in channelBuffers)
                {
                    if (ScopeData.GetData(ChannelDataSourceScope.Viewport, kvp.Key) != null)
                    {
                        Array dataArray = ScopeData.GetData(ChannelDataSourceScope.Viewport, kvp.Key).array;
                        AcquisitionsRecorded = (int)Math.Abs(kvp.Value.AddData(dataArray, ScopeData.LatestChunkSize));
                    }
                } 
                DataStorageSize = channelBuffers.Select(x => x.Value.BytesStored()).Sum();

                acqInfo.Add(
                    new AcquisitionInfo()
                    {
                        firstSampleTime = (ulong)(DateTime.Now.TimeOfDay.TotalMilliseconds*1000000.0),
                        samples = ScopeData.ViewportSamples,
                        samplePeriod = ScopeData.samplePeriod[ChannelDataSourceScope.Viewport]
                    });
                foreach (var kvp in ScopeData.Settings)
                {
                    if (!settings.Keys.Contains(kvp.Key))
                        settings.Add(kvp.Key, new List<double>());

                    settings[kvp.Key].Add(kvp.Value);
                }
            }
        }
    }
}
