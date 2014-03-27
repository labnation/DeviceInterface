using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.DataPackages;

namespace ECore.DeviceImplementations
{
    public enum WaveSource { FILE, GENERATOR }

    public partial class ScopeDummy : EDeviceImplementation, IScope
    {
        private DateTime timeOrigin;

        //Wave settings
        private WaveSource waveSource = WaveSource.FILE;
        private WaveForm[] waveForm = { WaveForm.SINE, WaveForm.SAWTOOTH_SINE };
        private double[] amplitude = new double[] {1.3, 1.8};
        private double[] dcOffset = new double[] { 0.0f, -0.9f };
        private double[] frequency = new double[] { 200e3, 600e3 };
        private double[] noiseAmplitude = new double[] { 0.1, 0.1 }; //Noise mean voltage
        private int usbLatency = 23; //milliseconds of latency to simulate USB request delay
        private float[] yOffset = new float[] { 0, 0 };

        //Scope variables
        private const uint waveLength = 2 * outputWaveLength;
        private double samplePeriodMinimum = 10e-9; //ns --> sampleFreq of 100MHz by default
        private double SamplePeriod { get { return samplePeriodMinimum * decimation; } }
        public const uint channels = 2;
        private const uint outputWaveLength = 2048;
        private float triggerLevel = 0;
        private double triggerHoldoff = 0;
        private uint triggerChannel = 0;
        private static uint triggerWidth = 4;
        private uint decimation = 1;
        private TriggerDirection triggerDirection = TriggerDirection.FALLING;

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
        private void validateDecimation(uint decimation)
        {
            if (decimation < 1)
                throw new ValidationException("Decimation must be larger than 0");
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
        public void SetTriggerDirection(TriggerDirection direction)
        {
            this.triggerDirection = direction;
        }
        public void SetDecimation(uint decimation)
        {
            validateDecimation(decimation);
            this.decimation = decimation;
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

        private static bool Trigger(float[] wave, TriggerDirection direction, int holdoff, float level, uint outputWaveLength, out int triggerIndex)
        {
            //Hold off:
            // - if positive, start looking for trigger at that index, so we are sure to have that many samples before the trigger
            // - if negative, start looking at index 0, but add abs(holdoff) to returned index
            triggerIndex = 0;
            for (int i = Math.Max(0, holdoff); i < wave.Length - triggerWidth - outputWaveLength; i++)
            {
                float invertor = (direction == TriggerDirection.RISING) ? 1f : -1f;
                if (invertor * wave[i] < invertor * level && invertor * wave[i + triggerWidth] > invertor * level)
                {
                    triggerIndex = (int)(i + triggerWidth / 2);
                    return true;
                }
            }
            return false;
        }

        public DataPackageScope GetScopeData()
        {
            //FIXME: support trigger channel selection
            if (!eDevice.IsRunning) 
                return null;
            //Sleep to simulate USB delay
            System.Threading.Thread.Sleep(usbLatency);
            float[][] output = null;
            int triggerIndex = 0;
            int triggerHoldoffInSamples = 0;

            if(waveSource == WaveSource.GENERATOR)
            {
                float[][] wave = new float[channels][];
                //Don't bother generating a wave if the trigger is larger than the amplitude
                //if (Math.Abs(triggerLevel) > amplitude[triggerChannel] + dcOffset[triggerChannel] + noiseAmplitude[triggerChannel])
                //    return null;
            
                TimeSpan timeOffset = DateTime.Now - timeOrigin;
                for (int i = 0; i < channels; i++)
                {
                    wave[i] = ScopeDummy.GenerateWave(waveForm[i], waveLength,
                                    SamplePeriod,
                                    timeOffset.TotalSeconds,
                                    frequency[i],
                                    amplitude[i], 0, dcOffset[i]);
                    ScopeDummy.AddNoise(wave[i], noiseAmplitude[i]);
                }
                triggerHoldoffInSamples = (int)(triggerHoldoff / SamplePeriod);
                if (ScopeDummy.Trigger(wave[triggerChannel], triggerDirection, triggerHoldoffInSamples, triggerLevel, outputWaveLength, out triggerIndex))
                {
                    output = new float[channels][];
                    for (int i = 0; i < channels; i++)
                    {
                        output[i] = ScopeDummy.CropWave(outputWaveLength, wave[i], triggerIndex, triggerHoldoffInSamples);     
                    }
                }
                if (output == null)
                    return null;
            }
            else if (waveSource == WaveSource.FILE)
            {

                if (!GetWaveFromFile(triggerHoldoff, triggerChannel, triggerDirection, triggerLevel, decimation, SamplePeriod, ref output)) return null;
                for(int i = 0; i < channels; i++)
                    ScopeDummy.AddNoise(output[i], noiseAmplitude[i]);
                triggerHoldoffInSamples = (int)(triggerHoldoff / SamplePeriod);
            }

            DataPackageScope p = new DataPackageScope(SamplePeriod, triggerHoldoffInSamples);
            p.SetData(ScopeChannel.ChA, output[0]);
            p.SetData(ScopeChannel.ChB, output[1]);
            p.SetOffset(ScopeChannel.ChA, yOffset[0]);
            p.SetOffset(ScopeChannel.ChB, yOffset[1]);
            return p;
        }

    }
}
