using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.DataPackages;

namespace ECore.Devices
{
    public enum TriggerMode { ANALOG, DIGITAL, FREE_RUNNING };
    public enum TriggerDirection { RISING, FALLING };
    public enum Coupling { AC, DC };
    public enum AcquisitionMode { SINGLE, CONTINUOUS, SWEEP };
    public enum DigitalTriggerValue { O, I, R, F, X };

    public delegate void ScopeConnectHandler(IScope scope, bool connected);

    public interface IScope
    {
        DataPackageScope GetScopeData();

        bool Connected { get; }
        double GetDefaultTimeRange();
        double GetSamplePeriod();
        int GetNumberOfSamples();
        void SetAcquisitionMode(AcquisitionMode mode);
        void SetAcuisitionRunning(bool running);
        bool GetAcquisitionRunning();
        void SetTriggerHoldOff(double time);
        void SetTriggerAnalog(float voltage);
        void SetTriggerDigital(Dictionary<DigitalChannel, DigitalTriggerValue> condition);
        void SetYOffset(int channel, float offset);
        void SetTriggerChannel(int channel);
        void SetTriggerDirection(TriggerDirection direction);
        void SetTriggerMode(TriggerMode mode);
        void SetCoupling(int channel, Coupling coupling);
        Coupling GetCoupling(int channel);
        void SetTimeRange(double timeRange);
        void Configure();
        DataSources.DataSourceScope DataSourceScope { get; }
    }
}
