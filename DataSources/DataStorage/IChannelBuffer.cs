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
        Array GetData(int offset = 0, long length = -1);
        long BytesStored();
        void Destroy();
    }
}
