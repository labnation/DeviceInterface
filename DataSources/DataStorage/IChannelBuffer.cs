using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.DataSources
{
    public interface IChannelBuffer
    {
        Type GetDataType();
        String GetName();
        void AddData(object data);
        object GetData(int offset = 0, long length = -1);
        long BytesStored();
        void Destroy();
    }
}
