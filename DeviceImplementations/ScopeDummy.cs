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

        //Wave settings
        private WaveForm[] waveForm = { WaveForm.SINE, WaveForm.SAWTOOTH };
        private double[] amplitude = new double[] {1.3, 1.8};
        private double[] dcOffset = new double[] { 0.9f, 0f };
        private double[] frequency = new double[] {400e3, 100e3};
        private double[] noiseAmplitude = new double[] { 0.05, 0.1 }; //Noise mean voltage
        private int usbLatency = 23; //milliseconds of latency to simulate USB request delay
        private float[] yOffset = new float[] { 0, 0 };

        //Scope variables
        private const uint waveLength = 2 * outputWaveLength;
        private double samplePeriod = 10e-9; //ns --> sampleFreq of 100MHz by default
        public const uint channels = 2;
        private const uint outputWaveLength = 2048;
        private float triggerLevel = 0;
        private double triggerHoldoff = 0;
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

        public void SetTriggerHoldOff(double holdoff)
        {
            this.triggerHoldoff = holdoff;
        }
        public void SetTriggerLevel(float voltage)
        {
            this.triggerLevel = voltage;
            if (Math.Abs(triggerLevel) > Math.Abs(amplitude[triggerChannel] + dcOffset[triggerChannel] + noiseAmplitude[triggerChannel]))
                Logger.AddEntry(this, LogMessageType.ECoreInfo, "Not gonna generate dummy waves since trigger level is larger than amplitude");
        }
        public void SetYOffset(uint channel, float yOffset)
        {
            validateChannel(channel);
            this.yOffset[channel] = yOffset;
        }
        public void SetTriggerChannel(uint channel)
        {
            validateChannel(channel);
            this.triggerChannel = channel;
        }

        #endregion

        #region dummy scope settings
        public void SetDummyWaveAmplitude(uint channel, double amplitude)
        {
            validateChannel(channel);
            this.amplitude[channel] = amplitude;
        }
        public void SetDummyWaveFrequency(uint channel, double frequency)
        {
            validateChannel(channel);
            this.frequency[channel] = frequency;
        }
        public void SetDummyWaveForm(uint channel, WaveForm w)
        {
            validateChannel(channel);
            this.waveForm[channel] = w;
        }
        public void SetNoiseAmplitude(uint channel, double noiseAmplitude)
        {
            validateChannel(channel);
            this.noiseAmplitude[channel] = noiseAmplitude;
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
            if (Math.Abs(triggerLevel) > amplitude[triggerChannel] + dcOffset[triggerChannel] + noiseAmplitude[triggerChannel])
                return null;
            
            TimeSpan timeOffset = DateTime.Now - timeOrigin;
            for (int i = 0; i < channels; i++)
            {
                wave[i] = ScopeDummy.GenerateWave(waveForm[i], waveLength,
                                samplePeriod,
                                timeOffset.TotalSeconds,
                                frequency[i],
                                amplitude[i], 0, dcOffset[i]);
                ScopeDummy.AddNoise(wave[i], noiseAmplitude[i]);
            }
            int triggerHoldoffInSamples = (int)(triggerHoldoff / samplePeriod);
            if (ScopeDummy.Trigger(wave[triggerChannel], triggerHoldoffInSamples, triggerLevel, outputWaveLength, out triggerIndex))
            {
                output = new float[channels][];
                for (int i = 0; i < channels; i++)
                {
                    output[i] = ScopeDummy.CropWave(outputWaveLength, wave[i], triggerIndex, triggerHoldoffInSamples);     
                }
            }
            if (output == null)
                return null;

            DataPackageScope p = new DataPackageScope(samplePeriod, triggerHoldoffInSamples);
            p.SetData(ScopeChannel.ChA, output[0]);
            p.SetData(ScopeChannel.ChB, output[1]);
            p.SetOffset(ScopeChannel.ChA, yOffset[0]);
            p.SetOffset(ScopeChannel.ChB, yOffset[1]);
            return p;
        }

    }
}
