using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.EDataNodes
{
    public class EDataNodeFromDevice: EDataNode
    {
        private EDevice eDevice;        
        private EDataPackage lastDataPackage;
        public bool RawDataPassThrough;

        public EDataNodeFromDevice(EDevice eDevice)
        {
            this.eDevice = eDevice;
            this.RawDataPassThrough = false;
        }
        
        public override EDataPackage LatestDataPackage
        {
            get
            {
                return lastDataPackage;
            }
        }

        public override void Update(EDataNode sender, EventArgs e)
        {
            float[] rawValues = eDevice.DeviceImplementation.GetRawData();
            float[] voltageValues = rawValues;

            //the following option allows the raw data to be passed through, required for calibrating the data
            if (!RawDataPassThrough)
                voltageValues = eDevice.DeviceImplementation.ConvertRawDataToVoltages(rawValues);

            //convert data into an EDataPackage
            lastDataPackage = new EDataPackage(voltageValues);
        }
    }
}
