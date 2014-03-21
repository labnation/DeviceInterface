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
        private WaveForm waveForm = WaveForm.SINE;
        private const uint outputWaveLength = 2048;
        //waveLength = samples generated before trying to find trigger
        private const uint waveLength = 10 * outputWaveLength;
        private double samplePeriod = 20e-9; //ns --> sampleFreq of 50MHz by default
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
        }

        public override DataPackageScope GetScopeData()
        {
            int triggerIndex = -1;
            float[] wave = null;
            float[] output = null;
            while (output == null)
            {
                TimeSpan offset = DateTime.Now - timeOrigin;
                wave = ScopeDummy.GenerateWave(this.waveForm, waveLength, this.samplePeriod, offset.TotalSeconds, 5e6, 1, 0);
                if(ScopeDummy.Trigger(wave, triggerHoldoff, triggerLevel, out triggerIndex))
                    output = ScopeDummy.Something(outputWaveLength, wave, triggerIndex, triggerHoldoff);
            }
            DataPackageScope p = new DataPackageScope();
            p.SetDataAnalog(ScopeChannel.ChA, output);
            p.SetDataAnalog(ScopeChannel.ChB, output);
            return p;
        }

    }
}
