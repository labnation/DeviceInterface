using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
#if INTERNAL
using ECore.DeviceMemories;
#endif
using ECore.DataSources;


namespace ECore.Devices
{
    public enum TriggerDirection { RISING = 0, FALLING = 1 };
    public enum Coupling { AC, DC };
    public enum AcquisitionMode { SINGLE = 2, NORMAL = 1, AUTO = 0};
    public enum DigitalTriggerValue { L, H, R, F, X };

    public delegate void ScopeConnectHandler(IScope scope, bool connected);

    public interface IScope
    {
#if INTERNAL
        List<DeviceMemory> GetMemories();
#endif
        DataPackageScope GetScopeData();

        bool Ready { get; }
        string Serial { get; }
        double GetDefaultTimeRange();
        void SetAcquisitionMode(AcquisitionMode mode);
        void SetAcquisitionRunning(bool running);
        bool GetAcquisitionRunning();
        void SetTriggerHoldOff(double time);
        void SetTriggerAnalog(float voltage);
        void SetTriggerDigital(Dictionary<DigitalChannel, DigitalTriggerValue> condition);
        void SetVerticalRange(AnalogChannel channel, float minimum, float maximum);
        void SetYOffset(AnalogChannel channel, float offset);
        void SetTriggerChannel(AnalogChannel channel);
        void SetTriggerDirection(TriggerDirection direction);
        void SetForceTrigger();
        void SetCoupling(AnalogChannel channel, Coupling coupling);
        void SetTriggerWidth(uint width);
        uint GetTriggerWidth();
        void SetTriggerThreshold(uint threshold);
        uint GetTriggerThreshold();
        
        void SetEnableLogicAnalyser(bool enable);
        void SetLogicAnalyserChannel(AnalogChannel channel);

        void setAwgData(double[] data);
        void setAwgStretching(int decimation);
        void setAwgEnabled(bool enable);

        Coupling GetCoupling(AnalogChannel channel);
        void SetTimeRange(double timeRange);
        DataSources.DataSource DataSourceScope { get; }

        void CommitSettings();
    }

    public class ValidationException: Exception 
    {
        public ValidationException(string message) : base(message) { }
    }
}
