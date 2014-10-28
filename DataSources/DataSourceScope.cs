using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using ECore;
using ECore.Devices;
using Common;

namespace ECore.DataSources
{
    public delegate void NewDataAvailableHandler(DataPackageScope dataPackage, EventArgs e);

    public class DataSource
    {
        private IScope scope;
#if DEBUG
        public event NewDataAvailableHandler BeforeNewDataAvailable;
#endif
        public event NewDataAvailableHandler OnNewDataAvailable;
        public DataPackageScope LatestDataPackage { get; protected set; }

        private Thread dataFetchThread;

        // Recording variables
        public RecordingScope Recording { get; private set; }
        private TimeSpan RecordingInterval;
        private DateTime RecordingLastAcquisitionTimestamp;
        private int RecordingAcquisitionsPerInterval;
        private int RecordingAcquisitionsThisInterval;

        private bool running = false;

        public bool RecordingBusy
        {
            get
            {
                if (Recording == null) return false;
                return Recording.Busy;
            }
        }

        public int AcquisitionsRecorded { get { return Recording.AcquisitionsRecorded; } }
        
        private void fireDataAvailableEvents()
        {
            {
#if DEBUG
                if (BeforeNewDataAvailable != null)
                    BeforeNewDataAvailable(LatestDataPackage, new EventArgs());
#endif
                if (OnNewDataAvailable != null)
                    OnNewDataAvailable(LatestDataPackage, new EventArgs());
            }
        }
        
        internal DataSource(IScope scope)
        {
            this.scope = scope;
        }
        
        public bool IsRunning { get { return dataFetchThread != null && dataFetchThread.IsAlive; } }

        public bool Start()
        {
            if (IsRunning)
            {
                Logger.Warn("Not starting datasource since it's still/already running");
                return false;
            }

            running = true;
            //create and start thread, operating on dataGeneratorNode
            dataFetchThread = new Thread(DataFetchThreadStart);
            dataFetchThread.Name = "Scope data source";
            dataFetchThread.Priority = ThreadPriority.AboveNormal;
            dataFetchThread.Start();
            return true;
        }
        public void Stop()
        {
            if (!IsRunning)
            {
                Logger.Info("Not stopping device since it's not running");
                return;
            }            
            //stop thread
            running = false;

            Logger.Debug("Requested DataFetchThread to stop");
        }

        internal void Reset()
        {
            DestroyRecording();
            Stop();
        }

        private void DataFetchThreadStart()
        {           
            //main starting point for the thread which fetches the data from file
            Logger.Info("DataFetchThread spawn");

            if (!running)
                Logger.Error("Device not started as device.Start() didn't return true");

            //looping until device is stopped
            while (running && scope.Ready)
            {
                LatestDataPackage = scope.GetScopeData();
                if (LatestDataPackage != null)
                    this.fireDataAvailableEvents();
            }
            Logger.Info("Data fetch thread stopped");
        }

        public bool StartRecording()
        {
            return StartRecording(TimeSpan.Zero, 0);
        }

        public bool StartRecording(TimeSpan timeInterval, int acquisitionsPerInterval)
        {

            if (Recording != null)
            {
                Logger.Warn("Can't start recording since a previous recording still exists");
                return false;
            }


            this.RecordingLastAcquisitionTimestamp = DateTime.Now; 
            this.RecordingInterval = timeInterval;
            this.RecordingAcquisitionsPerInterval = acquisitionsPerInterval;
            this.RecordingAcquisitionsThisInterval = 0;
            
            Recording = new RecordingScope();

            OnNewDataAvailable += Record;
            return true;
        }

        private void Record(DataPackageScope dataPackage, EventArgs e)
        {
            //Only do the whole acquisitions per interval checking if the interval
            //and acqs per interval is greater than zero
            if (RecordingInterval > TimeSpan.Zero && RecordingAcquisitionsPerInterval > 0)
            {
                DateTime now = DateTime.Now;
                if (now.Subtract(RecordingLastAcquisitionTimestamp) > RecordingInterval)
                {
                    this.RecordingAcquisitionsThisInterval = 0;
                    this.RecordingLastAcquisitionTimestamp = now;
                }

                //exit in case enough acquisitions have already been stored this interval
                if (++this.RecordingAcquisitionsThisInterval > this.RecordingAcquisitionsPerInterval)
                    return;
            }
            Recording.Record(dataPackage, e);
        }

        public bool StopRecording()
        {
            if (Recording == null)
            {
                Logger.Warn("Can't stop recording since no recording exists");
                return false;
            }
            if (!Recording.Busy)
            {
                Logger.Info("Recording was already stopped");
                return false;
            }

            OnNewDataAvailable -= Record;
            Recording.Busy = false;
            if (Recording.acqInfo.Count == 0)
            {
                Recording.Dispose();
                Recording = null;
                return false;
            }
            return true;
        }

        public void DestroyRecording()
        {
            if (Recording != null)
            {
                StopRecording();
                Recording.Dispose();
                Recording = null;
            }
        }
    }
}
