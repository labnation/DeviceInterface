using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.EInterfaces
{
    [FunctionalityInterfaceAttribute]
    public interface ITriggerPosition
    {        
        EFunctionality TriggerPosition { get; }
        //EFunctionality CalibrationEnabled { get; }
    }
}
