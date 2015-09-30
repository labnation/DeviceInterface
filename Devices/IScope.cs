using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.DataSources;


namespace LabNation.DeviceInterface.Devices
{
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

    public enum TriggerModes { Analog, Digital, External };
    public enum TriggerDirection { RISING = 0, FALLING = 1 };
    public enum Coupling { AC, DC };
    public enum AcquisitionMode { SINGLE = 2, NORMAL = 1, AUTO = 0};
	/// <summary>
	/// Digital trigger value.
	/// 	L	Low
	/// 	H	High
	/// 	R	Rising edge
	/// 	F	Falling edge
	/// 	X	Don't care
	/// </summary>
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

        public AnalogTriggerValue Copy()
        {
            return (AnalogTriggerValue)this.MemberwiseClone();
        }
    }

    public interface IScope : IDevice
    {
		/// <summary>
		/// Method to synchronously fetch scope data
		/// </summary>
		/// <returns>The scope data.</returns>
        DataPackageScope GetScopeData();
		/// <summary>
		/// The DataSourceScope allows you to register a callback which
		/// is called whenever new data comes in.
		/// 
		/// Mind that the DataSourceScope must be started in order for the 
		/// fetch thread to run.
		/// </summary>
		/// <value>The data source scope.</value>
        DataSources.DataSource DataSourceScope { get; }

		/// <summary>
		/// Enabel or disable rolling mode using this property
		/// </summary>
        bool Rolling { get; set; }
		/// <summary>
		/// Start or stop the scope using this property
		/// </summary>
        bool Running { get; set; }
		/// <summary>
		/// True when the scope can go into rolling mode
		/// </summary>
        bool CanRoll { get; }
		/// <summary>
		/// True when the acquistion will be stopped after the current one
		/// </summary>
        bool StopPending { get; }
		/// <summary>
		/// True when the scope is awaiting a trigger condition to occur.
		/// </summary>
        bool AwaitingTrigger { get; }
		/// <summary>
		/// True when the scope is armed and waiting for a trigger
		/// </summary>
        bool Armed { get; }
        void Pause();
        void Resume();

        /* Acquisition & Trigger */

		/// <summary>
		/// When the sample rate is sufficiently low and data comes in slower than
		/// the transfer rate of the scope to host, the scope can optionally stream
		/// data yet available before the entire acqusition buffer is filled.
		/// 
		/// When false, the scope will wait for the acquisition to complete before
		/// streaming data to host. This ensures that only a single and viewport-complete
		/// data package will reach the host per acquisition. If viewport settings are changed
		/// while the current acquisition is not completed yet though, the scope can still
		/// send data of that ongoing acquisition.
		/// 
		/// In this mode, use the <see cref="LabNation.DeviceInterface.DataSources.DataPackageScope.Identifier"/> to
		/// distinguish between different acquisitions.
		/// </summary>
        bool PreferPartial { get; set; }
		/// <summary>
		/// Sets the acquisition mode which defines the trigger behavoir
		/// 	AUTO 	results in a timeout when no trigger is detected within 5ms
		/// 	NORMAL 	a trigger is required for the acquisition to complete
		/// 	SINGLE	once a trigger is detected, the running acquisition will finalise
		/// 			and the scope will stop after that.
		/// </summary>
		/// <value>The acquisition mode.</value>
		AcquisitionMode AcquisitionMode { get; set; }
		/// <summary>
		/// Gets or sets the length of the acquisition buffer (in seconds)
		/// </summary>
		/// <value>The length of the acquisition buffer (in seconds)</value>
        double AcquisitionLength { get; set; }
		/// <summary>
		/// Gets the sample period in seconds
		/// </summary>
		/// <value>The sample period in seconds</value>
        double SamplePeriod { get; }
		/// <summary>
		/// Gets the longest possible acquisition buffer (in seconds).
		/// </summary>
        double AcquisitionLengthMax { get; }
		/// <summary>
		/// Gets the shortest possible acquisition buffer (in seconds).
		/// </summary>
        double AcquisitionLengthMin { get; }

		/// <summary>
		/// Gets or sets the acquisition buffer depth (in samples)
		/// </summary>
        uint AcquisitionDepth { get; set; }
		/// <summary>
		/// Gets or sets the trigger hold off from the first sample of the acquisition buffer (in seconds)
		/// </summary>
		/// <value>The trigger hold off.</value>
        double TriggerHoldOff { get; set; }
		/// <summary>
		/// Gets the trigger mode (Analog, digital or external)
		/// </summary>
        TriggerModes TriggerMode { get; }
		/// <summary>
		/// Gets or sets the analog trigger value
		/// </summary>
		/// <value>The analog trigger value.</value>
        AnalogTriggerValue TriggerAnalog { get; set; }
		/// <summary>
		/// Sets the digital trigger
		/// </summary>
		/// <value>The digital trigger.</value>
        Dictionary<DigitalChannel, DigitalTriggerValue> TriggerDigital { set; }
		/// <summary>
		/// Gets or sets the width of the trigger (in samples)
		/// </summary>
		/// <value>The width of the trigger (in samples).</value>
        uint TriggerWidth { get; set; }
		/// <summary>
		/// Gets or sets the voltage threshold needed to cross before a trigger is considered valid
		/// </summary>
		/// <value>The trigger threshold (volts).</value>
        float TriggerThreshold { get; set; }
		/// <summary>
		/// Gets or sets a value indicating whether this <see cref="LabNation.DeviceInterface.Devices.IScope"/> send overview buffer with each acquisition
		/// </summary>
		/// <value><c>true</c> if send overview buffer; otherwise, <c>false</c>.</value>
        bool SendOverviewBuffer { get; set; }
		/// <summary>
		/// Calling this results in a trigger force
		/// </summary>
        void ForceTrigger();

        /* Channel specifics */
		/// <summary>
		/// Sets the coupling (AC or DC) for an analog input channel
		/// </summary>
		/// <param name="channel">Channel.</param>
		/// <param name="coupling">Coupling (AC/DC).</param>
        void SetCoupling(AnalogChannel channel, Coupling coupling);
        Coupling GetCoupling(AnalogChannel channel);
		/// <summary>
		/// Sets the voltage range of an analog input channel
		/// </summary>
		/// <param name="channel">Channel.</param>
		/// <param name="minimum">Minimum.</param>
		/// <param name="maximum">Maximum.</param>
        void SetVerticalRange(AnalogChannel channel, float minimum, float maximum);
		float[] GetVerticalRange(AnalogChannel channel);
		/// <summary>
		/// Sets the voltage offset of an analog input channel.
		/// 
		/// WARNING: this offset is dicated by the vertical range. Check
		/// GetYOffsetMax/Min() for the possible range
		/// </summary>
		/// <param name="channel">Channel.</param>
		/// <param name="offset">Offset.</param>
        void SetYOffset(AnalogChannel channel, float offset);
        float GetYOffset(AnalogChannel channel);
		/// <summary>
		/// Gets the maximal voltage offset for the current voltage range
		/// </summary>
		/// <returns>Maximum voltage offset</returns>
		/// <param name="ch">Ch.</param>
        float GetYOffsetMax(AnalogChannel ch);
		/// <summary>
		/// Gets the minimal voltage offset for the current voltage range
		/// </summary>
		/// <returns>Minimum voltage offset</returns>
		/// <param name="ch">Ch.</param>
        float GetYOffsetMin(AnalogChannel ch);
		/// <summary>
		/// Sets the probe division (x1, x10, x100)
		/// </summary>
		/// <param name="ch">Ch.</param>
		/// <param name="division">Division.</param>
        void SetProbeDivision(AnalogChannel ch, ProbeDivision division);
        ProbeDivision GetProbeDivision(AnalogChannel ch);

        /* Logic Analyser */
		/// <summary>
		/// Gets or sets a value indicating whether this <see cref="LabNation.DeviceInterface.Devices.IScope"/>'s logic analyser is enabled.
		/// </summary>
		/// <value><c>true</c> if logic analyser enabled; otherwise, <c>false</c>.</value>
        bool LogicAnalyserEnabled { get; set; }
		/// <summary>
		/// Which analog channel to discard to use for the logic analyser data
		/// </summary>
		/// <value>The analog channel sacrificed for logic analyser data.</value>
        AnalogChannel ChannelSacrificedForLogicAnalyser { set; }

        /* Viewport */
		/// <summary>
		/// Sets the view port.
		/// The viewport is the section of the acquisition buffer which is streamed to the host.
		/// It is subsampled so that it fits within 2048 samples.
		/// 
		/// When the scope is stopped, the acquisition buffer can be downloaded completely to the
		/// host, without subsampling, but this can take several seconds. Instead, the viewport 
		/// can be changed when only interested in a section of the acquisition buffer, potentially
		/// coarser than the effective sample rate.
		/// </summary>
		/// <param name="offset">Offset of the first sample of the acquisition (in seconds)</param>
		/// <param name="timespan">Timespan of the viewport</param>
        void SetViewPort(double offset, double timespan);
        double ViewPortTimeSpan { get; }
        double ViewPortOffset { get; }
        
		/// <summary>
		/// Commits the settings to the scope 
		/// </summary>
        void CommitSettings();
    }

    public class ValidationException: Exception 
    {
        public ValidationException(string message) : base(message) { }
    }
}
