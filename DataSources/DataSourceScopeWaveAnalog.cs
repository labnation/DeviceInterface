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
        private ScopeV2 scope;        
        private DataPackageWaveAnalog lastDataPackage;
        public bool RawDataPassThrough;

        public DataSourceScopeWaveAnalog(ScopeV2 scope)
        {
            this.scope = scope;
            this.RawDataPassThrough = false;
        }
        
        public override bool Update()
        {
            //FIXME: shouldn't get bytes here, but deviceimplementation should implement the conversion to voltage floats
            byte[] buffer = scope.GetBytes();
            float[] voltageValues;

            //the following option allows the raw data to be passed through, required for calibrating the data
            if (!RawDataPassThrough)
                voltageValues = scope.ConvertBytesToVoltages(buffer);
            else
                voltageValues = Utils.CastArray<byte, float>(buffer);

            //convert data into an EDataPackage
            //FIXME: change 0 to triggerIndex
            lastDataPackage = new DataPackageWaveAnalog(voltageValues, 0);
            return true;
        }
    }
}
