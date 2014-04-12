using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.DataPackages;

namespace ECore.DataSources
{
    public delegate void NewDataAvailableHandler(DataPackageScope dataPackage, EventArgs e);
    
    
    public abstract class DataSource
    {
        public event NewDataAvailableHandler OnNewDataAvailable;
        
        public DataSource() { }
        protected DateTime lastUpdate;
        protected DataPackageScope latestDataPackage;
        abstract public void Update();
        protected void fireDataAvailableEvents()
        {
            {
                if (OnNewDataAvailable != null)
                    OnNewDataAvailable(latestDataPackage, new EventArgs());
            }
        }
    }
}
