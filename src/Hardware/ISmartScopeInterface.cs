using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LabNation.DeviceInterface.Hardware
{
    public class ScopeIOException : Exception
    {
        internal ScopeIOException(string msg) : base(msg) { }
    }

    public enum ScopeController
    {
        PIC = 0,
        ROM = 1,
        FLASH = 2,
        FPGA = 3,
        AWG = 4
    }
		
    public interface ISmartScopeInterface : IHardwareInterface
    {
        void GetControllerRegister(ScopeController ctrl, uint address, uint length, out byte[] data);
        void SetControllerRegister(ScopeController ctrl, uint address, byte[] data);
        int GetAcquisition(byte[] buffer);
        byte[] GetData(int length);
        void FlushDataPipe();
        void Reset();
        bool FlashFpga(byte[] firmware);
        byte[] PicFirmwareVersion { get; }
        bool Destroyed { get; }
        void Destroy();
    }
}
