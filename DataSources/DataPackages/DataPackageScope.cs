using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.DeviceImplementations;

namespace ECore.DataPackages
{
    public enum ScopeChannel { ChA, ChB, Digi0, Digi1, Digi2, Digi3, Digi4, Digi5, Digi6, Digi7, Math, FftA, FftB, I2CDecoder };

    public class DataPackageScope
    {
        private uint triggerIndex;
        private Dictionary<ScopeChannel, float[]> dataAnalog;
        private Dictionary<ScopeChannel, bool[]> dataDigital;

        private static Type ScopeChannelType(ScopeChannel ch)
        {
            switch (ch)
            {
                case ScopeChannel.ChA:
                case ScopeChannel.ChB:
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

        public DataPackageScope()
        {
            dataAnalog = new Dictionary<ScopeChannel, float[]>();
            dataDigital = new Dictionary<ScopeChannel, bool[]>();
        }
        //FIXME: this constructor shouldn't be necessary, all data should be set using Set()
        public DataPackageScope(float[] voltages)
            : this()
        {
            dataAnalog.Add(ScopeChannel.ChA, voltages);
        }
        private void CheckChannelDataType(ScopeChannel ch, object data)
        {
            if(!ScopeChannelType(ch).Equals(data.GetType()))
                throw new Exception("data type mismatch while setting scope channel data");
        }
        public void SetDataAnalog(ScopeChannel ch, float[] data)
        {
            CheckChannelDataType(ch, data[0]);
            dataAnalog.Add(ch, data);
        }
        public void SetDataDigital(ScopeChannel ch, bool[] data)
        {
            CheckChannelDataType(ch, data[0]);
            dataDigital.Add(ch, data);
        }
        public float[] GetDataAnalog(ScopeChannel ch)
        {
            float[] data = null;
            dataAnalog.TryGetValue(ch, out data);
            return data;
        }
        public bool[] GetDataDigital(ScopeChannel ch)
        {
            bool[] data = null;
            dataDigital.TryGetValue(ch, out data);
            return data;
        }
    }
}
