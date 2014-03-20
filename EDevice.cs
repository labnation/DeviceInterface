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
        
        //properties regarding thread management
        private Thread dataFetchThread;
        private bool running;

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
            //this.deviceImplementation = (EDeviceImplementation)Activator.CreateInstance(deviceImplementationType, new object[] {this});
            //this.deviceImplementation = new DeviceImplementations.ScopeV2(this);
            this.deviceImplementation = new DeviceImplementations.ScopeDummy(this);
            deviceImplementation.InitializeHardwareInterface();

            this.running = false;

            Logger.AddEntry(this, LogMessageType.ECoreInfo, "EDevice initialized");
        }

        //start new thread, which will only fetch new data
        public void Start()
        {
            running = true;            
            //create and start thread, operating on dataGeneratorNode
            dataFetchThread = new Thread(RunThreadDataGenerator);
            dataFetchThread.Name = "DataFetchFromDeviceThread";
            dataFetchThread.Priority = ThreadPriority.AboveNormal;
            dataFetchThread.Start();
        }

        /*public void StartFromEmbedded()
        {
            running = true;

            //check whether physical HW device is connected. if not, load data from a stream
            
            dataSources.Add(new DataSources.DataSourceEmbeddedResource());

            //create and start thread, operating on dataGeneratorNode
            dataFetchThread = new Thread(RunThreadDataGenerator);
            dataFetchThread.Name = "DataFetchFromDeviceThread";
            dataFetchThread.Priority = ThreadPriority.AboveNormal;
            dataFetchThread.Start();
        }*/

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
                //Update each dataSource (callback is fired from within)
                foreach (DataSource d in this.deviceImplementation.DataSources)
                {
                    d.Update();
                }
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

