using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.HardwareInterfaces
{
    public enum ScopeController {
        PIC,
        ROM,
        FLASH,
        FPGA,
        FPGA_ROM
    }
    public interface IScopeHardwareInterface
    {
        void SetControllerRegister(ScopeController ctrl, uint address, byte[] data);

        void GetControllerRegister(ScopeController ctrl, uint address, uint length, out byte[] data);

    }
}
