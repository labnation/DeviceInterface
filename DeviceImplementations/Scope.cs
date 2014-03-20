using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.DataPackages;

namespace ECore.DeviceImplementations
{
    public abstract class Scope: EDeviceImplementation
    {
        public Scope(EDevice device) : base(device) { }
        public abstract DataPackageScope GetScopeData();
    }
}
