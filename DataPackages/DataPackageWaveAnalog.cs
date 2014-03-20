using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.DataPackages
{
    public class DataPackageWaveAnalog: DataPackageWave<float> {
        public DataPackageWaveAnalog(float[] samples, uint triggerIndex) : base(samples, triggerIndex) { }
    }
}
