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
        int AddData(Array data);
        Array GetDataOfNextAcquisition();
        void Rewind();
        long BytesStored();
        void Destroy();
    }
}
