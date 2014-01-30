using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.EInterfaces
{
    [FunctionalityInterfaceAttribute]
    public interface IScopeChannel
    {
        float MaxRange { get; }
        float ScalingFactor { get; }
        EFunctionality DivisionFactor { get; }
        EFunctionality MultiplicationFactor { get; }
        EFunctionality SamplingFrequency { get; }
        EFunctionality ChannelOffset { get; }
    }
}
