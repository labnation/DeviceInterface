using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using LabNation.DeviceInterface;
using LabNation.DeviceInterface.Devices;
using LabNation.Common;

namespace LabNation.DeviceInterface.DataSources
{
    public delegate void NewDataAvailableHandler(DataPackageScope dataPackage, DataSource dataSource);

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
        public TimeSpan RecordingInterval { get; set; }
        private DateTime RecordingLastAcquisitionTimestamp;
        public int RecordingAcquisitionsPerInterval { get; set; }
        private int RecordingAcquisitionsThisInterval;

        private bool running = false;
        private bool paused = false;

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
                    BeforeNewDataAvailable(LatestDataPackage, this);
#endif
                if (Recording != null && Recording.Busy)
                {
                    lock (Recording) //need this lock, as otherwise Recording can be disposed and streams closed between now and when decoder outputs are to be saved
                    {
                        Record(LatestDataPackage, new EventArgs());
                        if (OnNewDataAvailable != null)
                            OnNewDataAvailable(LatestDataPackage, this);
                    }
                }
                else
                {
                    if (OnNewDataAvailable != null)
                        OnNewDataAvailable(LatestDataPackage, this);
                }
            }
        }
        
        internal DataSource(IScope scope)
        {
            this.scope = scope;
            this.RecordingAcquisitionsPerInterval = 1;
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
            paused = false;
            //create and start thread, operating on dataGeneratorNode
            dataFetchThread = new Thread(DataFetchThreadStart);
            dataFetchThread.Name = "Scope data source";
            dataFetchThread.Priority = ThreadPriority.AboveNormal;
            dataFetchThread.Start();
            return true;
        }

        public void Pause()
        {
            if(paused) {
                Logger.Warn("Not pausing datasource since it's already paused");
                return;
            }
            if(!IsRunning)
            {
                Logger.Warn("Not pausing datasource since it's not running");
                return;
            }
            paused = true;
            Stop();
        }

        public void Resume()
        {
            if(!paused) {
                Logger.Warn("Not resuming datasource since it ain't paused");
                return;
            }
            Start();
        }

        public void Stop()
        {
            if (!IsRunning)
            {
                Logger.Info("Datasource stop requested, but not stopping device since it's not running");
                return;
            }            
            //stop thread
            running = false;
            dataFetchThread.Join(1000);
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
            Logger.Debug("DataFetchThread spawn");

            if (!running)
                Logger.Error("Device not started as device.Start() didn't return true");

            //looping until device is stopped
            while (running && scope.Ready)
            {
                LatestDataPackage = scope.GetScopeData();
                if (LatestDataPackage != null)
                    this.fireDataAvailableEvents();
            }
            Logger.Debug("Data fetch thread stopped");
        }

        public bool StartRecording(bool scopeIsRolling)
        {
            if (Recording != null)
            {
                Logger.Warn("Can't start recording since a previous recording still exists");
                return false;
            }

            this.RecordingLastAcquisitionTimestamp = DateTime.Now; 
            this.RecordingAcquisitionsThisInterval = 0;
            
            Recording = new RecordingScope(scopeIsRolling);

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
                Logger.Info("Recording stop requested but was already stopped");
                return false;
            }
            
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
