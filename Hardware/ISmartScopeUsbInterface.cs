using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LabNation.DeviceInterface.Hardware
{
    delegate void OnDeviceConnect(ISmartScopeUsbInterface hardwareInterface, bool connected);

    public class ScopeIOException : Exception
    {
        internal ScopeIOException(string msg) : base(msg) { }
    }

#if DEBUG
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

#if DEBUG
    public
#else
    internal
#endif
    interface ISmartScopeUsbInterface
    {
        string Serial { get; }

        void WriteControlBytes(byte[] message, bool async);
        void WriteControlBytesBulk(byte[] message, bool async);
        void WriteControlBytesBulk(byte[] message, int offset, int length, bool async);
        byte[] ReadControlBytes(int length);
        
        byte[] GetData(int numberOfBytes);

		bool Destroyed { get; }
        void Destroy();
        void FlushDataPipe(); 
    }
}
