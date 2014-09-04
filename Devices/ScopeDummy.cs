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

    public struct DummyScopeChannelConfig
    {
        public WaveForm waveform;
        public double amplitude;
        public Coupling coupling;
        public double dcOffset;
        public double frequency;
        public double phase;
        public double noise;
    }

	public partial class DummyScope : IScope {
#if INTERNAL
        public List<DeviceMemory> GetMemories() { return null; }
#endif

		private DataSources.DataSource dataSourceScope;

		public DataSources.DataSource DataSourceScope { get { return dataSourceScope; } }

		private DateTime timeOrigin;
		//Wave settings
		private WaveSource waveSource = WaveSource.GENERATOR;
		//Noise mean voltage
		private int usbLatency = 40;
		//milliseconds of latency to simulate USB request delay
        private Dictionary<AnalogChannel, float> yOffset = new Dictionary<AnalogChannel, float>() {
            { AnalogChannel.ChA, 0f},
            { AnalogChannel.ChB, 0f}
        };
		//Scope variables
		private const int waveLength = 3 * outputWaveLength;
		private double samplePeriodMinimum = 10e-9;
		//ns --> sampleFreq of 100MHz by default
		private double SamplePeriod { get { return samplePeriodMinimum * decimation; } }

        public const uint channels = 2;
        private Dictionary<AnalogChannel, DummyScopeChannelConfig> _channelConfig = new Dictionary<AnalogChannel, DummyScopeChannelConfig>();
        public Dictionary<AnalogChannel, DummyScopeChannelConfig> ChannelConfig { get { return _channelConfig; } }

        private Dictionary<AnalogChannel, ProbeDivision> probeSettings;

		private const int outputWaveLength = 2048;
		private float triggerLevel = 0;
		private double triggerHoldoff = 0;
		private AnalogChannel triggerChannel = AnalogChannel.ChA;
		private static uint triggerWidth = 10;

        private struct DigitalTrigger {
            public byte triggerCondition;
            public byte triggerMask;
            public byte preTriggerCondition;
            public byte preTriggerMask;
        }
        private DigitalTrigger digitalTrigger;

		private uint decimation = 1;
		private TriggerDirection triggerDirection = TriggerDirection.FALLING;
        private AcquisitionMode acquisitionMode = AcquisitionMode.NORMAL;
        private bool acquisitionRunning = false;
		//Hack
		bool regenerate = true;
		DataPackageScope p;
        private int maxAttempts = 10;

		#region constructor / initializer

		public DummyScope (ScopeConnectHandler handler)
            : base ()
		{
            probeSettings = new Dictionary<AnalogChannel, ProbeDivision>();
            foreach (AnalogChannel ch in AnalogChannel.List)
            {
                _channelConfig.Add(ch, new DummyScopeChannelConfig()
                {
                    amplitude = 2.0,
                    noise = 0.1,
                    coupling = Coupling.DC,
                    dcOffset = 0.0,
                    frequency = 135e3,
                    phase = 0,
                    waveform = WaveForm.TRIANGLE
                });
                probeSettings[ch] = ProbeDivision.X1;
            }

            timeOrigin = DateTime.Now;

			dataSourceScope = new DataSources.DataSource (this);
			if (handler != null)
				handler (this, true);
		}

        public void CommitSettings()
        {
            //Nothign to do here, all settings are updated immediately;
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
            this.acquisitionMode = mode;
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
			this.triggerHoldoff = holdoff;
		}

		public void SetTriggerAnalog (float voltage)
		{
			this.triggerLevel = voltage;
		}

        public void SetVerticalRange(AnalogChannel ch, float minimum, float maximum)
        {
        }

        public void SetProbeDivision(AnalogChannel ch, ProbeDivision division)
        {
            probeSettings[ch] = division;
        }

        public ProbeDivision GetProbeDivision(AnalogChannel ch)
        {
            return probeSettings[ch];
        }

        public void SetYOffset(AnalogChannel ch, float yOffset)
		{
			this.yOffset [ch] = yOffset;
		}

        public void SetTriggerChannel(AnalogChannel ch)
		{
			this.triggerChannel = ch;
		}

		public void SetTriggerDirection (TriggerDirection direction)
		{
			this.triggerDirection = direction;
		}
        public void SetForceTrigger()
        {
            
        }

        public void SetTriggerWidth(uint width)
        {
            throw new NotImplementedException();
        }
        public uint GetTriggerWidth()
        {
            throw new NotImplementedException();
        }
        public void SetTriggerThreshold(uint threshold)
        {
            throw new NotImplementedException();
        }

        public uint GetTriggerThreshold()
        {
            throw new NotImplementedException();
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
                DigitalTriggerValue value = kvp.Value;
                if (value == DigitalTriggerValue.X)
                {
                    Utils.ClearBit(ref digitalTrigger.triggerMask, bit);
                    Utils.ClearBit(ref digitalTrigger.preTriggerMask, bit);
                }
                if (value == DigitalTriggerValue.H)
                {
                    Utils.ClearBit(ref digitalTrigger.preTriggerMask, bit);
                    Utils.SetBit(ref digitalTrigger.triggerCondition, bit);
                }
                if (value == DigitalTriggerValue.L)
                {
                    Utils.ClearBit(ref digitalTrigger.preTriggerMask, bit);
                    Utils.ClearBit(ref digitalTrigger.triggerCondition, bit);
                }
                if (value == DigitalTriggerValue.R)
                {
                    Utils.ClearBit(ref digitalTrigger.preTriggerCondition, bit);
                    Utils.SetBit(ref digitalTrigger.triggerCondition, bit);
                }
                if (value == DigitalTriggerValue.F)
                {
                    Utils.SetBit(ref digitalTrigger.preTriggerCondition, bit);
                    Utils.ClearBit(ref digitalTrigger.triggerCondition, bit);
                }
            }
		}

		public void SetTimeRange (double timeRange)
		{
			decimation = 1;
			while (timeRange > decimation * GetDefaultTimeRange ())
				decimation++;
		}
        public double GetTimeRange()
        {
            return GetDefaultTimeRange() * Math.Pow(2,decimation - 1);
        }

		public void SetCoupling (AnalogChannel ch, Coupling coupling)
		{
            DummyScopeChannelConfig config = _channelConfig[ch];
            config.coupling = coupling;
            _channelConfig[ch] = config;
		}

		public Coupling GetCoupling (AnalogChannel ch)
		{
            return _channelConfig[ch].coupling;
		}

		public double GetDefaultTimeRange ()
		{ 
			return outputWaveLength * samplePeriodMinimum; 
		}

		#endregion

		#region dummy scope settings

        public void SetDummyWaveSource(WaveSource source)
        {
            this.waveSource = source;
        }

		public void SetDummyWaveAmplitude (AnalogChannel channel, double amplitude)
		{
            DummyScopeChannelConfig config = _channelConfig[channel];
            config.amplitude = amplitude;
            _channelConfig[channel] = config;
		}

        public void SetDummyWaveFrequency(AnalogChannel channel, double frequency)
		{
            DummyScopeChannelConfig config = _channelConfig[channel];
            config.frequency = frequency;
            _channelConfig[channel] = config;
		}

        public void SetDummyWavePhase(AnalogChannel channel, double phase)
        {
            DummyScopeChannelConfig config = _channelConfig[channel];
            config.phase = phase;
            _channelConfig[channel] = config;

        }

        public void SetDummyWaveForm(AnalogChannel channel, WaveForm w)
		{
            DummyScopeChannelConfig config = _channelConfig[channel];
            config.waveform = w;
            _channelConfig[channel] = config;

		}

        public void SetDummyWaveDcOffset(AnalogChannel channel, double dcOffset)
        {
            DummyScopeChannelConfig config = _channelConfig[channel];
            config.dcOffset = dcOffset;
            _channelConfig[channel] = config;

        }

        public void SetNoiseAmplitude(AnalogChannel channel, double noiseAmplitude)
		{
            DummyScopeChannelConfig config = _channelConfig[channel];
            config.noise = noiseAmplitude;
            _channelConfig[channel] = config;

		}

        //FIXME: implement this
        public void SetEnableLogicAnalyser(bool enable) { }
        public void SetLogicAnalyserChannel(AnalogChannel channel) { }
        public void SetAwgData(double[] data) { }
        public void SetAwgEnabled(bool enable) { }
        public int GetAwgStretcherForFrequency(double frequency) { return -1; }
        public int GetAwgNumberOfSamplesForFrequency(double frequency) { return -1; }
        public void SetAwgNumberOfSamples(int n) { }
        public int GetAwgNumberOfSamples() { return -1; }
        public void SetAwgStretching(int decimation) { }
        public int GetAwgStretching() { return -1; }
        public double GetAwgFrequencyMax() { return -1; }
        public double GetAwgFrequencyMin() { return -1; }
        public void SetAwgFrequency(double frequency) { }
        public double GetAwgFrequency() { return 0; }

		#endregion

		private static bool TriggerAnalog (AcquisitionMode acqMode, float [] wave, TriggerDirection direction, int holdoff, float level, float noise, uint outputWaveLength, out int triggerIndex)
		{
			//Hold off:
			// - if positive, start looking for trigger at that index, so we are sure to have that many samples before the trigger
			// - if negative, start looking at index 0
			triggerIndex = 0;
			for (int i = Math.Max (0, holdoff); i < wave.Length - triggerWidth - outputWaveLength; i++) {
				float invertor = (direction == TriggerDirection.RISING) ? 1f : -1f;
				if (invertor * wave [i] < invertor * level - noise && invertor * wave [i + triggerWidth] + noise > invertor * level) {
					triggerIndex = (int) (i + triggerWidth / 2);
					return true;
				}
			}
            if (acqMode == AcquisitionMode.AUTO)
            {
                triggerIndex = holdoff;
                return true;
            }
            else
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

		public DataPackageScope GetScopeData ()
		{
			//Sleep to simulate USB delay
			System.Threading.Thread.Sleep (usbLatency);
			float [][] outputAnalog = null;
			byte [] outputDigital = null;
			int triggerIndex = 0;
			int triggerHoldoffInSamples = 0;

            while (!acquisitionRunning)
                System.Threading.Thread.Sleep(10);

            TimeSpan timeOffset = DateTime.Now - timeOrigin;
			if (regenerate) {
				if (waveSource == WaveSource.GENERATOR) {
                    byte[] waveDigital = null;
                    float[][] waveAnalog = null;
                    bool triggerDetected = false;
                    
                    for (int k = 0; k < maxAttempts; k++)
                    {
                        //Generate analog wave
                        waveAnalog = new float[AnalogChannel.List.Count][];
                        timeOffset = DateTime.Now - timeOrigin;
                        foreach (AnalogChannel channel in AnalogChannel.List)
                        {
                            int i = channel.Value;
                            waveAnalog[i] = DummyScope.GenerateWave(waveLength,
                                SamplePeriod,
                                timeOffset.TotalSeconds,
                                _channelConfig[channel]);
                            if (_channelConfig[channel].coupling == Coupling.AC)
                                DummyScope.RemoveDcComponent(ref waveAnalog[i], _channelConfig[channel].frequency, SamplePeriod);
                            else
                                DummyScope.AddDcComponent(ref waveAnalog[i], (float)_channelConfig[channel].dcOffset);
                            DummyScope.AddNoise(waveAnalog[i], _channelConfig[channel].noise);
                        }

                        //Generate some bullshit digital wave
                        waveDigital = new byte[waveLength];
                        byte value = (byte)(timeOffset.Milliseconds);
                        for (int i = 0; i < waveDigital.Length; i++)
                        {
                            waveDigital[i] = value;
                            if (i % 10 == 0)
                                value++;
                        }

                        //Trigger detection
                        triggerHoldoffInSamples = (int)(triggerHoldoff / SamplePeriod);


                        //FIXME: properly implement trigger and LA mode and all like in SmartScope
                        //case TriggerMode.ANALOG:
                            triggerDetected = DummyScope.TriggerAnalog(acquisitionMode, waveAnalog[triggerChannel.Value], triggerDirection,
                                triggerHoldoffInSamples, triggerLevel, (float)_channelConfig[triggerChannel].noise,
                                outputWaveLength, out triggerIndex);
                            break;
                        /*
                            case TriggerMode.DIGITAL:
                                triggerDetected = DummyScope.TriggerDigital(waveDigital, triggerHoldoffInSamples, digitalTrigger, outputWaveLength, out triggerIndex);
                                if (!triggerDetected && acquisitionMode == AcquisitionMode.AUTO)
                                {
                                    triggerDetected = true;
                                    triggerIndex = 0;
                                }
                                break;
                        }*/
                        if (triggerDetected)
                            break;
                    }
                    if (!triggerDetected)
                        return null;

					outputAnalog = new float[channels][];
					for (int i = 0; i < channels; i++) {
						outputAnalog [i] = DummyScope.CropWave (outputWaveLength, waveAnalog [i], triggerIndex, triggerHoldoffInSamples);
					}
					outputDigital = DummyScope.CropWave (outputWaveLength, waveDigital, triggerIndex, triggerHoldoffInSamples);
				} else if (waveSource == WaveSource.FILE) {
					if (!GetWaveFromFile (acquisitionMode, triggerHoldoff, triggerChannel, triggerDirection, triggerLevel, decimation, SamplePeriod, ref outputAnalog))
						return null;
                    foreach (AnalogChannel ch in AnalogChannel.List)
                        DummyScope.AddNoise(outputAnalog[ch.Value], _channelConfig[ch].noise);
					triggerHoldoffInSamples = (int) (triggerHoldoff / SamplePeriod);
				}
                double firstSampleTime = (timeOffset.TotalMilliseconds / 1.0e3) + (triggerIndex - triggerHoldoffInSamples) * SamplePeriod;
                UInt64 firstSampleTimeNs = (UInt64)(firstSampleTime * 1e9);
				p = new DataPackageScope (SamplePeriod, triggerHoldoffInSamples, outputWaveLength, firstSampleTimeNs);
				p.SetData (AnalogChannel.ChA, outputAnalog [0]);
                p.SetData(AnalogChannel.ChB, outputAnalog[1]);

				p.SetDataDigital (outputDigital);
			}
#if __IOS__
			regenerate = true;
#endif

            if (acquisitionMode == AcquisitionMode.SINGLE)
                acquisitionRunning = false;

			return p;
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
	}
}
