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
        private int usbLatency = 2;
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
        		
        private uint waveLength { get { return 2 * acquisitionDepth; } }
        internal double BASE_SAMPLE_PERIOD = 10e-9; //10MHz sample rate
        private uint decimation = 0;
        private double SamplePeriod { get { return BASE_SAMPLE_PERIOD * Math.Pow(2, decimation); } }
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
        private int viewportOffset = 0; //Number of samples to skip in acq buffer
        private int viewportDecimation = 0;

        private const int OVERVIEW_LENGTH = 2048;
        private const int VIEWPORT_SAMPLES_MIN = 128;
        private const int VIEWPORT_SAMPLES_MAX = 2048;
        private const int VIEW_DECIMATION_MAX = 10;
        private int viewportSamples = VIEWPORT_SAMPLES_MAX;

        //Hack
        private bool logicAnalyser;
		bool regenerate = true;
		DataPackageScope p;
        private int maximumGenerationLength = 1024*1024*100; //Don't generate more than this many samples of wave

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
            set { this.acquisitionRunning = value; }
            get { return this.acquisitionRunning; } 
        }
        public bool StopPending { get { return false; } }

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
                    else if (value < 0)
                        this.triggerHoldoff = 0;
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
        public AnalogTriggerValue TriggerAnalog
        {
            set
            {
                this.triggerAnalog = value;
            }
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
            forceTrigger = true;
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
            double maxTimeSpan = AcquisitionTimeSpan - offset;
            if (timespan > maxTimeSpan)
            {
                if (timespan > AcquisitionTimeSpan)
                {
                    timespan = AcquisitionTimeSpan;
                    offset = 0;
                }
                else
                {
                    //Limit offset so the timespan can fit
                    offset = AcquisitionTimeSpan - timespan;
                }
            }

            //Decrease the number of samples till viewport sample period is larger than 
            //or equal to the full sample rate
            uint samples = VIEWPORT_SAMPLES_MAX;
                
            int viewDecimation = 0;
            while(true)
            {
                viewDecimation = (int)Math.Ceiling(Math.Log(timespan / samples / SamplePeriod, 2));
                if (viewDecimation >= 0)
                    break;
                samples /= 2;
            }
                
            if (samples < VIEWPORT_SAMPLES_MIN)
            {
                Logger.Warn("Unfeasible zoom level");
                return;
            }

            if (viewDecimation > VIEW_DECIMATION_MAX)
            {
                Logger.Warn("Clipping view decimation! better decrease the sample rate!");
                viewDecimation = VIEW_DECIMATION_MAX;
            }
            viewportSamples = (int)(timespan / (SamplePeriod * Math.Pow(2, viewDecimation)));
            viewportDecimation = viewDecimation;
            viewportOffset = TimeToSamples(offset, decimation);
		}
        public double ViewPortTimeSpan
        {
            get { return viewportSamples * SamplePeriod * Math.Pow(2, viewportDecimation); }
        }
        public double ViewPortOffset
        {
            get { return SamplesToTime((uint)viewportOffset); }
        }

        public uint AcquisitionDepth
        {
            set {
                lock (resetAcquisitionLock)
                {
                    double log2OfRatio = Math.Log((double)value / OVERVIEW_LENGTH, 2);
                    if(log2OfRatio != (int)log2OfRatio)
                        throw new ValidationException("Acquisition depth must be " + OVERVIEW_LENGTH + " * 2^N");
                    acquisitionDepth = value;
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

                    if (logicAnalyser)
                    {
                        triggerDetected = DummyScope.DoTriggerDigital(waveDigital.ToArray(), triggerHoldoffInSamples, digitalTrigger, acquisitionDepth, out triggerIndex);
                    }
                    else
                    {
                        triggerDetected = DummyScope.DoTriggerAnalog(waveAnalog[triggerAnalog.channel].ToArray(), triggerAnalog,
                            triggerHoldoffInSamples, triggerThreshold, triggerWidth,
                            acquisitionDepth, out triggerIndex);
                    }
                    
                    if(triggerDetected)
                        break;
                    if (
                        forceTrigger || 
                        (triggerTimeout > 0 && triggerTimeout < waveAnalog[AnalogChannel.ChA].Count * SamplePeriodCurrent)
                        )
                    {
                        forceTrigger = false;
                        triggerIndex = triggerHoldoffInSamples;
                        break;
                    }
                    //Stop trying to find a trigger at some point to avoid running out of memory
                    if (waveAnalog[AnalogChannel.ChA].Count  > maximumGenerationLength)
                    {
                        System.Threading.Thread.Sleep(10);
                        return null;
                    }

                    var timePassed = new TimeSpan((long)(waveLengthLocal * SamplePeriodCurrent * 1e7));
                    timeOffset = timeOffset.Add(timePassed);
                }
                    
                foreach(AnalogChannel channel in AnalogChannel.List)
                {
                    acquisitionBufferAnalog[channel] = DummyScope.CropWave(acquisitionDepth, waveAnalog[channel].ToArray(), triggerIndex, triggerHoldoffInSamples);
                }
                acquisitionBufferDigital = DummyScope.CropWave(acquisitionDepth, waveDigital.ToArray(), triggerIndex, triggerHoldoffInSamples);
            }                   
            p = new DataPackageScope(
                    acquisitionDepth, SamplePeriodCurrent, 
                    SamplePeriodCurrent * Math.Pow(2, viewportDecimation), viewportSamples, viewportOffset * SamplePeriodCurrent, 
                    TriggerHoldoffCurrent, false, false);

            foreach (AnalogChannel ch in AnalogChannel.List)
            {
                p.SetAcquisitionBufferOverviewData(ch, GetViewport(acquisitionBufferAnalog[ch], 0, (int)(Math.Log(acquisitionDepth/OVERVIEW_LENGTH, 2)), OVERVIEW_LENGTH));
                p.SetViewportData(ch, GetViewport(acquisitionBufferAnalog[ch], viewportOffset, viewportDecimation, viewportSamples));
            }

            p.SetViewportDataDigital(GetViewport(acquisitionBufferDigital, viewportOffset, viewportDecimation, viewportSamples));
#if __IOS__
			regenerate = true;
#endif

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
			for (int i = Math.Max (0, holdoff); i < wave.Length - width - outputWaveLength; i++) {
				float invertor = (trigger.direction == TriggerDirection.RISING) ? 1f : -1f;
                int triggerIndexTmp = (int)(i + width / 2);
                if (
                    (invertor * wave[i] < invertor * trigger.level && invertor * wave[i + width] >= invertor * trigger.level + threshold)
                    &&
                    triggerIndexTmp - holdoff + outputWaveLength <= wave.Length
                    )
                {
                    triggerIndex = triggerIndexTmp;
					return true;
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
