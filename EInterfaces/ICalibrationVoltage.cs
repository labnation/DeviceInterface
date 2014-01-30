using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.EInterfaces
{
    [FunctionalityInterfaceAttribute]
    public interface ICalibrationVoltage
    {        
        EFunctionality CalibrationVoltage { get; }
        //EFunctionality CalibrationEnabled { get; }
    }
}
