using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using ECore;
using ECore.DataPackages;
using ECore.Devices;

namespace ECore.DataSources
{
    public class DataSourceScope: DataSource
    {
        private IScope scope;
        private Thread dataFetchThread;
        private bool running = false;

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
                Logger.AddEntry(this, LogMessageType.ECoreWarning, "Not starting datasource since it's still/already running");
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
                Logger.AddEntry(this, LogMessageType.ECoreInfo, "Not stopping device since it's not running");
                return;
            }            
            //stop thread
            running = false;

            Logger.AddEntry(this, LogMessageType.ECoreInfo, "Requested DataFetchThread to stop");
        }

        public void DataFetchThreadStart()
        {           
            //main starting point for the thread which fetches the data from file
            Logger.AddEntry(this, LogMessageType.ECoreInfo, "DataFetchThread spawn");

            if (!running)
                Logger.AddEntry(this, LogMessageType.ECoreError, "Device not started as device.Start() didn't return true");

            //looping until device is stopped
            while (running && device.Connected)
            {
                latestDataPackage = scope.GetScopeData();
                if (latestDataPackage != null)
                    this.fireDataAvailableEvents();
            }
            Logger.AddEntry(this, LogMessageType.ECoreInfo, "Data thread stopped");
        }
    }
}
