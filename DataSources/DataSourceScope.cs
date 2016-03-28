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

        private bool running = false;
        private bool paused = false;

        private void fireDataAvailableEvents()
        {
            if (OnNewDataAvailable != null)
                OnNewDataAvailable(LatestDataPackage, this);
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

    }
}
