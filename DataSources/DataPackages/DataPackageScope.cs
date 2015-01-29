using System;
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
        private Dictionary<AnalogChannel, float[]> acquisitionBufferOverviewAnalog;
        private Dictionary<AnalogChannel, float[]> viewportDataAnalog;
        private Dictionary<AnalogChannel, byte[]> viewportDataAnalogRaw;
        //FIXME: think through how to deal with the digital data. For now, it's better
        //to just pass it around as an 8-bit bus. But what if we have 10 channels? or 11? or 42?
        private byte[] acquisitionBufferOverviewDigital;
        private byte[] viewportDataDigital;
#if DEBUG
        internal SmartScopeHeader header = null;
#endif

        public Dictionary<string, double> Settings { get; private set; }

        internal DataPackageScope(
            uint acquiredSamples, double samplePeriod, 
            double viewportSamplePeriod, int viewportSamples, double viewportOffset,
            double holdoff, bool partial, bool rolling, int identifier, double viewportExcess = 0)
        {
            this.Processed = false;
            this.Identifier = identifier;
            this.AcquisitionSamples = acquiredSamples;
            this.AcquisitionSamplePeriod = samplePeriod;

            this.ViewportSamples = viewportSamples;
            this.ViewportSamplePeriod = viewportSamplePeriod;
            this.ViewportOffset = viewportOffset;
            this.ViewportExcess = viewportExcess;

            this.Holdoff = holdoff;
            this.Partial = partial;
            this.Rolling = rolling;

            acquisitionBufferOverviewAnalog = new Dictionary<AnalogChannel, float[]>();
            viewportDataAnalog = new Dictionary<AnalogChannel, float[]>();
            viewportDataAnalogRaw = new Dictionary<AnalogChannel, byte[]>();
            Settings = new Dictionary<string,double>();
        }

        //FIXME should be internal but currently used by averaging and inverting processors
        public void SetViewportData(AnalogChannel ch, float[] data)
        {
            if (data.Length == 0) return;
            viewportDataAnalog.Remove(ch);
            viewportDataAnalog.Add(ch, data);
        }

        public void SetViewportDataRaw(AnalogChannel ch, byte[] data)
        {
            if (data.Length == 0) return;
            viewportDataAnalogRaw.Remove(ch);
            viewportDataAnalogRaw.Add(ch, data);
        }

        internal void SetViewportDataDigital(byte[] data)
        {
            if (data.Length == 0) return;
            viewportDataDigital = data;
        }

        public void SetAcquisitionBufferOverviewData(AnalogChannel ch, float[] data)
        {
            if (data.Length == 0) return;
            acquisitionBufferOverviewAnalog.Remove(ch);
            acquisitionBufferOverviewAnalog.Add(ch, data);
        }

        internal void SetAcquisitionBufferOverviewDataDigital(byte[] data)
        {
            if (data.Length == 0) return;
            acquisitionBufferOverviewDigital = data;
        }

        internal void AddSetting(String setting, double value)
        {
            this.Settings.Add(setting, value);
        }

        //FIXME: should we perhaps return a copy of the array?
        public float[] GetViewportData(AnalogChannel ch)
        {
            float[] data = null;
            viewportDataAnalog.TryGetValue(ch, out data);
            return data;
        }

        public byte[] GetViewportDataRaw(AnalogChannel ch)
        {
            byte[] data = null;
            viewportDataAnalogRaw.TryGetValue(ch, out data);
            return data;
        }

        public byte[] GetViewportDataDigital()
        {
            return viewportDataDigital;
        }

        public bool[] GetViewportDataDigital(Channel ch, float? thresholdHigh = null, float? thresholdLow = null)
        {
            if (ch is AnalogChannel)
            {
                float[] analogData = GetViewportData(ch as AnalogChannel);
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
                byte[] bus = GetViewportDataDigital();
                if(bus == null) return null;

                Func<byte, bool> bitFilter = new Func<byte,bool>(x => Utils.IsBitSet(x, ch.Value));
                bool[] output = Utils.TransformArray(bus, bitFilter);
                return output;
            }
            return null;
        }

        public float[] GetOverviewBufferData(AnalogChannel ch)
        {
            float[] data = null;
            acquisitionBufferOverviewAnalog.TryGetValue(ch, out data);
            return data;
        }

        public byte[] GetOverviewBufferDataDigital()
        {
            return acquisitionBufferOverviewDigital;
        }

        /// <summary>
        /// Unique identifier for package
        /// </summary>
        public int Identifier { get; private set; }

        public double AcquisitionLength { get { return AcquisitionSamples * AcquisitionSamplePeriod; } }

        /// <summary>
        /// The number of samples acquired
        /// </summary>
        public uint AcquisitionSamples { get; private set; }

        /// <summary>
        /// Time between 2 consecutive data array elements. In seconds.
        /// </summary>
        public double AcquisitionSamplePeriod { get; private set; }

        /// <summary>
        /// The trigger holdoff in seconds
        /// </summary>
        public double Holdoff { get; set; }

        /// <summary>
        /// Indicates whether this is a partial acquisition package
        /// </summary>
        public bool Partial { get; internal set; }

        /// <summary>
        /// True when the scope is in rolling mode
        /// </summary>
        public bool Rolling { get; internal set; }

        /// <summary>
        /// The time between the first sample in the acquisition buffer and 
        /// the viewport's first sample
        /// </summary>
        public double ViewportOffset { get; internal set; }

        /// <summary>
        /// The time of excessive samples leading the viewport buffer
        /// </summary>
        public double ViewportExcess { get; internal set; }

        /// <summary>
        /// The time between samples of the viewport
        /// </summary>
        public double ViewportSamplePeriod { get; internal set; }

        /// <summary>
        /// The number of samples stored per channel
        /// </summary>
        //FIXME: this should be a private set, but is internal since a FIXME in SmartScope.GetScopeData()
        public int ViewportSamples { get; set; }

        /// <summary>
        /// Flag which can be used by external code to indicate this DataPackage has been processed
        /// already. This is useful for the case where the same datapackage is modified, i.e. an overview
        /// buffer was added, and sent to the UI again.
        /// </summary>
        public bool Processed { get; set; }
    }
}
