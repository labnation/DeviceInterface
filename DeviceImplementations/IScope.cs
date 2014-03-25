using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.DataPackages;

namespace ECore.DeviceImplementations
{
    //FIXME: make me an interface
    public interface IScope
    {
        DataPackageScope GetScopeData();

        void SetTriggerHoldOff(int samples);
        void SetTriggerLevel(float voltage);
        void SetYOffset(uint channel, float offset);
        void SetTriggerChannel(uint channel);
    }
}
