﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.Devices;
using Common;

namespace ECore.DataSources
{
    /// <summary>
    /// The object containing a scope acquisition's data.
    /// </summary>
    public class DataPackageScope
    {
        private Dictionary<AnalogChannel, float[]> dataAnalog;
        private Dictionary<AnalogChannel, byte[]> dataAnalogRaw;
        //FIXME: think through how to deal with the digital data. For now, it's better
        //to just pass it around as an 8-bit bus. But what if we have 10 channels? or 11? or 42?
        private byte[] dataDigital;
        public Dictionary<string, double> Settings { get; private set; }

        internal DataPackageScope(double samplePeriod, int samples, double holdoff, bool partial, bool rolling)
        {
            this.SamplePeriod = samplePeriod;
            this.Samples = samples;
            this.Holdoff = holdoff;
            this.Partial = partial;
            this.Rolling = rolling;
            dataAnalog = new Dictionary<AnalogChannel, float[]>();
            dataAnalogRaw = new Dictionary<AnalogChannel, byte[]>();
            Settings = new Dictionary<string,double>();
        }

        //FIXME should be internal but currently used by averaging and inverting processors
        public void SetData(AnalogChannel ch, float[] data)
        {
            dataAnalog.Remove(ch);
            dataAnalog.Add(ch, data);
        }

        public void SetDataRaw(AnalogChannel ch, byte[] data)
        {
            dataAnalogRaw.Remove(ch);
            dataAnalogRaw.Add(ch, data);
        }

        internal void SetDataDigital(byte[] data)
        {
            dataDigital = data;
        }

        internal void AddSetting(String setting, double value)
        {
            this.Settings.Add(setting, value);
        }

        //FIXME: should we perhaps return a copy of the array?
        public float[] GetData(AnalogChannel ch)
        {
            float[] data = null;
            dataAnalog.TryGetValue(ch, out data);
            return data;
        }

        public byte[] GetDataRaw(AnalogChannel ch)
        {
            byte[] data = null;
            dataAnalogRaw.TryGetValue(ch, out data);
            return data;
        }

        public byte[] GetDataDigital()
        {
            return dataDigital;
        }

        public bool[] GetDataDigital(Channel ch, float? thresholdHigh = null, float? thresholdLow = null)
        {
            if (ch is AnalogChannel)
            {
                float[] analogData = GetData(ch as AnalogChannel);
                if (analogData == null) return null;

                float H = thresholdHigh.HasValue ? thresholdHigh.Value : analogData.Min() + (analogData.Max() - analogData.Min()) * 0.7f;
                float L = thresholdLow.HasValue ? thresholdLow.Value : analogData.Min() + (analogData.Max() - analogData.Min()) * 0.3f;

                bool[] digitalData = new bool[analogData.Length];
                bool digitalDataPrevious = false;
                for (int i = 0; i < analogData.Length; i++)
                    digitalDataPrevious = digitalData[i] = Utils.Schmitt(analogData[i], digitalDataPrevious, H, L);
                return digitalData;
            }
            else if (ch is DigitalChannel)
            {
                byte[] bus = GetDataDigital();
                if(bus == null) return null;

                Func<byte, bool> bitFilter = new Func<byte,bool>(x => Utils.IsBitSet(x, ch.Value));
                bool[] output = Utils.TransformArray(bus, bitFilter);
                return output;
            }
            return null;
        }

        /// <summary>
        /// Time between 2 consecutive data array elements. In seconds.
        /// </summary>
        public double SamplePeriod { get; private set; }

        /// <summary>
        /// The trigger holdoff in seconds
        /// </summary>
        public double Holdoff { get; set; }

        /// <summary>
        /// The number of samples stored per channel
        /// </summary>
        //FIXME: this should be a private set, but is internal since a FIXME in SmartScope.GetScopeData()
        public int Samples { get; set; }

        /// <summary>
        /// Indicates whether this is a partial acquisition package
        /// </summary>
        public bool Partial { get; internal set; }

        /// <summary>
        /// True when the scope is in rolling mode
        /// </summary>
        public bool Rolling { get; internal set; }
    }
}
