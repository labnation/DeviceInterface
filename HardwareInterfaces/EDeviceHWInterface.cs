using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore
{
    abstract public class EDeviceHWInterface
    {
        abstract public int WriteControlMaxLength();
        abstract public int WriteControlBytes(byte[] message);
        abstract public byte[] ReadControlBytes(int length);
        abstract public byte[] GetData(int numberOfBytes);
        abstract public bool Connected { get; }
        abstract public void Stop();
        abstract public bool Start();
    }
}
