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

        public bool IsRecording { get; private set; }
        public bool CanStore { get; private set; }
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

        private void DataFetchThreadStart()
        {           
            //main starting point for the thread which fetches the data from file
            Logger.Info("DataFetchThread spawn");

            if (!running)
                Logger.Error("Device not started as device.Start() didn't return true");

            //looping until device is stopped
            while (running && device.Connected)
            {
                latestDataPackage = scope.GetScopeData();
                if (latestDataPackage != null)
                    this.fireDataAvailableEvents();
            }
            Logger.Info("Data fetch thread stopped");
        }

        public bool StartRecording()
        {
            if (CanStore || IsRecording)
                return false;

            Recording = new RecordingScope();

            foreach (AnalogChannel ch in AnalogChannel.list)
                Recording.channelBuffers.Add(ch, new ChannelBufferFloat("Channel" + ch.Name));

            foreach (LogicAnalyserChannel ch in LogicAnalyserChannel.list)
                Recording.channelBuffers.Add(ch, new ChannelBufferByte("LogicAnalyser" + ch.Name));

            OnNewDataAvailable += Recording.Record;

            this.IsRecording = true;
            return IsRecording;
        }

        public bool StopRecording()
        {
            if (!this.IsRecording)
                return false;

            OnNewDataAvailable -= Recording.Record;
            this.IsRecording = false;
            if (Recording.acqInfo.Count > 0)
                this.CanStore = true;
            else
                this.CanStore = false;

            return true;
        }

        public void DestroyRecording()
        {
            if (IsRecording)
                StopRecording();
            Recording.Dispose();
            Recording = null;
        }


    }
}
