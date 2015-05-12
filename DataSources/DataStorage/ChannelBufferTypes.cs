using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace LabNation.DeviceInterface.DataSources
{

    internal class ChannelBufferFloat : ChannelBuffer<float>
    {
        public ChannelBufferFloat(string name) : base(name) { this.sizeOfType = sizeof(float); }
    }
    internal class ChannelBufferByte : ChannelBuffer<byte>
    {
        public ChannelBufferByte(string name) : base(name) { this.sizeOfType = sizeof(byte); }
    }
}
