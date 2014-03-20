using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.DataPackages;

namespace ECore
{
    public delegate void NewDataAvailableHandler(DataPackageWaveAnalog dataPackage, EventArgs e);

    public abstract class DataSource
    {
        public DataSource() { }
        protected DateTime lastUpdate;
        protected DataPackageWaveAnalog latestDataPackage;
        public DataPackageWaveAnalog LatestDataPackage { get { return this.latestDataPackage; } }
        abstract public void Update();
    }
}
