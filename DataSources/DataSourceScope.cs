using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore;
using ECore.DataPackages;
using ECore.DeviceImplementations;

namespace ECore.DataSources
{
    public class DataSourceScope: DataSource
    {
        private Scope scope;
        private DataPackageScope lastDataPackage;

        public DataSourceScope(Scope scope)
        {
            this.scope = scope;
        }
        
        public override void Update()
        {
            lastDataPackage = scope.GetScopeData();
            this.fireDataAvailableEvents();
        }
    }
}
