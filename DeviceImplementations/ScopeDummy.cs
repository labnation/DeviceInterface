using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.DataPackages;

namespace ECore.DeviceImplementations
{
    public partial class ScopeDummy : EDeviceImplementation, IScope
    {
        private DateTime timeOrigin;
        
        //Dummy wave settings
        private WaveForm[] waveForm = { WaveForm.SINE, WaveForm.SAWTOOTH };
        //waveLength = number of samples generated before trying to find trigger
        private const uint waveLength = 10 * outputWaveLength;
        private double samplePeriod = 20e-9; //ns --> sampleFreq of 50MHz by default
        private double amplitude = 10;
        private double frequency = 22e3;
        private double noiseAmplitude = 0.5; //Noise mean voltage
        private int usbLatency = 20; //milliseconds of latency to simulate USB request delay

        //Scope variables
        private const uint outputWaveLength = 2048;
        public const uint channels = 2;
        private float[] yOffset = new float[channels];
        private float triggerLevel = 0;
        private int triggerHoldoff = 0;
        private uint triggerChannel = 0;
        private static uint triggerWidth = 4;

        #region constructor / initializer 

        public ScopeDummy(EDevice d) : base(d) { }

        public override void InitializeDataSources()
        {
            dataSources.Add(new DataSources.DataSourceScope(this));
        }
        public override void InitializeHardwareInterface()
        {
            //Dummy has no hardware interface. So sad, living in a computer's memory
        }
        public override void InitializeMemories()
        {
            //Dummy has not memory. Yep, it's *that* dumb.
        }
        public override void Start() { timeOrigin = DateTime.Now; }
        public override void Stop() { }

        #endregion

        #region real scope settings

        private void validateChannel(uint ch)
        {
            if (ch >= channels)
                throw new ValidationException("Channel must be between 0 and " + (channels - 1));
        }

        public int GetTriggerHoldoff()
        {
            return this.triggerHoldoff;
        }
        public void SetTriggerHoldOff(int samples)
        {
            this.triggerHoldoff = samples;
        }
        public void SetTriggerLevel(float voltage)
        {
            this.triggerLevel = voltage;
            if(Math.Abs(this.triggerLevel) > Math.Abs(this.amplitude))
                Logger.AddEntry(this, LogMessageType.ECoreInfo, "Not gonna generate dummy waves since trigger level is larger than amplitude");
        }
        public void SetYOffset(uint channel, float offset)
        {
            validateChannel(channel);
            this.yOffset[channel] = offset;
        }
        public void SetTriggerChannel(uint channel)
        {
            validateChannel(channel);
            this.triggerChannel = channel;
        }

        #endregion

        #region dummy scope settings
        public void SetDummyWaveAmplitude(double amplitude)
        {
            this.amplitude = amplitude;
        }
        public void SetDummyWaveFrequency(double frequency)
        {
            this.frequency = frequency;
        }
        public void SetDummyWaveForm(uint channel, WaveForm w)
        {
            validateChannel(channel);
            this.waveForm[channel] = w;
        }
        public void SetNoiseAmplitude(double noiseAmplitude)
        {
            this.noiseAmplitude = noiseAmplitude;
        }
        #endregion

        public DataPackageScope GetScopeData()
        {
            //FIXME: support trigger channel selection
            if (!eDevice.IsRunning) 
                return null;
            //Sleep to simulate USB delay
            System.Threading.Thread.Sleep(usbLatency);
            int triggerIndex = 0;
            float[][] wave = new float[channels][];
            float[][] output = null;

            //Don't bother generating a wave if the trigger is larger than the amplitude
            if (Math.Abs(this.triggerLevel) > this.amplitude)
                return null;
            
            TimeSpan offset = DateTime.Now - timeOrigin;
            for (int i = 0; i < channels; i++)
            {
                wave[i] = ScopeDummy.GenerateWave(this.waveForm[i], waveLength,
                                this.samplePeriod,
                                offset.TotalSeconds,
                                this.frequency,
                                this.amplitude, 0,
                                this.yOffset[i]);
            }
            //output will remain null as long as a trigger with the current time offset is not found
            //We might get stuck here for a while if the trigger level is beyond the wave amplitude
            
            if (ScopeDummy.Trigger(wave[this.triggerChannel], triggerHoldoff, triggerLevel, out triggerIndex))
            {
                output = new float[channels][];
                for (int i = 0; i < channels; i++)
                {
                    output[i] = ScopeDummy.CropWave(outputWaveLength, wave[i], triggerIndex, triggerHoldoff);
                    ScopeDummy.AddNoise(output[i], this.noiseAmplitude);
                }
            }
            if (output == null)
                return null;

            DataPackageScope p = new DataPackageScope(samplePeriod, triggerIndex, triggerHoldoff);
            p.SetData(ScopeChannel.ChA, output[0]);
            p.SetData(ScopeChannel.ChB, output[1]);
            return p;
        }

    }
}
