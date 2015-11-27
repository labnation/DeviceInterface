using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LabNation.DeviceInterface.DataSources
{
    internal interface IChannelBuffer
    {
        Type GetDataType();
        String GetName();
        void AddData(Array data);
        Array GetDataOfNextAcquisition();
        long BytesStored();
        void Destroy();
    }
}
