using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.Memories;
using System.IO;
using LabNation.DeviceInterface.DataSources;
using LabNation.DeviceInterface.Hardware;
using LabNation.Common;
using AForge.Math;
using System.Threading.Tasks;
using System.Threading;
#if ANDROID
using Android.Content;
#endif

namespace LabNation.DeviceInterface.Devices
{
    public partial class SmartScope4Channel : IScope, IDisposable
    {
        public event AcquisitionTransferFinishedHandler OnAcquisitionTransferFinished;
        public enum Position { First, Second };
#if DEBUG
        public
#else
        private
#endif
        Dictionary<Position, SmartScope> smartScopes;
        List<SmartScope> ss { get { return smartScopes.Values.ToList(); } }
        SmartScope first { get { return smartScopes[Position.First]; } }
        SmartScope second { get { return smartScopes[Position.Second]; } }

        private IScope ScopeOf(AnalogChannel ch)
        {
            if (ch.Order < 2) return first;
            return second;
        }
        private AnalogChannel ChMod2(AnalogChannel ch)
        {
            return 
                ch == AnalogChannel.ChD ? AnalogChannel.ChB :
                ch == AnalogChannel.ChC ? AnalogChannel.ChA :
                ch;
        }
        List<AnalogChannel> availableChannels = new List<AnalogChannel>() { AnalogChannel.ChA, AnalogChannel.ChB, AnalogChannel.ChC, AnalogChannel.ChD };
        public List<AnalogChannel> AvailableChannels { get { return availableChannels; } }

        #if ANDROID
        Context context;
        #endif

        public string Serial
        {
            get
            {
                return "1: " + smartScopes[Position.First].Serial + "\n2: " + smartScopes[Position.Second].Serial;
            }
        }

        internal SmartScope4Channel(ISmartScopeUsbInterface usbInterfaceFirst, ISmartScopeUsbInterface usbInterfaceSecond) : base()
        {
            smartScopes = new Dictionary<Position,SmartScope>();
            smartScopes.Add(Position.First, new SmartScope(usbInterfaceFirst));
            smartScopes.Add(Position.Second, new SmartScope(usbInterfaceSecond));
            this.dataSourceScope = new DataSource(this);
        }

        private bool paused = false;
        public void Pause() 
        {
            foreach(SmartScope s in ss)
                s.Pause();
            paused = true;
        }

        public void Resume() 
        {
            if(!paused) {
                Logger.Warn("Not resuming scope since it wasn't paused");
            }
            paused = false;
            foreach(SmartScope s in ss)
                s.Resume();
        }

        public void Dispose()
        {
            foreach(SmartScope s in ss)
                s.Dispose();
        }

       
        /// <summary>
        /// Get a package of scope data
        /// </summary>
        /// <returns>Null in case communication failed, a data package otherwise. Might result in disconnecting the device if a sync error occurs</returns>
        public DataPackageScope GetScopeData()
		{
            DataPackageScope p1 = smartScopes[Position.First].GetScopeData();
            DataPackageScope p2 = smartScopes[Position.Second].GetScopeData();
            if (p1 == null) return null;
            return p1.MergeWith(p2);
        }
        
        public bool Ready { get { return ss.Select(x => x.Ready).Aggregate(true, (acc, x) => acc && x); } }


        public bool Rolling
        {
            get { return ss.Select(x => x.Rolling).Aggregate(true, (acc, x) => acc && x); }
            set { ss.ForEach(x => x.Rolling = value); }
        }
        public bool Running
        {
            get { return ss.Select(x => x.Running).Aggregate(true, (acc, x) => acc && x); }
            set { ss.ForEach(x => x.Running = value); }
        }
        public bool CanRoll
        {
            get { return ss.Select(x => x.CanRoll).Aggregate(true, (acc, x) => acc && x); }
        }
        public bool StopPending
        {
            get { return ss.Select(x => x.StopPending).Aggregate(false, (acc, x) => acc || x); }
        }
        public bool AwaitingTrigger
        {
            get { return ss.Select(x => x.AwaitingTrigger).Aggregate(false, (acc, x) => acc || x); }
        }
        public bool Armed { get { return ss.Select(x => x.Armed).Aggregate(false, (acc, x) => acc || x); } }

        /* Acquisition & Trigger */
        public uint AcquisitionDepthUserMaximum
        {
            get { return first.AcquisitionDepthUserMaximum; }
            set { ss.ForEach(x => x.AcquisitionDepthUserMaximum = value); }
        }
        public bool PreferPartial
        {
            get { return ss.Select(x => x.PreferPartial).Aggregate(true, (acc, x) => acc && x); }
            set { ss.ForEach(x => x.PreferPartial = value); }
        }
        public AcquisitionMode AcquisitionMode
        {
            get { return first.AcquisitionMode; }
            set { ss.ForEach(x => x.AcquisitionMode = value); }
        }
        public double AcquisitionLength
        {
            get { return first.AcquisitionLength; }
            set { ss.ForEach(x => x.AcquisitionLength = value); }
        }
        public double SamplePeriod { get { return first.SamplePeriod; } }
        public double AcquisitionLengthMax { get { return first.AcquisitionLengthMax; } }
        public double AcquisitionLengthMin { get { return first.AcquisitionLengthMin; } }
        public uint AcquisitionDepthMax { get { return first.AcquisitionDepthMax; } }
        public uint InputDecimationMax { get { return first.InputDecimationMax; } }
        public int SubSampleRate { get { return first.SubSampleRate; } }
        public uint AcquisitionDepth
        {
            get { return first.AcquisitionDepth; }
            set { ss.ForEach(x => x.AcquisitionDepth = value); } 
        }
        public double TriggerHoldOff
        {
            get { return first.TriggerHoldOff; }
            set { ss.ForEach(x => x.TriggerHoldOff = value); }
        }
        private TriggerValue triggerValue;
        public TriggerValue TriggerValue
        {
            get { 
                //OK need logic here to adjust channel numbers
                throw new NotImplementedException();
            }
            set {
                if (value.source != TriggerSource.Channel || value.mode == TriggerMode.Digital)
                    throw new Exception("Only analog channel triggering supported for now in 4 channel mode");
                this.triggerValue = value.Copy();
                TriggerValue tvAdjusted = value.Copy();
                tvAdjusted.channel = ChMod2(tvAdjusted.channel);
                
                SmartScope master = (SmartScope)ScopeOf(value.channel);
                SmartScope slave = master == first ? second : first;
                master.TriggerValue = tvAdjusted;
                slave.TriggerValue = new Devices.TriggerValue() { 
                    mode = TriggerMode.Edge,
                    source = TriggerSource.External,
                    edge = TriggerEdge.RISING
                };
                //First set slave mode, to ensure not having 2 masters
                slave.Set4ChannelMode(true, false);

                master.Set4ChannelMode(true, true);
            }
        }
        public bool SendOverviewBuffer
        {
            get { return first.SendOverviewBuffer; }
            set { ss.ForEach(x => x.SendOverviewBuffer = value); }
        }
        public void ForceTrigger()
        { 
            // FIXME: only send force trigger to scope who has trigger
            ss.ForEach(x => x.ForceTrigger());
        }

        /* Channel specifics */
        public void SetCoupling(AnalogChannel channel, Coupling coupling)
        {
            ScopeOf(channel).SetCoupling(ChMod2(channel), coupling);
        }
        public Coupling GetCoupling(AnalogChannel channel)
        {
            return ScopeOf(channel).GetCoupling(ChMod2(channel));
        }
        public void SetVerticalRange(AnalogChannel channel, float minimum, float maximum)
        {
            ScopeOf(channel).SetVerticalRange(ChMod2(channel), minimum, maximum);
        }

        public void SetYOffset(AnalogChannel channel, float offset)
        {
            ScopeOf(channel).SetYOffset(ChMod2(channel), offset);
        }
        public float GetYOffset(AnalogChannel channel)
        {
            return ScopeOf(channel).GetYOffset(ChMod2(channel));
        }
        public float GetYOffsetMax(AnalogChannel ch)
        {
            return ScopeOf(ch).GetYOffsetMax(ChMod2(ch));
        }
        public float GetYOffsetMin(AnalogChannel ch)
        {
            return ScopeOf(ch).GetYOffsetMin(ChMod2(ch));
        }
        public void SetProbeDivision(AnalogChannel ch, ProbeDivision division) {
            ScopeOf(ch).SetProbeDivision(ChMod2(ch), division);
        }
        public ProbeDivision GetProbeDivision(AnalogChannel ch) 
        {
            return ScopeOf(ch).GetProbeDivision(ChMod2(ch));
        }

        /* Logic Analyser */
        public bool LogicAnalyserEnabled { get { return false; } }
        public AnalogChannel ChannelSacrificedForLogicAnalyser { set {  } }

        /* Viewport */
        public void SetViewPort(double offset, double timespan)
        {
            ss.ForEach(x => x.SetViewPort(offset, timespan));
        }
        public double ViewPortTimeSpan { get { return first.ViewPortTimeSpan; } }
        public double ViewPortOffset { get { return first.ViewPortOffset; } }
        public bool SuspendViewportUpdates
        {
            get { return ss.Select(x => x.SuspendViewportUpdates).Aggregate(true, (acc, x) => acc && x); }
            set { ss.ForEach(x => x.SuspendViewportUpdates = value); }
        }

        public void CommitSettings() {
            ss.ForEach(x => x.CommitSettings());
        }

        public List<DeviceMemory> GetMemories() {
            List<DeviceMemory> dm = new List<DeviceMemory>();
                ss.ForEach(x => dm.AddRange(x.GetMemories()));
            return dm;
        }

        private DataSource dataSourceScope ;
        public DataSource DataSourceScope { get { return dataSourceScope; } }

    }
}
