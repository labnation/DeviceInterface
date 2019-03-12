using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.DataSources;
using LabNation.Common;
using LabNation.DeviceInterface.Memories;
using LabNation.DeviceInterface.Hardware;
#if ANDROID
    using Android.Media;
#endif

namespace LabNation.DeviceInterface.Devices {

    public class DummyScopeChannelConfig
    {
        public AnalogWaveForm waveform;
        public double amplitude;
        public Coupling coupling;
        public double dcOffset;
        public double frequency;
        public double phase;
        public double dutyCycle;
        public double noise;
        public int bursts;
    }

	public partial class DummyScope : IDevice, IScope {
#if DEBUG
        public List<DeviceMemory> GetMemories() { return null; }
#endif

        public IHardwareInterface HardwareInterface { get { return this.hardwareInterface; }  }
        public DummyInterface _hardwareInterface;
        public DummyInterface hardwareInterface
        {
            get { return this._hardwareInterface; }
            set
            {
                this._hardwareInterface = value;
                double origAcqLength = AcquisitionLength;
                if (value.Serial == DummyInterface.Audio)
                    BASE_SAMPLE_PERIOD = 1f / 44100f;
                else
                    BASE_SAMPLE_PERIOD = 10e-9;
                AcquisitionLength = origAcqLength;

                if (acquisitionRunning)
                {
                    if (value.Serial == DummyInterface.Audio)
                        InitAudioJack();
                    else
                        KillAudioJack();
                }
            }
        }

        public string Serial { get { return HardwareInterface.Serial; } }

        public bool isAudio { get { return this.HardwareInterface.Serial == DummyInterface.Audio; } }
        public bool isGenerator { get { return this.HardwareInterface.Serial == DummyInterface.Generator; } }
        public bool isFile { get { return this.HardwareInterface.Serial == DummyInterface.File; } }

        public DataSources.DataSource DataSourceScope { get; private set; }
		private DateTime timeOrigin;
		//Wave settings
        private int usbLatency = 10;
        private uint acquisitionDepth = 2048;
        private object acquisitionSettingsLock = new object();
        private bool forceTrigger = false;
        private int acquistionId = 0;
        public bool SuspendViewportUpdates { get; set; }
        public event AcquisitionTransferFinishedHandler OnAcquisitionTransferFinished;

        Dictionary<AnalogChannel, float[]> acquisitionBufferAnalog = new Dictionary<AnalogChannel, float[]>();
        byte[] acquisitionBufferDigital = null;

#if ANDROID
		//audio jack parts
		private int audioBufferLengthInBytes;
		private AudioRecord audioJack = null;
#endif
        
        //milliseconds of latency to simulate USB request delay
        private Dictionary<AnalogChannel, float> yOffset = new Dictionary<AnalogChannel, float>() {
            { AnalogChannel.ChA, 0f},
            { AnalogChannel.ChB, 0f}
        };
		
        //Acquisition variables
        private AcquisitionMode acquisitionMode = AcquisitionMode.NORMAL;
        private bool acquisitionRunning = false;

        private double SamplePeriodCurrent = 0;
        private uint waveLengthCurrent = 0;
        private double TriggerHoldoffCurrent;
        private uint acquisitionDepthCurrent;
        private bool logicAnalyserEnabledCurrent;
        private AnalogChannel logicAnalyserChannelCurrent;
		private DataPackageScope lastCommittedDataPackage = null;
        		
        private uint waveLength { get { return 2 * acquisitionDepth; } }
        internal double BASE_SAMPLE_PERIOD = 10e-9; //10MHz sample rate
        internal uint DECIMATION_MAX = 10;
        private static uint ACQUISITION_DEPTH_MIN = 1024;
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

        public uint AcquisitionDepthUserMaximum { get; set; }
        public uint AcquisitionDepthMax
        {
            get { return (uint)ACQUISITION_DEPTH_MAX; }
        }
        public uint InputDecimationMax
        {
            get { return (uint)9; }
        }
        public int SubSampleRate { get { return 0; } }

        public Dictionary<AnalogChannel, DummyScopeChannelConfig> ChannelConfig { get; private set; }

        //Trigger
        private double triggerHoldoff = 0;
        private uint triggerWidth = 10;
        private float triggerThreshold = 0;

        TriggerValue triggerValue = new TriggerValue
        {
            source = TriggerSource.Channel,
            channel = AnalogChannel.ChA,
            level = 0f,
            edge = TriggerEdge.RISING
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
        private object viewportUpdateLock = new object();

        private const int OVERVIEW_LENGTH = 2048;
        private const int VIEWPORT_SAMPLES_MIN = 128;
        private const int VIEWPORT_SAMPLES_MAX = 2048;
        private const int VIEW_DECIMATION_MAX = 10;

		DataPackageScope p;
        private static int GENERATION_LENGTH_MAX = (int)ACQUISITION_DEPTH_MAX * 3; //Don't generate more than this many samples of wave

		#region constructor / initializer

		public DummyScope (DummyInterface iface) : base ()
		{
            this.hardwareInterface = iface;
            ChannelConfig = new Dictionary<AnalogChannel, DummyScopeChannelConfig>() 
            {
                { AnalogChannel.ChA, new DummyScopeChannelConfig()
                    {
                        amplitude = 2,
                        noise = 0,
                        coupling = Coupling.DC,
                        dcOffset = 0.0,
                        frequency = 10e3,
                        phase = 0,
                        dutyCycle = 0.5f,
                        waveform = AnalogWaveForm.TRIANGLE
                    }
                },
                { AnalogChannel.ChB, new DummyScopeChannelConfig() 
                    {
                        amplitude = 1,
                        noise = 0.015,
                        coupling = Coupling.DC,
                        dcOffset = 0.0,
                        frequency = 10e3,
                        phase = 0,
                        dutyCycle = 0.5f,
                        waveform = AnalogWaveForm.SINE
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
                lock (acquisitionSettingsLock)
                {
                    this.acquisitionMode = value;
                }
            }
			get {
				return this.acquisitionMode;
			}
        }
#if ANDROID
		private void InitAudioJack()
		{
			audioBufferLengthInBytes = AudioRecord.GetMinBufferSize (44100, ChannelIn.Mono, Android.Media.Encoding.Pcm16bit);
			audioJack = new AudioRecord (AudioSource.Mic, 44100, ChannelIn.Mono, Android.Media.Encoding.Pcm16bit, audioBufferLengthInBytes);
			audioJack.StartRecording ();
		}

		private void KillAudioJack()
		{
			try
			{
				audioJack.Stop();
				audioJack.Release();
			}
			catch { 
				//do nothing, this was anyhow just an attempt to release the audio jack in a clean way
			}
		}
#else
        private void InitAudioJack() { }
        private void KillAudioJack() { }
#endif

        public bool Running {
            set
            {
				if (value) {
					double origAcqLength = AcquisitionLength;
					if (isAudio)
						BASE_SAMPLE_PERIOD = 1f / 44100f;
					else
						BASE_SAMPLE_PERIOD = 10e-9;
					AcquisitionLength = origAcqLength;
					StopPending = false;
                    if (isAudio && !this.acquisitionRunning)
						InitAudioJack ();
					this.acquisitionRunning = value;
				} else {
#if ANDROID
					if (this.acquisitionRunning && audioJack != null)
						KillAudioJack ();
#endif
					StopPending = true;
				}
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
                lock (acquisitionSettingsLock)
                {
                    if (value > AcquisitionTimeSpan)
                        this.triggerHoldoff = AcquisitionTimeSpan;
                    //else if (value < 0)
                        //this.triggerHoldoff = 0;
                    else
                        this.triggerHoldoff = value;
                }
            }
            get
            {
                return this.triggerHoldoff;
            }
		}

        public bool SendOverviewBuffer { get; set; }

        public TriggerValue TriggerValue
        {
            get { return this.triggerValue.Copy(); }
            set { 
                this.triggerValue = value;
                TriggerDigital = this.triggerValue.Digital;
            }
        }
        public void SetVerticalRange(AnalogChannel ch, float minimum, float maximum)
        {
        }
        public void SetYOffset(AnalogChannel ch, float yOffset)
		{
			this.yOffset [ch] = yOffset;
		}
		public float GetYOffset(AnalogChannel ch)
		{
		   return this.yOffset[ch];
		}
        public float GetYOffsetLimit1(AnalogChannel ch) { return float.MaxValue; }
        public float GetYOffsetLimit2(AnalogChannel ch) { return float.MinValue; }

        public void ForceTrigger()
        {
            if (!forceTrigger)
            {
                awaitingTrigger = false;
                forceTrigger = true;
            }
        }

        private Dictionary<DigitalChannel, DigitalTriggerValue> TriggerDigital
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

            if (timespan > maxTimeSpan)
                timespan = maxTimeSpan;
            if (timespan < SamplePeriod)
                timespan = SamplePeriod;

            ViewPortOffset = offset;
            ViewPortTimeSpan = timespan;
            lock (viewportUpdateLock)
            {
                viewportUpdate = true;
            }
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

        public double AcquisitionLengthMin
        {
            get { return ACQUISITION_DEPTH_MIN * BASE_SAMPLE_PERIOD; }
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
                if (isAudio)
                {
					AcquisitionDepth = (uint)samples;
					decimation = 0;
				} else {
					double ratio = (double)samples / OVERVIEW_LENGTH;
					int log2OfRatio = (int)Math.Ceiling (Math.Log (ratio, 2));
					if (log2OfRatio < 0)
						log2OfRatio = 0;
					if (log2OfRatio > ACQUISITION_DEPTH_POWER_MAX)
						log2OfRatio = ACQUISITION_DEPTH_POWER_MAX;
					AcquisitionDepth = (uint)(OVERVIEW_LENGTH * Math.Pow (2, log2OfRatio));

					ratio = samples / AcquisitionDepth;
					log2OfRatio = (int)Math.Ceiling (Math.Log (ratio, 2));
					if (log2OfRatio < 0)
						log2OfRatio = 0;
					decimation = (uint)log2OfRatio;
				}
            }
        }

        public uint AcquisitionDepth
        {
            set {
                lock (acquisitionSettingsLock)
                {
                    if (value == 0) //Overflowing - take max
                    {
                        acquisitionDepth = ACQUISITION_DEPTH_MAX;
                    }
                    else
                    {
                        double log2OfRatio = Math.Log((double)value / OVERVIEW_LENGTH, 2);
                        if (log2OfRatio != (int)log2OfRatio && !isAudio)
                        {
                            //this only happens on some platforms. If it happens, the difference is like 20,9999999996641 vs 20
                            //probably rounding issue -> round and correct instead of crash
                            Logger.Error("Acquisition depth must be " + OVERVIEW_LENGTH + " * 2^N  ---  " + log2OfRatio.ToString() + " vs " + ((int)log2OfRatio).ToString());
                            log2OfRatio = (int)Math.Round(log2OfRatio);
                            value = (uint)Math.Pow(2, log2OfRatio);
                        }
                        if (value > ACQUISITION_DEPTH_MAX)
                            acquisitionDepth = ACQUISITION_DEPTH_MAX;
                        else
                            acquisitionDepth = value;
                    }
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
            if (timeOrigin          == null) return null;
            if (viewportUpdateLock  == null) return null;
            if (timeOrigin == null) return null;
            if (acquisitionRunning == null) return null;

            //Sleep to simulate USB delay
            System.Threading.Thread.Sleep(usbLatency);
            TimeSpan timeOffset = DateTime.Now - timeOrigin;

			List<AnalogChannel> channelsToAcquireDataFor = new List<AnalogChannel> ();
			if (isAudio)
				channelsToAcquireDataFor.Add (AnalogChannel.ChA);
			else
				channelsToAcquireDataFor.AddRange (AnalogChannel.List);
			
            if (acquisitionRunning)
            {
                lock (viewportUpdateLock)
                {
                    viewportUpdate = true;
                }
                int triggerHoldoffInSamples = 0;
                int triggerIndex = 0;
                Dictionary<AnalogChannel, List<float>> waveAnalog = new Dictionary<AnalogChannel, List<float>>();
                foreach(AnalogChannel ch in AnalogChannel.List)
                    waveAnalog.Add(ch, new List<float>((int)waveLength));
                List<byte> waveDigital = new List<byte>();

                bool triggerDetected = false;

                //loop until trigger condition is met
                while(true) {
					//in case the stop button has been pressed, this section makes sure the last-shown acquisition is kept on the display (otherwise it is replaced by a new acquisition)
					if ((StopPending || !acquisitionRunning) && (lastCommittedDataPackage != null)) {
						acquisitionRunning = false;
						return lastCommittedDataPackage;
					}

                    AcquisitionMode AcquisitionModeCurrent;
                    lock (acquisitionSettingsLock)
                    {
                        acquisitionBufferAnalog = new Dictionary<AnalogChannel, float[]>();
                        AcquisitionModeCurrent = acquisitionMode;
                        acquisitionDepthCurrent = AcquisitionDepth;
                        TriggerHoldoffCurrent = triggerHoldoff;
                        SamplePeriodCurrent = SamplePeriod;
                        waveLengthCurrent = waveLength;
                        logicAnalyserChannelCurrent = logicAnalyserChannel;
                        logicAnalyserEnabledCurrent = LogicAnalyserEnabled;
                    }

                    acquistionId++;

                    //Stop trying to find a trigger at some point to avoid running out of memory
                    if (waveAnalog.Where(x => x.Value.Count > GENERATION_LENGTH_MAX).Count() > 0 || waveDigital.Count > GENERATION_LENGTH_MAX)
                    {
                        System.Threading.Thread.Sleep(10);
                        return null;
                    }

                    //ANALOG CHANNELS DATA GENERATION
					foreach (AnalogChannel channel in channelsToAcquireDataFor)
                    {
                        if (!ChannelConfig.ContainsKey(channel)) return null;

                        if (logicAnalyserEnabledCurrent && channel == logicAnalyserChannelCurrent)
                            continue;
                        float[] wave;
                        if (HardwareInterface.Serial == DummyInterface.Generator)
                            wave = DummyScope.GenerateWave(waveLengthCurrent,
                                SamplePeriodCurrent,
                                timeOffset.Ticks / 1e7,
                                ChannelConfig[channel]);
                        else if (HardwareInterface.Serial == DummyInterface.File)
                        {
                            double timeOffsetFromFile = 0;
                            wave = (hardwareInterface as DummyInterfaceFromFile).GetWaveFromFile(channel, ref waveLengthCurrent, ref SamplePeriodCurrent, ref timeOffsetFromFile); //in case of FileReader, the file actually dictates most of the settings
                            acquisitionDepthCurrent = waveLengthCurrent;
                            //timeOffset = new TimeSpan((long)(timeOffsetFromFile * 1e7));
                            //ViewPortTimeSpan = SamplePeriodCurrent * (double)waveLengthCurrent;
                            //ViewPortOffset = 0; //MUSTFIX
                            //acquisitionDepthCurrent = waveLengthCurrent;
                        }
#if ANDROID
                        else if( hardwareInterface == DummyInterface.Audio) {
							//fetch audio data
							if (audioJack == null) return null;
							byte[] audioData = new byte[audioBufferLengthInBytes];
							int bytesRead = audioJack.Read (audioData, 0, audioBufferLengthInBytes); //2 bytes per sample
							int watchdog = 0;
							while (bytesRead <= 0 && watchdog++ < 1000) {
								System.Threading.Thread.Sleep (1);
								bytesRead = audioJack.Read (audioData, 0, audioBufferLengthInBytes); //2 bytes per sample
							}

							//convert bytes to shorts
							short[] sampleData = new short[audioData.Length / 2];
							Buffer.BlockCopy (audioData, 0, sampleData, 0, sampleData.Length * 2);

							//and then into floats
							wave = new float[sampleData.Length];
							for (int i = 0; i < wave.Length; i++)
								wave [i] = (float)sampleData [i] / (float)short.MaxValue;

							//in case of large zoomouts, decimation will be > 0
							//FIXME: this is not the best location to do this. time-errors will accumulate. better to do this on eventual wave. but then trigger index etc needs to be adjusted     
							int skip = 1 << (int)decimation;
							wave = wave.Where((x, i) => i % skip == 0).ToArray();

						}
#endif
                        else
                            throw new Exception("Unsupported dummy interface");

						//coupling, noise injection in SW. Not needed for File or Audio generators
                        if (isGenerator)
                        {
							if (ChannelConfig [channel].coupling == Coupling.AC)
								DummyScope.RemoveDcComponent (ref wave, ChannelConfig [channel].frequency, SamplePeriodCurrent);
							else
								DummyScope.AddDcComponent (ref wave, (float)ChannelConfig [channel].dcOffset);
							DummyScope.AddNoise (wave, ChannelConfig [channel].noise);
						}
                        waveAnalog[channel].AddRange(wave);
                    }

                    //DIGITAL CHANNELS DATA GENERATION
                    if (!isAudio && logicAnalyserEnabledCurrent) //MUSTFIX: add LA support for FileReader
                        waveDigital.AddRange(DummyScope.GenerateWaveDigital(waveLengthCurrent, SamplePeriodCurrent, timeOffset.TotalSeconds));

                    //SEARCH TRIGGER POSITION. STORE IN triggerIndex
                    triggerHoldoffInSamples = (int)(TriggerHoldoffCurrent / SamplePeriodCurrent);
                    double triggerTimeout = 0.0;
                    if (AcquisitionModeCurrent == AcquisitionMode.AUTO)
						triggerTimeout = SamplePeriodCurrent * acquisitionDepthCurrent * 1.0; //Give up after twice the acqbuffer timespan

                    //detect digital trigger
                    if (logicAnalyserEnabledCurrent && this.triggerValue.mode == TriggerMode.Digital)
                    {
                        triggerDetected = DummyScope.DoTriggerDigital(waveDigital.ToArray(), triggerHoldoffInSamples, digitalTrigger, acquisitionDepthCurrent, out triggerIndex);
                        if (isAudio)
							triggerDetected = false;
                    }
                    else
                    //detect analog trigger
                    {
                        if (triggerValue == null) return null;
                        if (triggerValue.source == null) return null;

                        if (triggerValue.source == TriggerSource.External)
                            triggerDetected = false;
                        triggerDetected = DummyScope.DoTriggerAnalog(waveAnalog[triggerValue.channel].ToArray(), triggerValue,
                            triggerHoldoffInSamples, triggerThreshold, triggerWidth,
                            acquisitionDepthCurrent, out triggerIndex);
                    }
                    awaitingTrigger = !triggerDetected;

                    //END DATA GENERATION WHILE LOOP
                    //break out of while loop if trigger was detected
                    if (triggerDetected)
                    {
                        forceTrigger = false;
                        awaitingTrigger = false;
                        break;
                    }

                    //break out of while loop if triggerWasForced or synthetical 10ms limit was reached or when reading from file
                    if (
                        forceTrigger ||
                        (triggerTimeout > 0 && waveAnalog[AnalogChannel.ChA].Count * SamplePeriodCurrent >= triggerTimeout) || isFile
                    )
                    {
                        forceTrigger = false;
                        triggerIndex = triggerHoldoffInSamples;
                        awaitingTrigger = false;
                        break;
                    }

                    //HOUSEKEEPING WHILE LOOP
                    //keep track of time of first samplemoment
                    var timePassed = new TimeSpan((long)(waveLengthCurrent * SamplePeriodCurrent * 1e7));
                    timeOffset = timeOffset.Add(timePassed);
                } // end of while loop -- at this point 'waveAnalog' and 'waveDigital' contains useful data. Either because the trigger has been found, too much time has passed or data has been read from file
                
                //CPU-GPU OPTIMISATION
                //crop wave to only displayable part and store in buffer    
                foreach (AnalogChannel channel in channelsToAcquireDataFor)
                {
                    if (logicAnalyserEnabledCurrent && channel == logicAnalyserChannelCurrent)
                        continue;
                    else if (isFile)
                        acquisitionBufferAnalog[channel] = waveAnalog[channel].ToArray();
                    else
                        acquisitionBufferAnalog[channel] = DummyScope.CropWave(acquisitionDepthCurrent, waveAnalog[channel].ToArray(), triggerIndex, triggerHoldoffInSamples);
                }
                acquisitionBufferDigital = DummyScope.CropWave(acquisitionDepthCurrent, waveDigital.ToArray(), triggerIndex, triggerHoldoffInSamples);
                //from this point onwards, 'waveAnalog' and 'waveDigital' are no longer used. data now stored instead in 'acquisitionBufferAnalog' and 'acquisitionBufferDigital'

                if (StopPending)
                {
                    acquisitionRunning = false;
                }
            }// ends 'if (acquisitionRunning)'. so when stopped both buffers contain the data of the previous call.

            lock (viewportUpdateLock)
            {
                if (!viewportUpdate)
                    return null;
                viewportUpdate = false;
            }

            if (acquisitionBufferAnalog == null) return null;
            if (acquisitionBufferAnalog.Count == 0) return null;

            //VIEWPORT DECIMATION.
            //Decrease the number of samples till viewport sample period is larger than 
            //or equal to the full sample rate
            uint samples = VIEWPORT_SAMPLES_MAX;
            int viewportDecimation = 0;
            if (!isFile)
            {
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
            }
            int viewportSamples = (int)(ViewPortTimeSpan / (SamplePeriodCurrent * Math.Pow(2, viewportDecimation))) + 2;
            int viewportOffsetLocal = (int)(ViewPortOffset / SamplePeriodCurrent);

            //CREATE DATAPACKAGESCOPE
            p = new DataPackageScope(this.GetType(),
                    acquisitionDepthCurrent, SamplePeriodCurrent, 
                    viewportSamples, (Int64)(ViewPortOffset / SamplePeriodCurrent),
                    TriggerHoldoffCurrent, (Int64)(TriggerHoldoffCurrent/SamplePeriodCurrent), false, acquistionId, TriggerValue);
            p.FullAcquisitionFetchProgress = 1f;
            p.samplePeriod[ChannelDataSourceScope.Viewport] = SamplePeriodCurrent * Math.Pow(2, viewportDecimation);
            p.offset[ChannelDataSourceScope.Viewport] = ViewPortOffset;

			//set values, needed for ETS to work properly
            if (acquisitionBufferAnalog != null && acquisitionBufferAnalog.ContainsKey(AnalogChannel.ChA))
            {
                p.samplePeriod[ChannelDataSourceScope.Overview] = SamplePeriodCurrent * (float)acquisitionBufferAnalog[AnalogChannel.ChA].Length / (float)OVERVIEW_LENGTH;
                p.offset[ChannelDataSourceScope.Overview] = 0;
            }

			if (acquisitionBufferAnalog.Count == 0)
				return lastCommittedDataPackage;

			foreach (AnalogChannel ch in channelsToAcquireDataFor)
            {
                if (logicAnalyserEnabledCurrent && ch == logicAnalyserChannelCurrent)
                    continue;
                if (SendOverviewBuffer)
                {
                    Array arr = DecimateViewport(acquisitionBufferAnalog[ch], 0, (int)(Math.Log(acquisitionDepthCurrent / OVERVIEW_LENGTH, 2)), OVERVIEW_LENGTH);
                    p.SetData(ChannelDataSourceScope.Overview, ch, arr);
                }
                var decimatedViewport = DecimateViewport(acquisitionBufferAnalog[ch], viewportOffsetLocal, viewportDecimation, viewportSamples);
                p.SetData(ChannelDataSourceScope.Viewport, ch, decimatedViewport);
                p.SetData(ChannelDataSourceScope.Acquisition, ch, acquisitionBufferAnalog[ch]);

                //set dummy minmax values
                p.SaturationLowValue[ch] = float.MinValue;
                p.SaturationHighValue[ch] = float.MaxValue;

                //set 20mV as resolution, which is needed for some processors (like freqdetection). Don't go too low, as ETS uses this in its difference detector
                p.Resolution[ch] = 0.020f;
            }

            if (logicAnalyserEnabledCurrent)
            {
                if (SendOverviewBuffer)
                    p.SetData(ChannelDataSourceScope.Overview, LogicAnalyserChannel.LA, DecimateViewport(acquisitionBufferDigital, 0, (int)(Math.Log(acquisitionDepthCurrent / OVERVIEW_LENGTH, 2)), OVERVIEW_LENGTH));
                p.SetData(ChannelDataSourceScope.Viewport, LogicAnalyserChannel.LA, DecimateViewport(acquisitionBufferDigital, viewportOffsetLocal, viewportDecimation, viewportSamples));
                p.SetData(ChannelDataSourceScope.Acquisition, LogicAnalyserChannel.LA, acquisitionBufferDigital);
            }

            if (acquisitionMode == AcquisitionMode.SINGLE)
                acquisitionRunning = false;

			lastCommittedDataPackage = p;
            return p;
        }

        public static T[] DecimateViewport<T>(T[] buffer, int offset, int decimation, int length)
        {
            if (buffer == null)
                return null;

            int skip = 1 << decimation;
            return buffer.Skip(offset).Take(length * skip).Where((x, i) => i % skip == 0).ToArray();
        }

		#endregion

		#region dummy scope settings

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
        public void SetDummyWaveDutyCycle(AnalogChannel channel, double dc)
        {
            ChannelConfig[channel].dutyCycle = dc > 1 ? 1 : dc < 0 ? 0 : dc;
        }
        public void SetDummyWaveForm(AnalogChannel channel, AnalogWaveForm w)
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

        public bool LogicAnalyserEnabled
        {
            get
            {
                return this.logicAnalyserChannel != null;
            }
        }
        private AnalogChannel logicAnalyserChannel = AnalogChannel.ChB;
        public AnalogChannel ChannelSacrificedForLogicAnalyser
        {
            set 
            {
                lock (acquisitionSettingsLock)
                {
                    this.logicAnalyserChannel = value;
                }
            }
        }

		#endregion

        #region Helpers
        private static bool DoTriggerAnalog (float [] wave, TriggerValue trigger, int holdoff, float threshold, uint width, uint outputWaveLength, out int triggerIndex)
		{
			//Hold off:
			// - if positive, start looking for trigger at that index, so we are sure to have that many samples before the trigger
			// - if negative, start looking at index 0
			triggerIndex = 0;
            uint halfWidth = width / 2;
            uint preconditionCounterRising = 0;
            uint preconditionCounterFalling = 0;
            uint postconditionCounterRising = 0;
            uint postconditionCounterFalling = 0;
			int startIndex = Math.Max (0, holdoff);
			int maxI = (int)Math.Min (wave.Length - width - outputWaveLength + startIndex, wave.Length);
			for (int i = startIndex; i < maxI; i++) {
                bool preconditionRisingMet = preconditionCounterRising == halfWidth;
                bool preconditionFallingMet = preconditionCounterFalling == halfWidth;
                if (preconditionRisingMet)
                {
                    if (wave[i] >= trigger.level + threshold)
                        postconditionCounterRising++;
                }
                else
                    postconditionCounterRising = 0;

                if (preconditionFallingMet)
                {
                    if (wave[i] <= trigger.level - threshold)
                        postconditionCounterFalling++;
                }
                else
                    postconditionCounterFalling = 0;

                if (wave[i] < trigger.level && !preconditionRisingMet)
                {
                    preconditionCounterRising++;
                }
                if (wave[i] > trigger.level && !preconditionFallingMet)
                {
                    preconditionCounterFalling++;
                }

                if (
                    (preconditionRisingMet && postconditionCounterRising == halfWidth && trigger.edge != TriggerEdge.FALLING) 
                ||
                    (preconditionFallingMet && postconditionCounterFalling == halfWidth && trigger.edge != TriggerEdge.RISING) 
                )
                {
                    int triggerIndexTmp = (int)(i - width / 2);
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
