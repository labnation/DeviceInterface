using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LabNation.DeviceInterface.Hardware
{
    public interface IHardwareInterface
    {
        string Serial { get; }
    }
}
