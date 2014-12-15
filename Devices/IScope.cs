using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.DataSources;


namespace ECore.Devices
{
    public enum ProbeDivision { X1, X10 };
    public enum TriggerDirection { RISING = 0, FALLING = 1 };
    public enum Coupling { AC, DC };
    public enum AcquisitionMode { SINGLE = 2, NORMAL = 1, AUTO = 0};
    public enum DigitalTriggerValue { L, H, R, F, X };
    /// <summary>
    /// Describes an analog trigger
    /// </summary>
    public class AnalogTriggerValue
    {
        /// <summary>
        /// Trigger channel
        /// </summary>
        public AnalogChannel channel;
        /// <summary>
        /// The direction
        /// </summary>
        public TriggerDirection direction;
        /// <summary>
        /// Trigger level in volt
        /// </summary>
        public float level;
    }

    public interface IScope : IDevice
    {
        DataPackageScope GetScopeData();
        double GetDefaultTimeRange();

        void SetAcquisitionMode(AcquisitionMode mode);
        void SetAcquisitionRunning(bool running);
        bool CanRoll { get; }
        bool Rolling { get; }
        void SetRolling(bool enable);
        bool Running { get; }
        bool StopPending { get; }
        void Pause();
        void Resume();

        void SetTriggerHoldOff(double time);
        void SetTriggerAnalog(AnalogTriggerValue trigger);
        void SetTriggerDigital(Dictionary<DigitalChannel, DigitalTriggerValue> condition);
        void SetVerticalRange(AnalogChannel channel, float minimum, float maximum);
        void SetYOffset(AnalogChannel channel, float offset);
        float GetYOffset(AnalogChannel channel);
        void SetForceTrigger();
        void SetCoupling(AnalogChannel channel, Coupling coupling);
        void SetTriggerWidth(uint width);
        uint GetTriggerWidth();
        void SetTriggerThreshold(float threshold);
        float GetTriggerThreshold();
        void SetProbeDivision(AnalogChannel ch, ProbeDivision division);
        ProbeDivision GetProbeDivision(AnalogChannel ch);
        
        void SetEnableLogicAnalyser(bool enable);
        void SetLogicAnalyserChannel(AnalogChannel channel);

        Coupling GetCoupling(AnalogChannel channel);
        void SetTimeRange(double timeRange);
        double GetTimeRange();
        DataSources.DataSource DataSourceScope { get; }

        void CommitSettings();
    }

    public class ValidationException: Exception 
    {
        public ValidationException(string message) : base(message) { }
    }
}
