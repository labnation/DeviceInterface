using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.DeviceImplementations
{
    public enum ScopeChannel { ChA, ChB, Digi0, Digi1, Digi2, Digi3, Digi4, Digi5, Digi6, Digi7, Math, FftA, FftB, I2CDecoder }
    public class ScopeData
    {
        private float[] voltages;
        public float[] Voltages { get { return voltages; } }
        public ScopeData(float[] voltages)
        {
            this.voltages = voltages;
        }
        
    }
    public abstract class Scope: EDeviceImplementation
    {
        public Scope(EDevice device) : base(device) { }
        //FIXME: add channel argument
        public abstract ScopeData GetScopeData();
    }
}
