using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.DataPackages;

namespace ECore.Devices
{
    public enum TriggerDirection { RISING, FALLING };

    public interface IScope
    {
        DataPackageScope GetScopeData();
        bool Connected { get; }
        void SetTriggerHoldOff(double time);
        void SetTriggerLevel(float voltage);
        void SetYOffset(uint channel, float offset);
        void SetTriggerChannel(uint channel);
        void SetTriggerDirection(TriggerDirection direction);
        void SetDecimation(uint decimation);
    }
}
