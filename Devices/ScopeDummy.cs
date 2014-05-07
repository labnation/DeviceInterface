using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.DataPackages;

namespace ECore.Devices {
	public enum WaveSource {
		FILE,
		GENERATOR
	}

	public partial class ScopeDummy : EDevice, IScope {
		private DataSources.DataSourceScope dataSourceScope;

		public DataSources.DataSourceScope DataSourceScope { get { return dataSourceScope; } }

		private DateTime timeOrigin;
		//Wave settings
		private WaveSource waveSource = WaveSource.FILE;
		private TriggerMode triggerMode = TriggerMode.ANALOG;
		private WaveForm [] waveForm = { WaveForm.SINE, WaveForm.SAWTOOTH_SINE };
		private double [] amplitude = new double[] { 1.3, 1.8 };
		private Coupling [] coupling = new Coupling[] {
			Coupling.DC,
			Coupling.DC
		};
		private double [] dcOffset = new double[] { 0.0f, -0.9f };
		private double [] frequency = new double[] { 200e3, 600e3 };
		private double [] noiseAmplitude = new double[] { 0.1, 0.1 };
		//Noise mean voltage
		private int usbLatency = 40;
		//milliseconds of latency to simulate USB request delay
		private float [] yOffset = new float[] { 0, 0 };
		//Scope variables
		private const uint waveLength = 3 * outputWaveLength;
		private double samplePeriodMinimum = 10e-9;
		//ns --> sampleFreq of 100MHz by default
		private double SamplePeriod { get { return samplePeriodMinimum * decimation; } }

		public const uint channels = 2;
		private const uint outputWaveLength = 2048;
		private float triggerLevel = 0;
		private double triggerHoldoff = 0;
		private uint triggerChannel = 0;
		private static uint triggerWidth = 4;

        private struct DigitalTrigger {
            public byte triggerCondition;
            public byte triggerMask;
            public byte preTriggerCondition;
            public byte preTriggerMask;
        }
        private DigitalTrigger digitalTrigger;

		private uint decimation = 1;
		private TriggerDirection triggerDirection = TriggerDirection.FALLING;
        private AcquisitionMode acquisitionMode = AcquisitionMode.CONTINUOUS;
        private bool acquisitionRunning = false;
		//Hack
		bool regenerate = true;
		DataPackageScope p;

		#region constructor / initializer

		public ScopeDummy (ScopeConnectHandler handler)
            : base ()
		{
			dataSourceScope = new DataSources.DataSourceScope (this);
			if (handler != null)
				handler (this, true);
		}

		public void Configure ()
		{ 
			timeOrigin = DateTime.Now;
		}

		#endregion

		#region real scope settings

		private void validateChannel (uint ch)
		{
			if (ch >= channels)
				throw new ValidationException ("Channel must be between 0 and " + (channels - 1));
		}

		private void validateDecimation (uint decimation)
		{
			if (decimation < 1)
				throw new ValidationException ("Decimation must be larger than 0");
		}

        public void SetAcquisitionMode(AcquisitionMode mode)
        {
            this.acquisitionMode = mode;
        }
        public void SetAcuisitionRunning(bool running)
        {
            this.acquisitionRunning = running;
        }
        public bool GetAcuisitionRunning()
        {
            return this.acquisitionRunning;
        }

		public void SetTriggerHoldOff (double holdoff)
		{
			this.triggerHoldoff = holdoff;
		}

		public void SetTriggerAnalog (float voltage)
		{
			this.triggerLevel = voltage;
		}

		public void SetYOffset (uint channel, float yOffset)
		{
			validateChannel (channel);
			this.yOffset [channel] = yOffset;
		}

		public void SetTriggerChannel (uint channel)
		{
			validateChannel (channel);
			this.triggerChannel = channel;
		}

		public void SetTriggerDirection (TriggerDirection direction)
		{
			this.triggerDirection = direction;
		}

		public void SetTriggerMode (TriggerMode mode)
		{
			this.triggerMode = mode;
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
                if (value == DigitalTriggerValue.I)
                {
                    Utils.ClearBit(ref digitalTrigger.preTriggerMask, bit);
                    Utils.SetBit(ref digitalTrigger.triggerCondition, bit);
                }
                if (value == DigitalTriggerValue.O)
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

		public void SetCoupling (uint channel, Coupling coupling)
		{
			validateChannel (channel);
			this.coupling [channel] = coupling;
		}

		public Coupling GetCoupling (uint channel)
		{
			validateChannel (channel);
			return coupling [channel];
		}

		public double GetDefaultTimeRange ()
		{ 
			return outputWaveLength * samplePeriodMinimum; 
		}

        public double GetSamplePeriod()
        {
            return SamplePeriod;
        }

        public int GetNumberOfSamples()
        {
            return (int)outputWaveLength;
        }

		#endregion

		#region dummy scope settings

		public void SetDummyWaveAmplitude (uint channel, double amplitude)
		{
			validateChannel (channel);
			this.amplitude [channel] = amplitude;
		}

		public void SetDummyWaveFrequency (uint channel, double frequency)
		{
			validateChannel (channel);
			this.frequency [channel] = frequency;
		}

		public void SetDummyWaveForm (uint channel, WaveForm w)
		{
			validateChannel (channel);
			this.waveForm [channel] = w;
		}

		public void SetNoiseAmplitude (uint channel, double noiseAmplitude)
		{
			validateChannel (channel);
			this.noiseAmplitude [channel] = noiseAmplitude;
		}

		#endregion

		private static bool TriggerAnalog (float [] wave, TriggerDirection direction, int holdoff, float level, float noise, uint outputWaveLength, out int triggerIndex)
		{
			//Hold off:
			// - if positive, start looking for trigger at that index, so we are sure to have that many samples before the trigger
			// - if negative, start looking at index 0
			triggerIndex = 0;
			for (int i = Math.Max (0, holdoff); i < wave.Length - triggerWidth - outputWaveLength; i++) {
				float invertor = (direction == TriggerDirection.RISING) ? 1f : -1f;
				if (invertor * wave [i] < invertor * level - noise && invertor * wave [i + triggerWidth] > invertor * level) {
					triggerIndex = (int) (i + triggerWidth / 2);
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

			if (regenerate) {
				if (waveSource == WaveSource.GENERATOR) {
					//Generate analog wave
					float [][] waveAnalog = new float[channels][];
					TimeSpan timeOffset = DateTime.Now - timeOrigin;
					for (int i = 0; i < channels; i++) {
						waveAnalog [i] = ScopeDummy.GenerateWave (waveForm [i], waveLength,
							SamplePeriod,
							timeOffset.TotalSeconds,
							frequency [i],
							amplitude [i], 0, dcOffset [i]);
						ScopeDummy.AddNoise (waveAnalog [i], noiseAmplitude [i]);
					}

					//Generate some bullshit digital wave
					byte [] waveDigital = new byte[waveLength];
					byte value = 0;
					for (int i = 0; i < waveDigital.Length; i++) {
						waveDigital [i] = value;
						if (i % 10 == 0)
							value++;
					}

					//Trigger detection
					triggerHoldoffInSamples = (int) (triggerHoldoff / SamplePeriod);

					bool triggerDetected = false;
					switch (triggerMode) {
					case TriggerMode.ANALOG:
						triggerDetected = ScopeDummy.TriggerAnalog (waveAnalog [triggerChannel], triggerDirection,
							triggerHoldoffInSamples, triggerLevel, (float) noiseAmplitude [triggerChannel], 
							outputWaveLength, out triggerIndex);
						break;
					case TriggerMode.DIGITAL:
                        triggerDetected = ScopeDummy.TriggerDigital(waveDigital, triggerHoldoffInSamples, digitalTrigger, outputWaveLength, out triggerIndex);
						break;
					case TriggerMode.FREE_RUNNING:
						triggerDetected = true;
						triggerIndex = 0;
						triggerHoldoffInSamples = 0;
						break;
					}
					if (!triggerDetected)
						return null;

					outputAnalog = new float[channels][];
					for (int i = 0; i < channels; i++) {
						outputAnalog [i] = ScopeDummy.CropWave (outputWaveLength, waveAnalog [i], triggerIndex, triggerHoldoffInSamples);     
					}
					outputDigital = ScopeDummy.CropWave (outputWaveLength, waveDigital, triggerIndex, triggerHoldoffInSamples);
				} else if (waveSource == WaveSource.FILE) {

					if (!GetWaveFromFile (triggerMode, triggerHoldoff, triggerChannel, triggerDirection, triggerLevel, decimation, SamplePeriod, ref outputAnalog))
						return null;
					for (int i = 0; i < channels; i++)
						ScopeDummy.AddNoise (outputAnalog [i], noiseAmplitude [i]);
					triggerHoldoffInSamples = (int) (triggerHoldoff / SamplePeriod);
				}
				p = new DataPackageScope (SamplePeriod, triggerHoldoffInSamples);
				p.SetData (ScopeChannels.ChA, outputAnalog [0]);
				p.SetData (ScopeChannels.ChB, outputAnalog [1]);
				p.SetOffset (ScopeChannels.ChA, yOffset [0]);
				p.SetOffset (ScopeChannels.ChB, yOffset [1]);
				p.SetDataDigital (outputDigital);
			}
#if __IOS__
			regenerate = true;
#endif

            if (acquisitionMode == AcquisitionMode.SINGLE)
                acquisitionRunning = false;

			return p;
		}
	}
}
