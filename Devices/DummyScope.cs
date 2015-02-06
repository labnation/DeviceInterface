using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.DataSources;
using Common;
using ECore.DeviceMemories;

namespace ECore.Devices {
	public enum WaveSource {
		FILE,
		GENERATOR
	}

    public class DummyScopeChannelConfig
    {
        public WaveForm waveform;
        public double amplitude;
        public Coupling coupling;
        public double dcOffset;
        public double frequency;
        public double phase;
        public double noise;
        public int bursts;
        public ProbeDivision probeDivision;
    }

	public partial class DummyScope : IDevice, IScope {
#if DEBUG
        public List<DeviceMemory> GetMemories() { return null; }
#endif

        public DataSources.DataSource DataSourceScope { get; private set; }
		private DateTime timeOrigin;
		//Wave settings
        private int usbLatency = 10;
        private uint acquisitionDepth = 2048;
        private object resetAcquisitionLock = new object();
        private bool resetAcquisition = false;
        private bool forceTrigger = false;

        Dictionary<AnalogChannel, float[]> acquisitionBufferAnalog = new Dictionary<AnalogChannel, float[]>();
        byte[] acquisitionBufferDigital = null;

        
        //milliseconds of latency to simulate USB request delay
        private Dictionary<AnalogChannel, float> yOffset = new Dictionary<AnalogChannel, float>() {
            { AnalogChannel.ChA, 0f},
            { AnalogChannel.ChB, 0f}
        };
		
        //Acquisition variables
        private AcquisitionMode acquisitionMode = AcquisitionMode.NORMAL;
        private bool acquisitionRunning = false;

        private double SamplePeriodCurrent = 0;
        private uint waveLengthLocal = 0;
        private double TriggerHoldoffCurrent;
        private uint acquisitionDepthCurrent;
        		
        private uint waveLength { get { return 2 * acquisitionDepth; } }
        internal double BASE_SAMPLE_PERIOD = 10e-9; //10MHz sample rate
        internal uint DECIMATION_MAX = 10;
        private static uint ACQUISITION_DEPTH_MAX = 16 * 1024;
        private static int ACQUISITION_DEPTH_POWER_MAX = (int)Math.Ceiling(Math.Log(uint.MaxValue / OVERVIEW_LENGTH, 2));
        private uint _decimation = 0;
        private uint decimation
        {
            get { return _decimation; }
            set
            {
                if (value > DECIMATION_MAX)
                    _decimation = DECIMATION_MAX;
                else
                    _decimation = value;
            }
        }
        public double SamplePeriod { get { return BASE_SAMPLE_PERIOD * Math.Pow(2, decimation); } }
        public double AcquisitionTimeSpan { get { return SamplesToTime(AcquisitionDepth); } } 
        public double SamplesToTime(uint samples)
        {
            return samples * SamplePeriod;
        }

        private Int32 TimeToSamples(double time, uint inputDecimation)
        {
            return (Int32)(time / (BASE_SAMPLE_PERIOD * Math.Pow(2, inputDecimation)));
        }

        public Dictionary<AnalogChannel, DummyScopeChannelConfig> ChannelConfig { get; private set; }

        //Trigger
        private double triggerHoldoff = 0;
        private uint triggerWidth = 10;
        private float triggerThreshold = 0;

        AnalogTriggerValue triggerAnalog = new AnalogTriggerValue
        {
            channel = AnalogChannel.ChA,
            level = 0f,
            direction = TriggerDirection.RISING
        };
		
        private struct DigitalTrigger {
            public byte triggerCondition;
            public byte triggerMask;
            public byte preTriggerCondition;
            public byte preTriggerMask;
        }
        private DigitalTrigger digitalTrigger;
        
        //Viewport
        private bool viewportUpdate = false;

        private const int OVERVIEW_LENGTH = 2048;
        private const int VIEWPORT_SAMPLES_MIN = 128;
        private const int VIEWPORT_SAMPLES_MAX = 2048;
        private const int VIEW_DECIMATION_MAX = 10;

        //Hack
        private bool logicAnalyser;
		DataPackageScope p;
        private static int GENERATION_LENGTH_MAX = (int)ACQUISITION_DEPTH_MAX * 3; //Don't generate more than this many samples of wave

		#region constructor / initializer

		internal DummyScope () : base ()
		{
            waveSource = WaveSource.GENERATOR;
            ChannelConfig = new Dictionary<AnalogChannel, DummyScopeChannelConfig>() 
            {
                { AnalogChannel.ChA, new DummyScopeChannelConfig()
                    {
                        amplitude = 2,
                        noise = 0.1,
                        coupling = Coupling.DC,
                        dcOffset = 0.0,
                        frequency = 10e3,
                        phase = 0,
                        waveform = WaveForm.TRIANGLE,
                        probeDivision = ProbeDivision.X1,
                    }
                },
                { AnalogChannel.ChB, new DummyScopeChannelConfig() 
                    {
                        amplitude = 1,
                        noise = 0,
                        coupling = Coupling.DC,
                        dcOffset = 0.0,
                        frequency = 10e3,
                        phase = 0,
                        waveform = WaveForm.SINE,
                        probeDivision = ProbeDivision.X1,
                    }
                }
            };
            
            timeOrigin = DateTime.Now;
			DataSourceScope = new DataSources.DataSource (this);
            AcquisitionDepth = 16 * 1024;
		}
        public void CommitSettings() { }

        public void Pause() 
        {
            this.DataSourceScope.Pause();
        }

        public void Resume() 
        {
            this.DataSourceScope.Resume();
        }

		#endregion

		#region real scope settings

        public AcquisitionMode AcquisitionMode
        {
            set
            {
                lock (resetAcquisitionLock)
                {
                    this.acquisitionMode = value;
                    resetAcquisition = true;
                }
            }
        }

        public bool Running {
            set
            {
                if (value)
                {
                    StopPending = false;
                    this.acquisitionRunning = value;
                }
                else
                    StopPending = true;
            }

            get { return this.acquisitionRunning; } 
        }
        public bool StopPending { get; private set; }
        private bool awaitingTrigger = false;
        public bool AwaitingTrigger { get { return acquisitionRunning && awaitingTrigger; } }
        public bool Armed { get { return acquisitionRunning; } }

        public bool CanRoll { get { return false; } }
        public bool Rolling { set { } get { return false; } }

		public double TriggerHoldOff
		{
            set
            {
                lock (resetAcquisitionLock)
                {
                    if (value > AcquisitionTimeSpan)
                        this.triggerHoldoff = AcquisitionTimeSpan;
                    //else if (value < 0)
                        //this.triggerHoldoff = 0;
                    else
                        this.triggerHoldoff = value;
                    resetAcquisition = true;
                }
            }
            get
            {
                return this.triggerHoldoff;
            }
		}

        public bool SendOverviewBuffer { get; set; }

        public AnalogTriggerValue TriggerAnalog
        {
            get { return this.triggerAnalog.Copy(); }
            set { this.triggerAnalog = value; }
        }
        public void SetVerticalRange(AnalogChannel ch, float minimum, float maximum)
        {
        }
        public void SetProbeDivision(AnalogChannel ch, ProbeDivision division)
        {
            ChannelConfig[ch].probeDivision = division;
        }
        public ProbeDivision GetProbeDivision(AnalogChannel ch)
        {
            return ChannelConfig[ch].probeDivision;
        }
        public void SetYOffset(AnalogChannel ch, float yOffset)
		{
			this.yOffset [ch] = yOffset;
		}
		public float GetYOffset(AnalogChannel ch)
		{
		   return this.yOffset[ch];
		}
        public float GetYOffsetMax(AnalogChannel ch) { return float.MaxValue; }
        public float GetYOffsetMin(AnalogChannel ch) { return float.MinValue; }

        public void ForceTrigger()
        {
            if (!forceTrigger)
            {
                awaitingTrigger = false;
                forceTrigger = true;
            }
        }
        public uint TriggerWidth
        {
            set
            {
                triggerWidth = value;
            }
            get
            {
                return triggerWidth;
            }
        }
        public float TriggerThreshold
        {
            set
            {
                triggerThreshold = value;
            }
            get
            {
                return triggerThreshold;
            }
        }
        public Dictionary<DigitalChannel, DigitalTriggerValue> TriggerDigital
        {
            set
            {
                digitalTrigger.triggerCondition = 0x0;
                digitalTrigger.triggerMask = 0xFF;
                digitalTrigger.preTriggerCondition = 0x0;
                digitalTrigger.preTriggerMask = 0xFF;
                foreach (KeyValuePair<DigitalChannel, DigitalTriggerValue> kvp in value)
                {
                    int bit = kvp.Key.Value;
                    switch (kvp.Value)
                    {
                        case DigitalTriggerValue.X:
                            Utils.ClearBit(ref digitalTrigger.triggerMask, bit);
                            Utils.ClearBit(ref digitalTrigger.preTriggerMask, bit);
                            break;
                        case DigitalTriggerValue.H:
                            Utils.ClearBit(ref digitalTrigger.preTriggerMask, bit);
                            Utils.SetBit(ref digitalTrigger.triggerCondition, bit);
                            break;
                        case DigitalTriggerValue.L:
                            Utils.ClearBit(ref digitalTrigger.preTriggerMask, bit);
                            Utils.ClearBit(ref digitalTrigger.triggerCondition, bit);
                            break;
                        case DigitalTriggerValue.R:
                            Utils.ClearBit(ref digitalTrigger.preTriggerCondition, bit);
                            Utils.SetBit(ref digitalTrigger.triggerCondition, bit);
                            break;
                        case DigitalTriggerValue.F:
                            Utils.SetBit(ref digitalTrigger.preTriggerCondition, bit);
                            Utils.ClearBit(ref digitalTrigger.triggerCondition, bit);
                            break;
                    }
                }
            }
        }
		public void SetViewPort(double offset, double timespan)
        {
            /*                maxTimeSpan
             *            <---------------->
             *  .--------------------------,
             *  |        ||       ||       |
             *  `--------------------------`
             *  <--------><------->
             *    offset   timespan
             */
            if (offset < 0)
                offset = 0;
            if (offset >= AcquisitionTimeSpan)
                offset = 0;

            double maxTimeSpan = AcquisitionTimeSpan - offset;
            
            if (timespan > maxTimeSpan || timespan < SamplePeriod)
                return;

            ViewPortOffset = offset;
            ViewPortTimeSpan = timespan;
            viewportUpdate = true;
		}
        public double ViewPortTimeSpan
        {
            get;
            private set;
        }
        public double ViewPortOffset
        {
            get;
            private set;
        }

        public double AcquisitionLengthMax
        {
            get { return ACQUISITION_DEPTH_MAX * BASE_SAMPLE_PERIOD * DECIMATION_MAX; }
        }

        public bool PreferPartial { get; set; }

        public double AcquisitionLength
        {
            get
            {
                return AcquisitionDepth * SamplePeriod;
            }
            set
            {
                double samples = value / BASE_SAMPLE_PERIOD;
                double ratio = (double)samples / OVERVIEW_LENGTH;
                int log2OfRatio = (int)Math.Ceiling(Math.Log(ratio, 2));
                if (log2OfRatio < 0)
                    log2OfRatio = 0;
                if (log2OfRatio > ACQUISITION_DEPTH_POWER_MAX)
                    log2OfRatio = ACQUISITION_DEPTH_POWER_MAX;
                AcquisitionDepth = (uint)(OVERVIEW_LENGTH * Math.Pow(2, log2OfRatio));

                ratio = samples / AcquisitionDepth;
                log2OfRatio = (int)Math.Ceiling(Math.Log(ratio, 2));
                if (log2OfRatio < 0)
                    log2OfRatio = 0;
                decimation = (uint)log2OfRatio;
            }
        }

        public uint AcquisitionDepth
        {
            set {
                lock (resetAcquisitionLock)
                {
                    if (value == 0) //Overflowing - take max
                    {
                        acquisitionDepth = ACQUISITION_DEPTH_MAX;
                    }
                    else
                    {
                        double log2OfRatio = Math.Log((double)value / OVERVIEW_LENGTH, 2);
                        if (log2OfRatio != (int)log2OfRatio)
                            throw new ValidationException("Acquisition depth must be " + OVERVIEW_LENGTH + " * 2^N");
                        if (value > ACQUISITION_DEPTH_MAX)
                            acquisitionDepth = ACQUISITION_DEPTH_MAX;
                        else
                            acquisitionDepth = value;
                    }
                    resetAcquisition = true;
                }
            }
            get { return acquisitionDepth; }
        }


		public void SetCoupling (AnalogChannel ch, Coupling coupling)
		{
            ChannelConfig[ch].coupling = coupling;
		}
		public Coupling GetCoupling (AnalogChannel ch)
		{
            return ChannelConfig[ch].coupling;
		}
        public DataPackageScope GetScopeData()
        {
            //Sleep to simulate USB delay
            System.Threading.Thread.Sleep(usbLatency);
            TimeSpan timeOffset = DateTime.Now - timeOrigin;
            if (acquisitionRunning)
            {
                viewportUpdate = true;
                int triggerHoldoffInSamples = 0;
                int triggerIndex = 0;
                Dictionary<AnalogChannel, List<float>> waveAnalog = new Dictionary<AnalogChannel, List<float>>();
                foreach(AnalogChannel ch in AnalogChannel.List)
                    waveAnalog.Add(ch, new List<float>());
                List<byte> waveDigital = new List<byte>();

                bool triggerDetected = false;

                while(true) {
                    AcquisitionMode AcquisitionModeLocal;
                    lock (resetAcquisitionLock)
                    {
                        AcquisitionModeLocal = acquisitionMode;
                        acquisitionDepthCurrent = AcquisitionDepth;
                        TriggerHoldoffCurrent = triggerHoldoff;
                        SamplePeriodCurrent = SamplePeriod;
                        waveLengthLocal = waveLength;
                    }

                    foreach (AnalogChannel channel in AnalogChannel.List)
                    {
                        float[] wave;
                        switch(waveSource) {
                            case WaveSource.GENERATOR:
                                wave = DummyScope.GenerateWave(waveLengthLocal,
                                    SamplePeriodCurrent,
                                    timeOffset.Ticks / 1e7,
                                    ChannelConfig[channel]);
                                break;
                            case WaveSource.FILE:
                                wave = GetWaveFromFile(channel, waveLengthLocal, SamplePeriodCurrent, timeOffset.Ticks / 1e7);
                                break;
                            default:
                                throw new Exception("Unsupported wavesource");

                        }
                        if (ChannelConfig[channel].coupling == Coupling.AC)
                            DummyScope.RemoveDcComponent(ref wave, ChannelConfig[channel].frequency, SamplePeriodCurrent);
                        else
                            DummyScope.AddDcComponent(ref wave, (float)ChannelConfig[channel].dcOffset);
                        DummyScope.AddNoise(wave, ChannelConfig[channel].noise);
                        waveAnalog[channel].AddRange(wave);
                    }
                    waveDigital.AddRange(DummyScope.GenerateWaveDigital(waveLengthLocal, SamplePeriodCurrent, timeOffset.TotalSeconds));

                    triggerHoldoffInSamples = (int)(TriggerHoldoffCurrent / SamplePeriodCurrent);
                    double triggerTimeout = 0.0;
                    if (AcquisitionModeLocal == AcquisitionMode.AUTO)
                        triggerTimeout = 0.01; //Give up after 10ms

                    if (
                        forceTrigger ||
                        (triggerTimeout > 0 && triggerTimeout < waveAnalog[AnalogChannel.ChA].Count * SamplePeriodCurrent)
                    )
                    {
                        forceTrigger = false;
                        triggerIndex = triggerHoldoffInSamples;
                        awaitingTrigger = false;
                        break;
                    }

                    if (logicAnalyser)
                    {
                        triggerDetected = DummyScope.DoTriggerDigital(waveDigital.ToArray(), triggerHoldoffInSamples, digitalTrigger, acquisitionDepthCurrent, out triggerIndex);
                    }
                    else
                    {
                        triggerDetected = DummyScope.DoTriggerAnalog(waveAnalog[triggerAnalog.channel].ToArray(), triggerAnalog,
                            triggerHoldoffInSamples, triggerThreshold, triggerWidth,
                            acquisitionDepthCurrent, out triggerIndex);
                    }
                    awaitingTrigger = !triggerDetected;

                    if (triggerDetected)
                        break;
                    //Stop trying to find a trigger at some point to avoid running out of memory
                    if (waveAnalog[AnalogChannel.ChA].Count  > GENERATION_LENGTH_MAX)
                    {
                        System.Threading.Thread.Sleep(10);
                        return null;
                    }

                    var timePassed = new TimeSpan((long)(waveLengthLocal * SamplePeriodCurrent * 1e7));
                    timeOffset = timeOffset.Add(timePassed);
                }
                    
                foreach(AnalogChannel channel in AnalogChannel.List)
                {
                    acquisitionBufferAnalog[channel] = DummyScope.CropWave(acquisitionDepthCurrent, waveAnalog[channel].ToArray(), triggerIndex, triggerHoldoffInSamples);
                }
                acquisitionBufferDigital = DummyScope.CropWave(acquisitionDepthCurrent, waveDigital.ToArray(), triggerIndex, triggerHoldoffInSamples);
                if (StopPending)
                {
                    acquisitionRunning = false;
                }
            }
            if (!viewportUpdate)
                return null;
            viewportUpdate = false;

            if (acquisitionBufferAnalog[AnalogChannel.ChA] == null)
                return null;

            //Decrease the number of samples till viewport sample period is larger than 
            //or equal to the full sample rate
            uint samples = VIEWPORT_SAMPLES_MAX;
            int viewportDecimation = 0;
            while (true)
            {
                viewportDecimation = (int)Math.Ceiling(Math.Log(ViewPortTimeSpan / (samples + 2) / SamplePeriodCurrent, 2));
                if (viewportDecimation >= 0)
                    break;
                samples /= 2;
            }

            if (viewportDecimation > VIEW_DECIMATION_MAX)
            {
                Logger.Warn("Clipping view decimation! better decrease the sample rate!");
                viewportDecimation = VIEW_DECIMATION_MAX;
            }
            int viewportSamples = (int)(ViewPortTimeSpan / (SamplePeriodCurrent * Math.Pow(2, viewportDecimation))) + 2;
            int viewportOffsetLocal = (int)(ViewPortOffset / SamplePeriodCurrent);

            
            p = new DataPackageScope(
                    acquisitionDepthCurrent, SamplePeriodCurrent, 
                    SamplePeriodCurrent * Math.Pow(2, viewportDecimation), viewportSamples, ViewPortOffset, 
                    TriggerHoldoffCurrent, false, false, 0);

            foreach (AnalogChannel ch in AnalogChannel.List)
            {
                p.SetAcquisitionBufferOverviewData(ch, GetViewport(acquisitionBufferAnalog[ch], 0, (int)(Math.Log(acquisitionDepthCurrent / OVERVIEW_LENGTH, 2)), OVERVIEW_LENGTH));
                p.SetViewportData(ch, GetViewport(acquisitionBufferAnalog[ch], viewportOffsetLocal, viewportDecimation, viewportSamples));
            }

            if(acquisitionBufferDigital != null)
                p.SetViewportDataDigital(GetViewport(acquisitionBufferDigital, viewportOffsetLocal, viewportDecimation, viewportSamples));

            if (acquisitionMode == AcquisitionMode.SINGLE)
                acquisitionRunning = false;

            return p;
        }

        public static T[] GetViewport<T>(T[] buffer, int offset, int decimation, int length)
        {
            int skip = 1 << decimation;
            return buffer.Skip(offset).Take(length * skip).Where((x, i) => i % skip == 0).ToArray();
        }

		#endregion

		#region dummy scope settings

        public WaveSource waveSource { get ; set; }

        public void SetDummyWaveAmplitude (AnalogChannel channel, double amplitude)
		{
            ChannelConfig[channel].amplitude = amplitude;
		}
        public void SetDummyWaveFrequency(AnalogChannel channel, double frequency)
		{
            ChannelConfig[channel].frequency = frequency;
		}
        public void SetDummyWavePhase(AnalogChannel channel, double phase)
        {
            ChannelConfig[channel].phase = phase;
        }
        public void SetDummyWaveForm(AnalogChannel channel, WaveForm w)
		{
            ChannelConfig[channel].waveform = w;
		}
        public void SetDummyWaveDcOffset(AnalogChannel channel, double dcOffset)
        {
            ChannelConfig[channel].dcOffset = dcOffset;
        }
        public void SetDummyWaveDcOffset(AnalogChannel channel, int bursts)
        {
            ChannelConfig[channel].bursts = bursts;
        }
        public void SetNoiseAmplitude(AnalogChannel channel, double noiseAmplitude)
		{
            ChannelConfig[channel].noise = noiseAmplitude;
		}

        //FIXME: implement this
        public bool LogicAnalyserEnabled
        {
            set { logicAnalyser = value; }
            get { return logicAnalyser; }
        }
        public AnalogChannel LogicAnalyserChannel
        {
            set { }
        }

		#endregion

        #region Helpers
        private static bool DoTriggerAnalog (float [] wave, AnalogTriggerValue trigger, int holdoff, float threshold, uint width, uint outputWaveLength, out int triggerIndex)
		{
			//Hold off:
			// - if positive, start looking for trigger at that index, so we are sure to have that many samples before the trigger
			// - if negative, start looking at index 0
			triggerIndex = 0;
            float invertor = (trigger.direction == TriggerDirection.RISING) ? 1f : -1f;
            uint halfWidth = width / 2;
            uint preconditionCounter = 0;
            uint postconditionCounter = 0;
			for (int i = Math.Max (0, holdoff); i < wave.Length - width - outputWaveLength; i++) {
                bool preconditionMet = preconditionCounter == halfWidth;
                if (preconditionMet)
                {
                    if (invertor * wave[i] >= invertor * trigger.level + threshold)
                        postconditionCounter++;
                }
                else
                {
                    if (invertor * wave[i] < invertor * trigger.level)
                        preconditionCounter++;
                }
                if (preconditionMet && postconditionCounter == halfWidth)
                {
                    int triggerIndexTmp = (int)(i + width / 2);
                    if (triggerIndexTmp - holdoff + outputWaveLength <= wave.Length)
                    {
                        triggerIndex = triggerIndexTmp;
                        return true;
                    }
                }
			}
			return false;
		}
        private static bool DoTriggerDigital(byte[] wave, int holdoff, DigitalTrigger trigger, uint outputWaveLength, out int triggerIndex)
		{
			//Hold off:
			// - if positive, start looking for trigger at that index, so we are sure to have that many samples before the trigger
			// - if negative, start looking at index 0
			triggerIndex = 0;
			for (int i = Math.Max (1, holdoff); i < wave.Length - outputWaveLength; i++) {
				if (
                    (wave[i] & trigger.triggerMask) == trigger.triggerCondition 
                    &&
                    (wave[i - 1] & trigger.preTriggerMask) == trigger.preTriggerCondition 
                    ) {
                    triggerIndex = i;
					return true;
				}
			}
			return false;
		}
        private static void AddDcComponent(ref float[] p, float offset)
        {
            if (offset == 0f)
                return;
            p = p.AsParallel().Select(x => x + offset).ToArray();
        }
        private static void RemoveDcComponent(ref float[] p, double frequency, double samplePeriod)
        {
            int periodLength = (int)Math.Round(1.0 / (frequency * samplePeriod));
            if (periodLength == 0)
                return;
            float mean = p.Take(periodLength).Average();
            if (mean == 0f)
                return;
            p = p.AsParallel().Select(x => x - mean).ToArray();
        }
        #endregion
	}
}
