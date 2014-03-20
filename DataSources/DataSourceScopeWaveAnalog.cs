using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore;
using ECore.DataPackages;
using ECore.DeviceImplementations;

namespace ECore.DataSources
{
    public class DataSourceScopeWaveAnalog: DataSource
    {
        private Scope scope;        
        private DataPackageWaveAnalog lastDataPackage;
        public bool RawDataPassThrough;

        public DataSourceScopeWaveAnalog(Scope scope)
        {
            this.scope = scope;
            this.RawDataPassThrough = false;
        }
        
        public override bool Update()
        {
            lastDataPackage = new DataPackageWaveAnalog(scope.GetVoltages(), 0);
            return true;
        }
    }
}
