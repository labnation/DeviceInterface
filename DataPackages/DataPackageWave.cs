using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.DataPackages
{
    public abstract class DataPackageWave<T>
    {
        private T[] samples;
        private uint triggerIndex;

        public DataPackageWave(T[] samples, uint triggerIndex)
        {
            this.samples = samples;
            this.triggerIndex = triggerIndex;
        }

        public T[] Samples { get { return this.samples; } }
        public uint TriggerIndex { get { return this.triggerIndex; } }
    }
}
