using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore;
using ECore.DataPackages;

namespace ECore.EDataNodes
{
    public class EDataNodeFromDevice: EDataNode
    {
        private EDevice eDevice;        
        private DataPackageWaveAnalog lastDataPackage;
        public bool RawDataPassThrough;

        public EDataNodeFromDevice(EDevice eDevice)
        {
            this.eDevice = eDevice;
            this.RawDataPassThrough = false;
        }
        
        public override void Update(EDataNode sender, EventArgs e)
        {
            byte[] buffer = eDevice.DeviceImplementation.GetBytes();
            float[] voltageValues;

            //the following option allows the raw data to be passed through, required for calibrating the data
            if (!RawDataPassThrough)
                voltageValues = eDevice.DeviceImplementation.ConvertBytesToVoltages(buffer);
            else
                voltageValues = Utils.CastArray<byte, float>(buffer);

            //convert data into an EDataPackage
            //FIXME: change 0 to triggerIndex
            lastDataPackage = new DataPackageWaveAnalog(voltageValues, 0);
        }
    }
}
