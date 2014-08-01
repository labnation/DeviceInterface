using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.DataSources;

namespace ECore.Devices
{
    public enum TriggerMode { ANALOG, DIGITAL };
    public enum TriggerDirection { RISING = 0, FALLING = 1 };
    public enum Coupling { AC, DC };
    public enum AcquisitionMode { SINGLE = 2, NORMAL = 1, AUTO = 0};
    public enum DigitalTriggerValue { O, I, R, F, X };

    public delegate void ScopeConnectHandler(IScope scope, bool connected);

    public interface IScope
    {
        DataPackageScope GetScopeData();

        bool Connected { get; }
        string Serial { get; }
        double GetDefaultTimeRange();
        void SetAcquisitionMode(AcquisitionMode mode);
        void SetAcuisitionRunning(bool running);
        bool GetAcquisitionRunning();
        void SetTriggerHoldOff(double time);
        void SetTriggerAnalog(float voltage);
        void SetTriggerDigital(Dictionary<DigitalChannel, DigitalTriggerValue> condition);
        void SetVerticalRange(int channel, float minimum, float maximum);
        void SetYOffset(int channel, float offset);
        void SetTriggerChannel(int channel);
        void SetTriggerDirection(TriggerDirection direction);
        void SetTriggerMode(TriggerMode mode);
        void SetForceTrigger();
        void SetCoupling(int channel, Coupling coupling);
        Coupling GetCoupling(int channel);
        void SetTimeRange(double timeRange);
        DataSources.DataSourceScope DataSourceScope { get; }

        void CommitSettings();
    }
}
