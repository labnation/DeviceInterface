using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.HardwareInterfaces
{
    abstract class InterfaceManager<T> 
        where T : InterfaceManager<T>, new()
    {
        protected static Dictionary<string, ISmartScopeUsbInterface> interfaces = new Dictionary<string, ISmartScopeUsbInterface>();

        private static T instance = new T();
        private static bool initialized = false;
        public static T Instance
        {
            get
            {
                if (!initialized)
                {
                    initialized = true;
                    instance.Initialize();
                }
                return instance;
            }
        }

        public OnDeviceConnect onConnect;
        protected abstract void Initialize();
        public abstract void PollDevice();
    }
}
