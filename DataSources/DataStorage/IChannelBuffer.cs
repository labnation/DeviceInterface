using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LabNation.DeviceInterface.DataSources
{
    public interface IChannelBuffer
    {
        int SamplesStored { get; }
        Type GetDataType();
        String GetName();
        int AddData(Array data, int chunkSize);
        Array GetDataOfNextAcquisition();
        void Rewind();
        long BytesStored();
        void Destroy();
    }
}
