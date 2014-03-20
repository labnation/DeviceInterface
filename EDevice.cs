using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Reflection;
using ECore.DeviceImplementations;

namespace ECore
{
    //ideally would be to constrain interfaces to be applied only to AuxFunctionalities class
    //how to use generics for memory? after propagation, EDevice needs to have a List of memories, so it needs to know all the specific memory types. or just use an arraylist?
    //strobes could be a parameter, as it is built on other registers. or an aux functionality!
    //parameters should define affected memories, and their registers as strings

    //main class, from which all specific cameras inherit
    public class EDevice
    {
        //properties regarding camera
        private EDeviceImplementation deviceImplementation;
        //FIXME: what the hell is this romManager doing here?
        private DeviceImplementations.ScopeV2.ScopeV2RomManager romManager;
        
        //properties regarding thread management
        private Thread dataFetchThread;
        private bool running;
        //FIXME: move initialization of datasources to device implementation
        private DataSource dataSource;

		//events
		public event NewDataAvailableHandler OnNewDataAvailable;

#if false
		#if ANDROID
		public static Android.Content.Context ApplicationContext;
		public EDevice(Type deviceImplementationType, Android.Content.Context appContext): this(deviceImplementationType)
		{
			ApplicationContext = appContext;
		}
		#endif        
#endif

        public EDevice(Type deviceImplementationType)
        {
            //this.deviceImplementation = (EDeviceImplementation)Activator.CreateInstance(deviceImplementationType);
            this.deviceImplementation = new DeviceImplementations.ScopeV2(this);
            deviceImplementation.InitializeHardwareInterface();
            this.romManager = deviceImplementation.CreateRomManager();

            this.running = false;

            Logger.AddEntry(this, LogMessageType.ECoreInfo, "EDevice initialized");
        }

        //start new thread, which will only fetch new data
        public void Start()
        {
            running = true;            

            //check whether physical HW device is connected. if not, load data from a stream
			if (HWInterface.Connected)
                //load data from a device
                //FIXME: move datasource initialization to deviceimplementation
                dataSource = new DataSources.DataSourceScopeV2((ScopeV2)this.deviceImplementation);
            else
                //load data from a stream                
                dataSource = new DataSources.DataSourceFile();

            //create and start thread, operating on dataGeneratorNode
            dataFetchThread = new Thread(RunThreadDataGenerator);
            dataFetchThread.Name = "DataFetchFromDeviceThread";
            dataFetchThread.Priority = ThreadPriority.AboveNormal;
            dataFetchThread.Start();
        }

        public void StartFromEmbedded()
        {
            running = true;

            //check whether physical HW device is connected. if not, load data from a stream
            
            dataSource = new DataSources.DataSourceEmbeddedResource();

            //create and start thread, operating on dataGeneratorNode
            dataFetchThread = new Thread(RunThreadDataGenerator);
            dataFetchThread.Name = "DataFetchFromDeviceThread";
            dataFetchThread.Priority = ThreadPriority.AboveNormal;
            dataFetchThread.Start();
        }

        public void RunThreadDataGenerator()
        {           
            //main starting point for the thread which fetches the data from file
            Logger.AddEntry(this, LogMessageType.ECoreInfo, "DataFetchThread spawn");

            //start HW
            //FIXME: make this line part of StartDevice
            HWInterface.Stop();
            deviceImplementation.Start();

            //looping until device is stopped
            while (running)
            {
                //update data
                dataSource.Update();

                //flag that new data has arrived
                if (OnNewDataAvailable != null)
                    OnNewDataAvailable(dataSource.LatestDataPackage,  new EventArgs());

                //Stop();
            }
        }

        public void Stop()
        {
            //stops acquisition thread
            running = false;

            //stop HW
            //dataFetchThread.Join(); --> We should do this here but it causes deadlock cos of logging not being asynchronous!!!
            deviceImplementation.Stop();
            //FIXME: make this line part of StopDevice
            deviceImplementation.hardwareInterface.Start();

            //add entry to log
            Logger.AddEntry(this, LogMessageType.ECoreInfo, "DataFetchThread stopped now");
        }

//FIXME: make the following 4 fields private when in RELEASE
        public EDeviceHWInterface HWInterface { get { return this.deviceImplementation.hardwareInterface; } }
        public EDeviceImplementation DeviceImplementation { get { return this.deviceImplementation; } }
        public DeviceImplementations.ScopeV2.ScopeV2RomManager RomManager { get { return this.romManager; } }
        public bool IsRunning { get { return running; } }


        #region settings

        static public String SettingSetterMethodName(Setting s)
        {
            String methodName = "Set" + Utils.SnakeToCamel(Enum.GetName(s.GetType(), s));
            return methodName;
        }

        public bool HasSetting(Setting s)
        {
            return this.deviceImplementation.HasSetting(s);
        }

        public void Set(Setting s, Object[] parameters) {
            if (!this.deviceImplementation.HasSetting(s))
                throw new MissingSettingException(this.deviceImplementation, s);
            MethodInfo m = this.deviceImplementation.GetType().GetMethod(SettingSetterMethodName(s));
            ParameterInfo[] pi = m.GetParameters();
            if (parameters == null || pi.Length != parameters.Length)
                throw new SettingParameterWrongNumberException(this.deviceImplementation, s,
                    pi.Length, parameters != null ? parameters.Length : 0);
            //Match parameters with method arguments
            
            for(int i = 0; i < pi.Length; i++)
            {
                if (!pi[i].ParameterType.Equals(parameters[i].GetType())) {
                    throw new SettingParameterTypeMismatchException(this.deviceImplementation, s,
                        i+1, pi[i].ParameterType, parameters[i].GetType());
                }
            }
            m.Invoke(this.deviceImplementation, parameters);
        }

        #endregion
    }
}

