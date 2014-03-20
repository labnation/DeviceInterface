using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.DataPackages;

namespace ECore
{
    public delegate void NewDataAvailableHandler(DataPackageWaveAnalog dataPackage, EventArgs e);

    public abstract class EDataNode
    {
        public EDataNode() { }
        protected DataPackageWaveAnalog latestDataPackage;
        public DataPackageWaveAnalog LatestDataPackage { get { return this.latestDataPackage; } }
        abstract public void Update();
    }
}
