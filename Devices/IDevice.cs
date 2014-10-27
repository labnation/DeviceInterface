using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
#if DEBUG
using ECore.DeviceMemories;
#endif

namespace ECore.Devices
{
    public delegate void DeviceConnectHandler(IDevice scope, bool connected);

    public interface IDevice
    {
        bool Ready { get; }
        string Serial { get; }
#if DEBUG
        List<DeviceMemory> GetMemories();
#endif

    }
}
