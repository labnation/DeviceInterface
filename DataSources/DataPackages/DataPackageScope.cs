using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.DeviceImplementations;

namespace ECore.DataPackages
{
    public class DataPackageScope
    {
        private int triggerIndex;
        private double samplePeriod;
        private Dictionary<AnalogChannel, float[]> dataAnalog;
        private Dictionary<DigitalChannel, bool[]> dataDigital;
        private Dictionary<AnalogChannel, float> yOffset;

        public DataPackageScope(double samplePeriod, int triggerIndex)
        {
            this.triggerIndex = triggerIndex;
            this.samplePeriod = samplePeriod;
            dataAnalog = new Dictionary<AnalogChannel, float[]>();
            yOffset = new Dictionary<AnalogChannel, float>();

            dataDigital = new Dictionary<DigitalChannel, bool[]>();
        }
        //FIXME: this constructor shouldn't be necessary, all data should be set using Set()
        //It's just here to support "legacy" code
        public DataPackageScope(float[] buffer)
            : this(20e-9, 0)
        {
            float[] chA = new float[buffer.Length / 2];
            float[] chB = new float[buffer.Length / 2];
            for (int i = 0; i < chA.Length; i++)
            {
                chA[i] = buffer[i];
                chB[i] = buffer[buffer.Length / 2 + i];
            }
            dataAnalog.Add(ScopeChannels.ChA, chA);
            dataAnalog.Add(ScopeChannels.ChB, chB);
        }

        public void SetData(AnalogChannel ch, float[] data)
        {
            dataAnalog.Remove(ch);
            dataAnalog.Add(ch, data);
        }
        public void SetData(DigitalChannel ch, bool[] data)
        {
            dataDigital.Remove(ch);
            dataDigital.Add(ch, data);
        }
        public void SetOffset(AnalogChannel ch, float offset)
        {
            this.yOffset.Add(ch, offset);
        }
        public float GetOffset(AnalogChannel ch)
        {
            float offset = 0f;
            this.yOffset.TryGetValue(ch, out offset);
            return offset;
        }
        //FIXME: should we perhaps return a copy of the array?
        public float[] GetData(AnalogChannel ch)
        {
            float[] data = null;
            dataAnalog.TryGetValue(ch, out data);
            return data;
        }
        public bool[] GetData(DigitalChannel ch)
        {
            bool[] data = null;
            dataDigital.TryGetValue(ch, out data);
            return data;
        }
        /// <summary>
        /// Index at which the data was triggered. WARN: This index is not necessarily within the 
        /// data array's bounds, depending on what trigger holdoff was used
        /// </remarks>
        public int TriggerIndex { get { return this.triggerIndex; } }
        /// <summary>
        /// Time between 2 consecutive data array elements. In seconds.
        /// </summary>
        public double SamplePeriod { get { return this.samplePeriod; } }
    }
}
