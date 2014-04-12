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
        public abstract bool Connected { get; }

		#if ANDROID
		public static Android.Content.Context ApplicationContext;
		public EDevice(Type deviceImplementationType, Android.Content.Context appContext): this(deviceImplementationType)
		{
			ApplicationContext = appContext;
		}
		#endif        
    }
}

