using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.HardwareInterfaces
{
    delegate void OnDeviceConnect(ISmartScopeUsbInterface hardwareInterface, bool connected);

    public class ScopeIOException : Exception
    {
        internal ScopeIOException(string msg) : base(msg) { }
    }

#if INTERNAL
    public
#else
    internal
#endif
    enum ScopeController
    {
        PIC,
        ROM,
        FLASH,
        FPGA,
        FPGA_ROM,
        AWG
    }

#if INTERNAL
    public
#else
    internal
#endif
    interface ISmartScopeUsbInterface
    {
        string GetSerial();

        void GetControllerRegister(ScopeController ctrl, uint address, uint length, out byte[] data);
        void SetControllerRegister(ScopeController ctrl, uint address, byte[] data);

        void WriteControlBytes(byte[] message, bool async);
        void WriteControlBytesBulk(byte[] message, bool async);
        byte[] ReadControlBytes(int length);
        
        void SendCommand(SmartScopeUsbInterfaceHelpers.PIC_COMMANDS cmd);
        
        byte[] GetData(int numberOfBytes);

        void FlushDataPipe(); 
        void Reset();
        void LoadBootLoader();
    }
}
