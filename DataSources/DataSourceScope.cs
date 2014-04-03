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
        private IScope scope;

        public DataSourceScope(IScope scope)
        {
            this.scope = scope;
        }
        
        public override void Update()
        {
            latestDataPackage = scope.GetScopeData();
            if (latestDataPackage != null)
                this.fireDataAvailableEvents();
        }
    }
}
