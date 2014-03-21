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

        public abstract int GetTriggerHoldoff();
        public abstract void SetTriggerHoldOff(int samples);
        public abstract void SetTriggerLevel(float voltage);
        public abstract void SetYOffset(uint channel, float offset);
        public abstract void SetTriggerChannel(uint channel);
    }
}
