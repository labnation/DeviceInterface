using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.HardwareInterfaces
{
    internal abstract class InterfaceManager<T> 
        where T : InterfaceManager<T>, new()
    {
        protected static int VID = 0x04D8;
        protected static int[] PIDs = new int[] { 0x0052, 0xF4B5 };
        protected static Guid guid = new Guid("{7d2c7901-f90b-434d-aae1-38e3e39a3ca1}");
        protected static Dictionary<object, ISmartScopeUsbInterface> interfaces = new Dictionary<object, ISmartScopeUsbInterface>();

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
