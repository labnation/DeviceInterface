using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.DeviceImplementations;

namespace ECore.DataPackages
{
    //FIXME: this might not be the best place to define all possible channels -> differ between actual and computed channels?
    public enum ScopeChannel { ChA, ChB, Digi0, Digi1, Digi2, Digi3, Digi4, Digi5, Digi6, Digi7, Math, FftA, FftB, I2CDecoder, Undefined };

    public class DataPackageScope
    {
        private int triggerIndex;
        private double samplePeriod;
        private Dictionary<ScopeChannel, float[]> dataAnalog;
        private Dictionary<ScopeChannel, bool[]> dataDigital;

        private static Type ScopeChannelType(ScopeChannel ch)
        {
            switch (ch)
            {
                case ScopeChannel.ChA:
                case ScopeChannel.ChB:
                case ScopeChannel.Math:
                    return typeof(float);
                case ScopeChannel.Digi0:
                case ScopeChannel.Digi1:
                case ScopeChannel.Digi2:
                case ScopeChannel.Digi3:
                case ScopeChannel.Digi4:
                case ScopeChannel.Digi5:
                case ScopeChannel.Digi6:
                case ScopeChannel.Digi7:
                    return typeof(bool);
                default:
                    throw new Exception("Unknown type for scope channel");
            }
        }

        public DataPackageScope(double samplePeriod, int triggerIndex)
        {
            this.triggerIndex = triggerIndex;
            this.samplePeriod = samplePeriod;
            dataAnalog = new Dictionary<ScopeChannel, float[]>();
            dataDigital = new Dictionary<ScopeChannel, bool[]>();
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
            dataAnalog.Add(ScopeChannel.ChA, chA);
            dataAnalog.Add(ScopeChannel.ChB, chB);
        }
        private void ValidateChannelDataType(ScopeChannel ch, object data)
        {
            if(!ScopeChannelType(ch).Equals(data.GetType()))
                throw new Exception("data type mismatch while setting scope channel data");
        }
        public void SetData<T>(ScopeChannel ch, T[] data)
        {
            ValidateChannelDataType(ch, data[0]);
            if(data[0].GetType().Equals(typeof(float))) {
                dataAnalog.Remove(ch);
                dataAnalog.Add(ch, data as float[]);
            } 
            else if(data[0].GetType().Equals(typeof(bool))) {
                dataDigital.Remove(ch);
                dataDigital.Add(ch, data as bool[]);
            }
        }
        //FIXME: should we perhaps return a copy of the array?
        public T[] GetData<T>(ScopeChannel ch)
        {
            T[] data = null;
            if(typeof(T).Equals(typeof(float))) {
                float[] d;
                dataAnalog.TryGetValue(ch, out d);
                data = d as T[];
            }
            else if(typeof(T).Equals(typeof(bool))) {
                bool[] d;
                dataDigital.TryGetValue(ch, out d);
                data = d as T[];
            }
            if (data == null) return null;
            T[] dataCopy = new T[data.Length];
            data.CopyTo(dataCopy, 0);
            return dataCopy;
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
