using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.DataSources;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;

namespace LabNation.DeviceInterface.Devices
{
    public delegate void AcquisitionTransferFinishedHandler(IScope scope, EventArgs e);

    [Serializable()]
    [DataContract]
    public class Probe 
    {
        [DataMember] public float Gain { get; private set; }
        [DataMember] public float Offset { get; private set; }
        [DataMember] public string Name { get; private set; }
        [DataMember] public string Unit { get; private set; }
        [DataMember] public bool Inverted { get; set; }

        public Probe(string name, string unit, float gain, float offset, bool invert)
        {
            this.Name = name;
            this.Unit = unit;
            this.Gain = gain;
            this.Offset = offset;
            this.Inverted = invert;
        }

        public float RawToUser(float raw)
        {
            if (Inverted)
                return -(Offset + Gain * raw);
            else
                return Offset + Gain * raw;
        }

        public float UserToRaw(float userValue)
        {
            if (Inverted)
                return (- userValue - Offset) / Gain;
            else
                return (userValue - Offset) / Gain;
        }

        public void ChangeOffset(float offset)
        {
            this.Offset = offset;
        }

        private static Probe defaultX1Probe = new Probe("X1", "V", 1, 0, false);
        public static Probe DefaultX1Probe {get { return defaultX1Probe; } }
        private static Probe defaultX10Probe = new Probe("X10", "V", 10, 0, false);
        public static Probe DefaultX10Probe { get { return defaultX10Probe; } }
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
        float GetYOffsetLimit1(AnalogChannel ch);
        float GetYOffsetLimit2(AnalogChannel ch);
        void SetProbeDivision(AnalogChannel ch, Probe division);
        Probe GetProbe(AnalogChannel ch);

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
