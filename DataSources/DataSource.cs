using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.Devices;

namespace ECore.DataSources
{
    public delegate void NewDataAvailableHandler(DataPackageScope dataPackage, EventArgs e);
    
    public abstract class DataSource
    {
#if INTERNAL
        public event NewDataAvailableHandler BeforeNewDataAvailable;
#endif
        public event NewDataAvailableHandler OnNewDataAvailable;
        protected EDevice device;
        protected DateTime lastUpdate;
        public DataPackageScope LatestDataPackage { get; protected set; }

        public DataSource(EDevice device) { this.device = device; }
        abstract public bool Start();
        abstract public void Stop();
        
        protected void fireDataAvailableEvents()
        {
            {
#if INTERNAL
                if (BeforeNewDataAvailable != null)
                    BeforeNewDataAvailable(LatestDataPackage, new EventArgs());
#endif
                if (OnNewDataAvailable != null)
                    OnNewDataAvailable(LatestDataPackage, new EventArgs());
            }
        }
    }
}
