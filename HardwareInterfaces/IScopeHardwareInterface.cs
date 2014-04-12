using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.HardwareInterfaces
{
    public enum ScopeController {
        PIC,
        FPGA,
        FPGA_ROM
    }
    public interface IScopeHardwareInterface
    {
        void SetControllerRegister(ScopeController ctrl, int address, byte[] data);

        void GetControllerRegister(ScopeController ctrl, int address, int length, out byte[] data);

    }
}
