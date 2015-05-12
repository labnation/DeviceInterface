using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
#if DEBUG
using LabNation.DeviceInterface.Memories;
#endif

namespace LabNation.DeviceInterface.Devices
{
    public delegate void DeviceConnectHandler(IDevice device, bool connected);

    public interface IDevice
    {
        bool Ready { get; }
        string Serial { get; }
#if DEBUG
        List<DeviceMemory> GetMemories();
#endif

    }
}
