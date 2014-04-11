using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.DeviceMemories;
using ECore.DataSources;

namespace ECore
{
    abstract public class EDeviceImplementation
    {
        //////////////////////////////////////////////////////////////////
        //shared properties        
        protected EDevice eDevice;
        //FIXME: make me protected in release mode
        public EDeviceHWInterface hardwareInterface;
        protected List<DeviceMemory<MemoryRegister<byte>>> byteMemories;
        protected List<DataSource> dataSources;
        public List<DataSource> DataSources { get { return this.dataSources; } }
        public abstract bool Connected { get; }
        //////////////////////////////////////////////////////////////////
        //contract for inheriters
        abstract public void InitializeMemories();
        abstract public void InitializeHardwareInterface();
        abstract public void InitializeDataSources();
        abstract public bool Start();
        abstract public void Stop();

        //////////////////////////////////////////////////////////////////
        //base functionality implementation
        protected EDeviceImplementation(EDevice eDevice)
        {
            this.eDevice = eDevice;
            byteMemories = new List<DeviceMemory<MemoryRegister<byte>>>();
            InitializeMemories();
            dataSources = new List<DataSource>();
            InitializeDataSources();
        }

        virtual public List<DeviceMemory<MemoryRegister<byte>>> Memories { get { return byteMemories; } }

        public bool HasSetting(Setting s) 
        {
            return Utils.HasMethod(this, EDevice.SettingSetterMethodName(s));
        }
    }
}
