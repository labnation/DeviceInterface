using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.DataPackages;
using ECore.Devices;

namespace ECore.DataSources
{
    public delegate void NewDataAvailableHandler(DataPackageScope dataPackage, EventArgs e);
    
    
    public abstract class DataSource
    {
        public event NewDataAvailableHandler OnNewDataAvailable;
        protected EDevice device;
        protected DateTime lastUpdate;
        protected DataPackageScope latestDataPackage;

        public DataSource(EDevice device) { this.device = device; }
        abstract public bool Start();
        abstract public void Stop();
        
        protected void fireDataAvailableEvents()
        {
            {
                if (OnNewDataAvailable != null)
                    OnNewDataAvailable(latestDataPackage, new EventArgs());
            }
        }
    }
}
