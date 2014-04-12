using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Reflection;
using ECore.DeviceMemories;
using ECore.DataSources;

namespace ECore.Devices
{
    //ideally would be to constrain interfaces to be applied only to AuxFunctionalities class
    //how to use generics for memory? after propagation, EDevice needs to have a List of memories, so it needs to know all the specific memory types. or just use an arraylist?
    //strobes could be a parameter, as it is built on other registers. or an aux functionality!
    //parameters should define affected memories, and their registers as strings

    //main class, from which all specific cameras inherit
    public abstract class EDevice
    {
        //fIXME: visibility
        protected List<DeviceMemory<MemoryRegister<byte>>> byteMemories = new List<DeviceMemory<MemoryRegister<byte>>>();
        public List<DeviceMemory<MemoryRegister<byte>>> Memories { get { return byteMemories; } }
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


        #region settings

        static public String SettingSetterMethodName(Setting s)
        {
            String methodName = "Set" + Utils.SnakeToCamel(Enum.GetName(s.GetType(), s));
            return methodName;
        }

        public bool HasSetting(Setting s)
        {
            return this.HasSetting(s);
        }

        public void Set(Setting s, Object[] parameters) {
            if (!this.HasSetting(s))
                throw new MissingSettingException(this, s);
            MethodInfo m = this.GetType().GetMethod(SettingSetterMethodName(s));
            ParameterInfo[] pi = m.GetParameters();
            if (parameters == null || pi.Length != parameters.Length)
                throw new SettingParameterWrongNumberException(this, s,
                    pi.Length, parameters != null ? parameters.Length : 0);
            //Match parameters with method arguments
            
            for(int i = 0; i < pi.Length; i++)
            {
                if (!pi[i].ParameterType.Equals(parameters[i].GetType())) {
                    throw new SettingParameterTypeMismatchException(this, s,
                        i+1, pi[i].ParameterType, parameters[i].GetType());
                }
            }
            m.Invoke(this, parameters);
        }

        #endregion
    }
}

