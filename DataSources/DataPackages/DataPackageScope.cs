using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.DeviceImplementations;

namespace ECore.DataPackages
{
    public class DataPackageScope
    {
        ScopeData data;
        private uint triggerIndex;

        public DataPackageScope(ScopeData data, uint triggerIndex)
        {
            this.data = data;
            this.triggerIndex = triggerIndex;
        }

        public ScopeData Data { get { return this.data; } }
        public uint TriggerIndex { get { return this.triggerIndex; } }
    }
}
