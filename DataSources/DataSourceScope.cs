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
    public class DataSourceScope: DataSource
    {
        private IScope scope;
        private Thread dataFetchThread;
        public RecordingScope Recording { get; private set; }
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

        public DataSourceScope(EDevice scope) : base(scope)
        {
            if (scope as IScope == null)
                throw new Exception("DataSourceScope needs an EDevice implementing the IScope interface to work");
            this.scope = scope as IScope;
        }
        
        public bool IsRunning { get { return dataFetchThread != null && dataFetchThread.IsAlive; } }

        public override bool Start()
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
        public override void Stop()
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
            while (running && device.Connected)
            {
                LatestDataPackage = scope.GetScopeData();
                if (LatestDataPackage != null)
                    this.fireDataAvailableEvents();
            }
            Logger.Info("Data fetch thread stopped");
        }

        public bool StartRecording()
        {
            if (Recording != null)
            {
                Logger.Warn("Can't start recording since a previous recording still exists");
                return false;
            }

            Recording = new RecordingScope();

            OnNewDataAvailable += Recording.Record;
            return true;
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

            OnNewDataAvailable -= Recording.Record;
            Recording.SetNotBusy();
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
