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

        void WriteControlBytes(byte[] message, bool async);
        void WriteControlBytesBulk(byte[] message, bool async);
        void WriteControlBytesBulk(byte[] message, int offset, int length, bool async);
        byte[] ReadControlBytes(int length);
        
        byte[] GetData(int numberOfBytes);

        void Destroy();
        void FlushDataPipe(); 
    }
}
