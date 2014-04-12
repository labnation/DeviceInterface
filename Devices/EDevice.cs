using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using ECore.DeviceMemories;
using ECore.DataSources;

namespace ECore.Devices
{
    public abstract partial class EDevice
    {
        protected List<DeviceMemory> memories = new List<DeviceMemory>();
        //FIXME: visibility
        public List<DeviceMemory> Memories { get { return memories; } }
        protected List<DataSource> dataSources = new List<DataSource>();
        public List<DataSource> DataSources { get { return this.dataSources; } }
        public abstract bool Connected { get; }

        //properties regarding thread management
        private Thread dataFetchThread;
        private bool dataFetchThreadRunning;

#if false
		#if ANDROID
		public static Android.Content.Context ApplicationContext;
		public EDevice(Type deviceImplementationType, Android.Content.Context appContext): this(deviceImplementationType)
		{
			ApplicationContext = appContext;
		}
		#endif        
#endif

        public EDevice()
        {
            this.dataFetchThreadRunning = false;
            Logger.AddEntry(this, LogMessageType.ECoreInfo, this.GetType().Name  + " constructed");
        }

        //start new thread, which will only fetch new data
        public virtual bool Start()
        {
            if(this.IsRunning) {
                Logger.AddEntry(this, LogMessageType.ECoreWarning, "Not starting device since it's still running");
                return false;
            }

            dataFetchThreadRunning = true;            
            //create and start thread, operating on dataGeneratorNode
            dataFetchThread = new Thread(DataFetchThreadStart);
            dataFetchThread.Name = "DataFetchFromDeviceThread";
            dataFetchThread.Priority = ThreadPriority.AboveNormal;
            dataFetchThread.Start();
            return true;
        }

        public void DataFetchThreadStart()
        {           
            //main starting point for the thread which fetches the data from file
            Logger.AddEntry(this, LogMessageType.ECoreInfo, "DataFetchThread spawn");

            if (!dataFetchThreadRunning)
                Logger.AddEntry(this, LogMessageType.ECoreError, "Device not started as device.Start() didn't return true");

            //looping until device is stopped
            while (dataFetchThreadRunning && this.Connected)
            {
                //Update each dataSource (OnDataAvailable callback is fired from within)
                foreach (DataSource d in this.DataSources)
                {
                    d.Update();
                }
            }
            Logger.AddEntry(this, LogMessageType.ECoreInfo, "Data thread stopped");
        }

        public virtual void Stop()
        {
            if (!this.IsRunning)
            {
                Logger.AddEntry(this, LogMessageType.ECoreInfo, "Not stopping device since it's not running");
                return;
            }
            
            //stop thread
            dataFetchThreadRunning = false;

            //add entry to log
            Logger.AddEntry(this, LogMessageType.ECoreInfo, "Requested DataFetchThread to stop");
        }

        public bool IsRunning { get { return dataFetchThread != null && dataFetchThread.IsAlive; } }

    }
}

