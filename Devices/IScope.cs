using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.DataSources;


namespace LabNation.DeviceInterface.Devices
{
    public delegate void AcquisitionTransferFinishedHandler(IScope scope, EventArgs e);

    public sealed class ProbeDivision 
    {
        public static List<ProbeDivision> divs = new List<ProbeDivision>();
        public float factor { get; private set; }
        private string name;

        private ProbeDivision(string name, float factor)
        {
            this.factor = factor;
            this.name = name;
            divs.Add(this);
        }
        public override string ToString() { return this.name; }

        public static implicit operator float(ProbeDivision d) { return d.factor; }
        public static ProbeDivision X1 = new ProbeDivision("X1", 1f);
        public static ProbeDivision X10 = new ProbeDivision("X10", 10f);
        public static ProbeDivision X100 = new ProbeDivision("X100", 100f);
        public static ProbeDivision findByFactor(float factor)
        {
            ProbeDivision d = divs.SingleOrDefault(x => x.factor == factor);
            return d == null ? X1 : d;
        }
    }

    public enum TriggerSource { Channel = 0, External = 1 };
    public enum TriggerEdge { RISING = 0, FALLING = 1, ANY = 2 };
    public enum TriggerMode { Edge = 0, Timeout = 1, Pulse = 2, Digital = 3 };
    public enum Coupling { AC, DC };
    public enum AcquisitionMode { SINGLE = 2, NORMAL = 1, AUTO = 0};
    public enum DigitalTriggerValue { L, H, R, F, X };
    /// <summary>
    /// Describes an entire trigger condition
    /// </summary>
    public class TriggerValue
    {
        /// <summary>
        /// Trigger mode (pulse,edge,timeout,digi)
        /// </summary>
        public TriggerMode mode = TriggerMode.Edge;
        /// <summary>
        /// Trigger source (analog/ext)
        /// </summary>
        public TriggerSource source = TriggerSource.Channel;
        /// <summary>
        /// Analog mode channel
        /// </summary>
        public AnalogChannel channel = AnalogChannel.ChA;
        /// <summary>
        /// Digital mode setting
        /// </summary>
        private Dictionary<DigitalChannel, DigitalTriggerValue> digital = new Dictionary<DigitalChannel,DigitalTriggerValue>();
        public Dictionary<DigitalChannel, DigitalTriggerValue> Digital
        {
            get { return digital; }
            set
            {
                if (value == null)
                    throw new Exception("Can't set trigger's digital value with null");
                digital = value;
            }
        }
        /// <summary>
        /// The direction for analog/ext trigger
        /// </summary>
        public TriggerEdge edge = TriggerEdge.RISING;
        /// <summary>
        /// Trigger level in volt for analog mode
        /// </summary>
        public float level;
        public double pulseWidthMin = 0.0;
        public double pulseWidthMax = double.PositiveInfinity;

        public TriggerValue() 
        {
            foreach (DigitalChannel ch in DigitalChannel.List)
                Digital[ch] = DigitalTriggerValue.X;
        }
        public TriggerValue(TriggerValue t) : this ()
        {
            mode = t.mode;
            source = t.source;
            channel = t.channel;
            Digital = new Dictionary<DigitalChannel, DigitalTriggerValue>(t.Digital);
            edge = t.edge;
            level = t.level;
            pulseWidthMax = t.pulseWidthMax;
            pulseWidthMin = t.pulseWidthMin;
        }

        public TriggerValue Copy()
        {
            return (TriggerValue)this.MemberwiseClone();
        }
    }

    public interface IScope : IDevice
    {
        DataPackageScope GetScopeData();
        DataSources.DataSource DataSourceScope { get; }
        
        bool Rolling { get; set; }
        bool Running { get; set; }
        bool CanRoll { get; }
        bool StopPending { get; }
        bool AwaitingTrigger { get; }
        bool Armed { get; }
        void Pause();
        void Resume();

        /* Acquisition & Trigger */
        uint AcquisitionDepthUserMaximum { get; set; }
        bool PreferPartial { get; set; }
		AcquisitionMode AcquisitionMode { get; set; }
        double AcquisitionLength { get; set; }
        double SamplePeriod { get; }
        double AcquisitionLengthMax { get; }
        double AcquisitionLengthMin { get; }
        uint AcquisitionDepthMax { get; }
        uint InputDecimationMax { get; }
        int SubSampleRate { get; }
        uint AcquisitionDepth { get; set; }
        double TriggerHoldOff { get; set; }
        TriggerValue TriggerValue { get; set; }
        bool SendOverviewBuffer { get; set; }
        void ForceTrigger();
        event AcquisitionTransferFinishedHandler OnAcquisitionTransferFinished;
        
        /* Channel specifics */
        void SetCoupling(AnalogChannel channel, Coupling coupling);
        Coupling GetCoupling(AnalogChannel channel);
        void SetVerticalRange(AnalogChannel channel, float minimum, float maximum);
        void SetYOffset(AnalogChannel channel, float offset);
        float GetYOffset(AnalogChannel channel);
        float GetYOffsetMax(AnalogChannel ch);
        float GetYOffsetMin(AnalogChannel ch);
        void SetProbeDivision(AnalogChannel ch, ProbeDivision division);
        ProbeDivision GetProbeDivision(AnalogChannel ch);

        /* Logic Analyser */
        bool LogicAnalyserEnabled { get; }
        AnalogChannel ChannelSacrificedForLogicAnalyser { set; }

        /* Viewport */        
        void SetViewPort(double offset, double timespan);
        double ViewPortTimeSpan { get; }
        double ViewPortOffset { get; }
        bool SuspendViewportUpdates { get; set; }
        
        void CommitSettings();
    }

    public class ValidationException: Exception 
    {
        public ValidationException(string message) : base(message) { }
    }
}
