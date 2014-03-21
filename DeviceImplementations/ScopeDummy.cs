using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.DataPackages;

namespace ECore.DeviceImplementations
{
    public partial class ScopeDummy : Scope
    {
        private DateTime timeOrigin;
        private WaveForm waveForm = WaveForm.TRIANGLE;
        private int usbLatency = 5; //milliseconds of latency to simulate USB request delay
        private const uint outputWaveLength = 2048;
        //waveLength = samples generated before trying to find trigger
        private const uint waveLength = 10 * outputWaveLength;
        private double samplePeriod = 20e-9; //ns --> sampleFreq of 50MHz by default
        private double amplitude = 1.5;
        private double frequency = 24.4e3;
        private float triggerLevel = 0;
        private int triggerHoldoff = 0;

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

        public override int GetTriggerHoldoff()
        {
            return this.triggerHoldoff;
        }
        public override void SetTriggerHoldOff(int samples)
        {
            this.triggerHoldoff = samples;
        }
        public override void SetTriggerLevel(float voltage)
        {
            this.triggerLevel = voltage;
            if(Math.Abs(this.triggerLevel) > Math.Abs(this.amplitude))
                Logger.AddEntry(this, LogMessageType.ECoreInfo, "Not gonna generate dummy waves since trigger level is larger than amplitude");
        }
        public override float GetTriggerLevel()
        {
            return 1.0f;
        }
        public void SetDummyWaveAmplitude(double amplitude)
        {
            this.amplitude = amplitude;
        }
        public void SetDummyWaveFrequency(double frequency)
        {
            this.frequency = frequency;
        }
        public void SetDummyWaveForm(WaveForm w)
        {
            this.waveForm = w;
        }
        public override DataPackageScope GetScopeData()
        {
            if (!eDevice.IsRunning) 
                return null;
            //Sleep to simulate USB delay
            System.Threading.Thread.Sleep(usbLatency);
            int triggerIndex = 0;
            float[] wave = null;
            float[] output = null;

            //Don't bother generating a wave if the trigger is larger than the amplitude
            if (Math.Abs(this.triggerLevel) > this.amplitude)
                return null;
            //output will remain null as long as a trigger with the current time offset is not found
            //We might get stuck here for a while if the trigger level is beyond the wave amplitude
            TimeSpan offset = DateTime.Now - timeOrigin;
            for(int tries = 0; tries < 3; tries++)
            {
                wave = ScopeDummy.GenerateWave(this.waveForm, waveLength, this.samplePeriod, offset.TotalSeconds, this.frequency, this.amplitude, 0);
                if (ScopeDummy.Trigger(wave, triggerHoldoff, triggerLevel, out triggerIndex))
                {
                    output = ScopeDummy.CropWave(outputWaveLength, wave, triggerIndex, triggerHoldoff);
                    break;
                }
                //If no trigger found, do it again with half the time window further
                offset.Add(new TimeSpan((long)(10e7 * (double)waveLength / 2.0 * samplePeriod)));
            }
            if (output == null)
                return null;

            DataPackageScope p = new DataPackageScope(samplePeriod, triggerIndex);
            p.SetDataAnalog(ScopeChannel.ChA, output);
            p.SetDataAnalog(ScopeChannel.ChB, output);
            return p;
        }

    }
}
