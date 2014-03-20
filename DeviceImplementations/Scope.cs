using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.DeviceImplementations
{
    public abstract class Scope: EDeviceImplementation
    {
        public Scope(EDevice device) : base(device) { }
    }
}
