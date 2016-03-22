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
        public enum Function { Master, Slave };
#if DEBUG
        public
#else
        private
#endif
        Dictionary<Function, SmartScope> smartScopes;
        List<SmartScope> ss { get { return smartScopes.Values.ToList(); } }
        SmartScope master { get { return smartScopes[Function.Master]; } }
        SmartScope slave { get { return smartScopes[Function.Slave]; } }

        #if ANDROID
        Context context;
        #endif

        public string Serial
        {
            get
            {
                return "Master: " + smartScopes[Function.Master].Serial + "\nSlave: " + smartScopes[Function.Slave].Serial;
            }
        }

        internal SmartScope4Channel(ISmartScopeUsbInterface usbInterfaceMaster, ISmartScopeUsbInterface usbInterfaceSlave) : base()
        {
            smartScopes = new Dictionary<Function,SmartScope>();
            smartScopes.Add(Function.Master, new SmartScope(usbInterfaceMaster));
            smartScopes.Add(Function.Slave, new SmartScope(usbInterfaceSlave));
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
            return smartScopes[Function.Master].GetScopeData();
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
            get { return master.AcquisitionDepthUserMaximum; }
            set { ss.ForEach(x => x.AcquisitionDepthUserMaximum = value); }
        }
        public bool PreferPartial
        {
            get { return ss.Select(x => x.PreferPartial).Aggregate(true, (acc, x) => acc && x); }
            set { ss.ForEach(x => x.PreferPartial = value); }
        }
        public AcquisitionMode AcquisitionMode
        {
            get { return master.AcquisitionMode; }
            set { ss.ForEach(x => x.AcquisitionMode = value); }
        }
        public double AcquisitionLength
        {
            get { return master.AcquisitionLength; }
            set { ss.ForEach(x => x.AcquisitionLength = value); }
        }
        public double SamplePeriod { get { return master.SamplePeriod; } }
        public double AcquisitionLengthMax { get { return master.AcquisitionLengthMax; } }
        public double AcquisitionLengthMin { get { return master.AcquisitionLengthMin; } }
        public uint AcquisitionDepthMax { get { return master.AcquisitionDepthMax; } }
        public uint InputDecimationMax { get { return master.InputDecimationMax; } }
        public int SubSampleRate { get { return master.SubSampleRate; } }
        public uint AcquisitionDepth
        {
            get { return master.AcquisitionDepth; }
            set { ss.ForEach(x => x.AcquisitionDepth = value); } 
        }
        public double TriggerHoldOff
        {
            get { return master.TriggerHoldOff; }
            set { ss.ForEach(x => x.TriggerHoldOff = value); }
        }
        public TriggerValue TriggerValue
        {
            get { 
                //OK need logic here to adjust channel numbers
                return master.TriggerValue; 
            }
            set { 
                // FIXME as abive
                ss.ForEach(x => x.TriggerValue= value); 
            }
        }
        public bool SendOverviewBuffer
        {
            get { return master.SendOverviewBuffer; }
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
            master.SetCoupling(channel, coupling);
        }
        public Coupling GetCoupling(AnalogChannel channel)
        {
            return master.GetCoupling(channel);
        }
        public void SetVerticalRange(AnalogChannel channel, float minimum, float maximum)
        {
            master.SetVerticalRange(channel, minimum, maximum);
        }

        public void SetYOffset(AnalogChannel channel, float offset)
        {
            master.SetYOffset(channel, offset);
        }
        public float GetYOffset(AnalogChannel channel)
        {
            return master.GetYOffset(channel);
        }
        public float GetYOffsetMax(AnalogChannel ch)
        {
            return master.GetYOffsetMax(ch);
        }
        public float GetYOffsetMin(AnalogChannel ch)
        {
            return master.GetYOffsetMin(ch);
        }
        public void SetProbeDivision(AnalogChannel ch, ProbeDivision division) { }
        public ProbeDivision GetProbeDivision(AnalogChannel ch) 
        {
            return master.GetProbeDivision(ch);
        }

        /* Logic Analyser */
        public bool LogicAnalyserEnabled { get { return false; } }
        public AnalogChannel ChannelSacrificedForLogicAnalyser { set {  } }

        /* Viewport */
        public void SetViewPort(double offset, double timespan)
        {
            ss.ForEach(x => x.SetViewPort(offset, timespan));
        }
        public double ViewPortTimeSpan { get { return master.ViewPortTimeSpan; } }
        public double ViewPortOffset { get { return master.ViewPortOffset; } }
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

        public DataSources.DataSource DataSourceScope { get { return master.DataSourceScope; } }

    }
}
