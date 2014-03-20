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
            float[] voltageValues;

            //the following option allows the raw data to be passed through, required for calibrating the data

            if (!RawDataPassThrough)
                voltageValues = scope.GetVoltages();
            else
                voltageValues = Utils.CastArray<byte, float>(scope.GetBytes());

            //convert data into an EDataPackage
            //FIXME: change 0 to triggerIndex
            lastDataPackage = new DataPackageWaveAnalog(voltageValues, 0);
            return true;
        }
    }
}
