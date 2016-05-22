using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.Hardware;
#if DEBUG
using LabNation.DeviceInterface.Memories;
#endif

namespace LabNation.DeviceInterface.Devices
{
    public delegate void DeviceConnectHandler(IDevice device, bool connected);
    public delegate void InterfaceConnectHandler(ISmartScopeUsbInterface hardwareInterface, bool connected);

    public interface IDevice
    {
        bool Ready { get; }
        string Serial { get; }
#if DEBUG
        List<DeviceMemory> GetMemories();
#endif

    }
}
