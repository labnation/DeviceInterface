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
        private const int outputWaveLength = 2000;
        private object resetAcquisitionLock = new object();
        private bool resetAcquisition = false;
        private bool forceTrigger = false;
        
        //milliseconds of latency to simulate USB request delay
        private Dictionary<AnalogChannel, float> yOffset = new Dictionary<AnalogChannel, float>() {
            { AnalogChannel.ChA, 0f},
            { AnalogChannel.ChB, 0f}
        };
		//Scope variables
		private const int waveLength = 3 * outputWaveLength;
		private double samplePeriodMinimum = 10e-9;
		//ns --> sampleFreq of 100MHz by default
        private double SamplePeriod { get { return samplePeriodMinimum * Math.Pow(2, decimation); } }

        public Dictionary<AnalogChannel, DummyScopeChannelConfig> ChannelConfig { get; private set; }
        AnalogTriggerValue triggerAnalog = new AnalogTriggerValue
        {
            channel = AnalogChannel.ChA,
            level = 0f,
            direction = TriggerDirection.RISING
        };
		
		private double triggerHoldoff = 0;
		private uint triggerWidth = 10;
        private float triggerThreshold = 0;

        private struct DigitalTrigger {
            public byte triggerCondition;
            public byte triggerMask;
            public byte preTriggerCondition;
            public byte preTriggerMask;
        }
        private DigitalTrigger digitalTrigger;
        private bool logicAnalyser;

		private uint decimation = 1;
        private AcquisitionMode acquisitionMode = AcquisitionMode.NORMAL;
        private bool acquisitionRunning = false;
		//Hack
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
                        frequency = 1e3,
                        phase = 0,
                        waveform = WaveForm.TRIANGLE,
                        probeDivision = ProbeDivision.X1,
                    }
                },
                { AnalogChannel.ChB, new DummyScopeChannelConfig() 
                    {
                        amplitude = 1,
                        noise = 0.1,
                        coupling = Coupling.DC,
                        dcOffset = 0.0,
                        frequency = 1e3,
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

		private void validateDecimation (uint decimation)
		{
			if (decimation < 1)
				throw new ValidationException ("Decimation must be larger than 0");
		}
        public void SetAcquisitionMode(AcquisitionMode mode)
        {
            lock (resetAcquisitionLock)
            {
                this.acquisitionMode = mode;
                resetAcquisition = true;
            }
        }
        public void SetAcquisitionRunning(bool running)
        {
            this.acquisitionRunning = running;
        }
        public bool Running { get { return this.acquisitionRunning; } }
        public bool StopPending { get { return false; } }

        public bool CanRoll { get { return false; } }
        public bool Rolling { get { return false; } }
        public void SetRolling(bool enable) { }

		public void SetTriggerHoldOff (double holdoff)
		{
            lock (resetAcquisitionLock)
            {
                this.triggerHoldoff = holdoff;
                resetAcquisition = true;
            }
		}
		public void SetTriggerAnalog (AnalogTriggerValue trigger)
		{
			this.triggerAnalog = trigger;
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
        public void SetForceTrigger()
        {
            forceTrigger = true;
        }
        public void SetTriggerWidth(uint width)
        {
            triggerWidth = width;
        }
        public uint GetTriggerWidth()
        {
            return triggerWidth;
        }
        public void SetTriggerThreshold(float threshold)
        {
            triggerThreshold = threshold;
        }
        public float GetTriggerThreshold()
        {
            return triggerThreshold;
        }
        public void SetTriggerDigital(Dictionary<DigitalChannel, DigitalTriggerValue> condition)
		{
            digitalTrigger.triggerCondition = 0x0;
            digitalTrigger.triggerMask = 0xFF;
            digitalTrigger.preTriggerCondition = 0x0;
            digitalTrigger.preTriggerMask = 0xFF;
            foreach (KeyValuePair<DigitalChannel, DigitalTriggerValue> kvp in condition)
            {
                int bit = kvp.Key.Value;
                switch(kvp.Value) {
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
		public void SetTimeRange (double timeRange)
		{
            lock (resetAcquisitionLock)
            {
                decimation = (uint)Math.Max(0, Math.Ceiling(Math.Log(timeRange / GetDefaultTimeRange(), 2)));
                resetAcquisition = true;
            }
		}
        public double GetTimeRange()
        {
            return GetDefaultTimeRange() * Math.Pow(2,decimation);
        }
		public void SetCoupling (AnalogChannel ch, Coupling coupling)
		{
            ChannelConfig[ch].coupling = coupling;
		}
		public Coupling GetCoupling (AnalogChannel ch)
		{
            return ChannelConfig[ch].coupling;
		}
		public double GetDefaultTimeRange ()
		{ 
			return outputWaveLength * samplePeriodMinimum; 
		}
        public DataPackageScope GetScopeData()
        {
            //Sleep to simulate USB delay
            System.Threading.Thread.Sleep(usbLatency);
            Dictionary<AnalogChannel, float[]> outputAnalog = new Dictionary<AnalogChannel, float[]>();
            byte[] outputDigital = null;
            int triggerIndex = 0;
            int triggerHoldoffInSamples = 0;
            double SamplePeriodLocal;
            double TriggerHoldoffLocal;
            AcquisitionMode AcquisitionModeLocal;


            if (!acquisitionRunning)
                return null;

            TimeSpan timeOffset = DateTime.Now - timeOrigin;
            if (regenerate)
            {
                Dictionary<AnalogChannel, List<float>> waveAnalog = new Dictionary<AnalogChannel, List<float>>();
                foreach(AnalogChannel ch in AnalogChannel.List)
                    waveAnalog.Add(ch, new List<float>());
                List<byte> waveDigital = new List<byte>();

                bool triggerDetected = false;

                while(true) {
                    lock (resetAcquisitionLock)
                    {
                        AcquisitionModeLocal = acquisitionMode;
                        TriggerHoldoffLocal = triggerHoldoff;
                        SamplePeriodLocal = SamplePeriod;
                    }

                    foreach (AnalogChannel channel in AnalogChannel.List)
                    {
                        float[] wave;
                        switch(waveSource) {
                            case WaveSource.GENERATOR:
                                wave = DummyScope.GenerateWave(waveLength,
                                    SamplePeriodLocal,
                                    timeOffset.Ticks / 1e7,
                                    ChannelConfig[channel]);
                                break;
                            case WaveSource.FILE:
                                wave = GetWaveFromFile(channel, waveLength, SamplePeriodLocal, timeOffset.Ticks / 1e7);
                                break;
                            default:
                                throw new Exception("Unsupported wavesource");

                        }
                        if (ChannelConfig[channel].coupling == Coupling.AC)
                            DummyScope.RemoveDcComponent(ref wave, ChannelConfig[channel].frequency, SamplePeriodLocal);
                        else
                            DummyScope.AddDcComponent(ref wave, (float)ChannelConfig[channel].dcOffset);
                        DummyScope.AddNoise(wave, ChannelConfig[channel].noise);
                        waveAnalog[channel].AddRange(wave);
                    }
                    waveDigital.AddRange(DummyScope.GenerateWaveDigital(waveLength, SamplePeriodLocal, timeOffset.TotalSeconds));

                    triggerHoldoffInSamples = (int)(TriggerHoldoffLocal / SamplePeriodLocal);
                    double triggerTimeout = 0.0;
                    if (AcquisitionModeLocal == AcquisitionMode.AUTO)
                        triggerTimeout = 0.01; //Give up after 10ms

                    if (logicAnalyser)
                    {
                        triggerDetected = DummyScope.TriggerDigital(waveDigital.ToArray(), triggerHoldoffInSamples, digitalTrigger, outputWaveLength, out triggerIndex);
                    }
                    else
                    {
                        triggerDetected = DummyScope.TriggerAnalog(waveAnalog[triggerAnalog.channel].ToArray(), triggerAnalog,
                            triggerHoldoffInSamples, triggerThreshold, triggerWidth,
                            outputWaveLength, out triggerIndex);
                    }
                    
                    if(triggerDetected)
                        break;
                    if (
                        forceTrigger || 
                        (triggerTimeout > 0 && triggerTimeout < waveAnalog[AnalogChannel.ChA].Count * SamplePeriodLocal)
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

                    var timePassed = new TimeSpan((long)(waveLength * SamplePeriodLocal * 1e7));
                    timeOffset = timeOffset.Add(timePassed);
                }
                    
                foreach(AnalogChannel channel in AnalogChannel.List)
                {
                    outputAnalog[channel] = DummyScope.CropWave(outputWaveLength, waveAnalog[channel].ToArray(), triggerIndex, triggerHoldoffInSamples);
                }
                outputDigital = DummyScope.CropWave(outputWaveLength, waveDigital.ToArray(), triggerIndex, triggerHoldoffInSamples);
            }                   
            double holdoff = triggerHoldoffInSamples * SamplePeriod;
            p = new DataPackageScope(SamplePeriod, outputWaveLength, holdoff, false, false);
            foreach(AnalogChannel ch in AnalogChannel.List)
                p.SetData(ch, outputAnalog[ch]);

            p.SetDataDigital(outputDigital);
#if __IOS__
			regenerate = true;
#endif

            if (acquisitionMode == AcquisitionMode.SINGLE)
                acquisitionRunning = false;

            return p;
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
        public void SetEnableLogicAnalyser(bool enable) 
        {
            logicAnalyser = enable;
        }
        public void SetLogicAnalyserChannel(AnalogChannel channel) { }

		#endregion

        #region Helpers
        private static bool TriggerAnalog (float [] wave, AnalogTriggerValue trigger, int holdoff, float threshold, uint width, uint outputWaveLength, out int triggerIndex)
		{
			//Hold off:
			// - if positive, start looking for trigger at that index, so we are sure to have that many samples before the trigger
			// - if negative, start looking at index 0
			triggerIndex = 0;
			for (int i = Math.Max (0, holdoff); i < wave.Length - width - outputWaveLength; i++) {
				float invertor = (trigger.direction == TriggerDirection.RISING) ? 1f : -1f;
                if (invertor * wave[i] < invertor * trigger.level && invertor * wave[i + width] >= invertor * trigger.level + threshold)
                {
					triggerIndex = (int) (i + width / 2);
					return true;
				}
			}
			return false;
		}
        private static bool TriggerDigital(byte[] wave, int holdoff, DigitalTrigger trigger, uint outputWaveLength, out int triggerIndex)
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
            float mean = p.Take(periodLength).Average();
            if (mean == 0f)
                return;
            p = p.AsParallel().Select(x => x - mean).ToArray();
        }
        #endregion
	}
}
