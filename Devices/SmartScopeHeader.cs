using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.Memories;
using LabNation.Common;

namespace LabNation.DeviceInterface.Devices
{
    internal class SmartScopeHeader
    {
        private byte[] raw;
        /// <summary>
        /// The number of bursts of [BytesPerBurst] bytes in this package
        /// </summary>
        internal int NumberOfPayloadBursts { get; private set; }
        /// <summary>
        /// The offset, in bursts, of this package's payload in the
        /// entire acquisition
        /// </summary>
        internal int PackageOffset { get; private set; }

        /// <summary>
        /// The number of bytes in 1 payload burst
        /// </summary>
        internal byte BytesPerBurst { get; private set; }

        /// <summary>
        /// The number of samples acquired
        /// </summary>
        internal uint AcquisitionDepth { get; private set; }

        /// <summary>
        /// The number of samples in this package
        /// </summary>
        internal int Samples { get; private set; }
        /// <summary>
        /// The time between two samples
        /// </summary>
        internal double SamplePeriod { get; private set; }

        /// <summary>
        /// The time between two samples of the viewport
        /// </summary>
        internal double ViewportSamplePeriod { get; private set; }

        /// <summary>
        /// The time between the start of the acquisition and the viewport's first sample
        /// </summary>
        internal double ViewportOffset { get; private set; }

        /// <summary>
        /// The time of excessive samples leading the viewport buffer
        /// </summary>
        internal double ViewportExcess { get; private set; }
        /// <summary>
        /// The trigger holdoff in seconds
        /// </summary>
        internal double TriggerHoldoff { get; private set; }

        /// <summary>
        /// The total number of samples in this viewport
        /// </summary>
        internal int ViewportLength { get; private set; }

        /// <summary>
        /// Wether this is the last package in the acquisition
        /// </summary>
        internal bool OverviewBuffer { get; private set; }

        /// <summary>
        /// True when acquisition is ongoing
        /// </summary>
        internal bool Acquiring { get; private set; }
        /// <summary>
        /// True when the holdoff is satisfied
        /// </summary>
        internal bool Armed { get; private set; }
        /// <summary>
        /// When true, no new acquisition will be started when this
        /// one has finished
        /// </summary>
        internal bool LastAcquisition { get; private set; }
        /// <summary>
        /// When true, the data is undecimated RAM samples.
        /// PackageOffset and Samples indicate the offset (in
        /// multiples of 2048 samples) and number of samples
        /// (should always be 2048)
        /// </summary>
        internal bool FullAcquisitionDump { get; private set; }
        /// <summary>
        /// True when a trigger needs to come in. Might be true
        /// while holdoff is not completed yet
        /// </summary>
        internal bool AwaitingTrigger { get; private set; }
        /// <summary>
        /// Wether the acquisition is in rolling mode
        /// </summary>
        internal bool Rolling { get; private set; }
        /// <summary>
        /// True when dump settings are impossible to be met
        /// </summary>
        internal bool ImpossibleDump{ get; private set; }

        /// <summary>
        /// True when dump was due to timeout
        /// </summary>
        internal bool TimedOut { get; private set; }

        internal bool LogicAnalyserEnabled { get; private set; }
        internal AnalogChannel ChannelSacrificedForLogicAnalyser { get; private set; }
        public AnalogTriggerValue AnalogTrigger { get; private set; }
        internal TriggerModes TriggerMode
        {
            get
            {
                if (LogicAnalyserEnabled && AnalogTrigger.channel.Equals(ChannelSacrificedForLogicAnalyser))
                    return TriggerModes.Digital;
                return TriggerModes.Analog;

            }
        }

        internal readonly int Channels = 2;
        internal int AcquisitionId { get; private set; }
        
        //FIXME: we really shouldn't be needing the freqcomp mode in here
        internal SmartScopeHeader(byte[] data)
        {
            int headerSize = SmartScope.AcquisitionRegisters.Length + SmartScope.DumpRegisters.Length + (int)Math.Ceiling(SmartScope.AcquisitionStrobes.Length / 8.0);
            raw = new byte[headerSize];
            if (data[0] != 'L' || data[1] != 'N')
                throw new Exception("Invalid magic number, can't parse header");

            int headerOffset = data[2];
            Array.Copy(data, headerOffset, raw, 0, headerSize);
            
            BytesPerBurst = data[3];
            NumberOfPayloadBursts = data[4] + (data[5] << 8);
            
            PackageOffset = (short)(data[6] + (data[7] << 8));

            ViewportLength = (int)(BytesPerBurst / Channels) << GetRegister(REG.VIEW_BURSTS);

            AcquisitionDepth = (uint)(2048 << GetRegister(REG.ACQUISITION_DEPTH));
            Samples = NumberOfPayloadBursts * BytesPerBurst / Channels;
            Acquiring       = Utils.IsBitSet(data[10], 0);
            OverviewBuffer  = Utils.IsBitSet(data[10], 1);
            LastAcquisition = Utils.IsBitSet(data[10], 2);
            Rolling         = Utils.IsBitSet(data[10], 3);
            TimedOut        = Utils.IsBitSet(data[10], 4);
            AwaitingTrigger = Utils.IsBitSet(data[10], 5);
            Armed           = Utils.IsBitSet(data[10], 6);
            FullAcquisitionDump = Utils.IsBitSet(data[10], 7);

            AnalogTrigger = new AnalogTriggerValue()
            {
                channel = AnalogChannel.List.First(x => x.Value ==  ((GetRegister(REG.TRIGGER_MODE) >> 2) & 0x03)),
                direction = (TriggerDirection)((GetRegister(REG.TRIGGER_MODE) >> 4) & 0x03),
            };
            ChannelSacrificedForLogicAnalyser = GetStrobe(STR.LA_CHANNEL) ? AnalogChannel.ChB : AnalogChannel.ChA;
            LogicAnalyserEnabled = GetStrobe(STR.LA_ENABLE);
            
            AcquisitionId = data[11];
            SamplePeriod = SmartScope.BASE_SAMPLE_PERIOD * Math.Pow(2, GetRegister(REG.INPUT_DECIMATION));
            ViewportSamplePeriod = SmartScope.BASE_SAMPLE_PERIOD * Math.Pow(2, GetRegister(REG.INPUT_DECIMATION) + GetRegister(REG.VIEW_DECIMATION));

            ViewportOffset = SamplePeriod * (
                GetRegister(REG.VIEW_OFFSET_B0) +
                (GetRegister(REG.VIEW_OFFSET_B1) << 8) +
                (GetRegister(REG.VIEW_OFFSET_B2) << 16)
                );

            int viewportExcessiveSamples = GetRegister(REG.VIEW_EXCESS_B0) + (GetRegister(REG.VIEW_EXCESS_B1) << 8);
            ViewportExcess = viewportExcessiveSamples * SamplePeriod;

            Int64 holdoffSamples = GetRegister(REG.TRIGGERHOLDOFF_B0) +
                                    (GetRegister(REG.TRIGGERHOLDOFF_B1) << 8) +
                                    (GetRegister(REG.TRIGGERHOLDOFF_B2) << 16) +
                                    (GetRegister(REG.TRIGGERHOLDOFF_B3) << 24) - SmartScope.TriggerDelay(TriggerMode, GetRegister(REG.TRIGGER_WIDTH), GetRegister(REG.INPUT_DECIMATION));
            TriggerHoldoff = holdoffSamples * (SmartScope.BASE_SAMPLE_PERIOD * Math.Pow(2, GetRegister(REG.INPUT_DECIMATION)));
        }

        internal byte GetRegister(REG r)
        {
            int offset = 0;
            if (!SmartScope.AcquisitionRegisters.Contains(r))
            {
                if (!SmartScope.DumpRegisters.Contains(r))
                    throw new Exception("Register " + r.ToString("G") + " not part of header");
                else
                    offset = SmartScope.AcquisitionRegisters.Length + Array.IndexOf(SmartScope.DumpRegisters, r); ;
            }
            else
            {
                offset = Array.IndexOf(SmartScope.AcquisitionRegisters, r);
            }

            return raw[offset]; 
        }

        internal bool GetStrobe(STR s)
        {
            if (!SmartScope.AcquisitionStrobes.Contains(s))
                throw new Exception("Strobe  " + s.ToString("G") + " not part of header");
            int offset = SmartScope.AcquisitionRegisters.Length + SmartScope.DumpRegisters.Length;
            offset += Array.IndexOf(SmartScope.AcquisitionStrobes, s) / 8;
            return Utils.IsBitSet(raw[offset], (int)Array.IndexOf(SmartScope.AcquisitionStrobes, s) % 8);
        }
    }

    internal static class SmartScopeHeaderHelpers
    {

        public static Dictionary<AnalogChannel, SmartScope.GainCalibration> ChannelSettings(this SmartScopeHeader h, SmartScope.Rom r)
        {
            Dictionary<AnalogChannel, SmartScope.GainCalibration> settings = new Dictionary<AnalogChannel, SmartScope.GainCalibration>();

            foreach (AnalogChannel ch in AnalogChannel.List)
            {
                //Parse div_mul
                byte divMul = h.GetRegister(REG.DIVIDER_MULTIPLIER);
                int chOffset = ch.Value * 4;
                double div = SmartScope.validDividers[(divMul >> (0 + chOffset)) & 0x3];
                double mul = SmartScope.validMultipliers[(divMul >> (2 + chOffset)) & 0x3];

                settings.Add(ch, r.getCalibration(ch, div, mul));
            }
            return settings;
        }
    }
}
