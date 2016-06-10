using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;
using LibUsbDotNet.Main;
using LibUsbDotNet;
using System.Threading;
using H=LabNation.DeviceInterface.Hardware.SmartScopeInterfaceHelpers;

namespace LabNation.DeviceInterface.Hardware
{
    public class SmartScopeInterfaceLibUsb : ISmartScopeInterfaceUsb
    {
    	public bool Destroyed { get; private set; }
        private object usbLock = new object();
        
        private enum EndPoint { CMD_WRITE, CMD_READ, DATA };
        private class UsbCommand
        {
            public UsbEndpointBase endPoint;
            public byte[] buffer;
            public int timeout;
            public byte[] result;
            public ErrorCode resultCode;
            public bool executed;
            public int bytesReadOrWritten;
            public UsbCommand(UsbEndpointBase ep, byte[] buffer, int timeout)
            {
                this.endPoint = ep;
                this.buffer = buffer;
                this.timeout = timeout;
                this.bytesReadOrWritten = -1;
                this.executed = false;
                this.resultCode = ErrorCode.None;
                this.result = null;
            }

            public void Execute(object usbLock)
            {
                if (endPoint is UsbEndpointWriter)
                {
                    //lock (usbLock)
                    {
                        resultCode = ((UsbEndpointWriter)endPoint).Write(buffer, timeout, out bytesReadOrWritten);
                    }
                    executed = true;
                }
                else if (endPoint is UsbEndpointReader)
                {
                    //lock (usbLock)
                    {
                        resultCode = ((UsbEndpointReader)endPoint).Read(buffer, timeout, out bytesReadOrWritten);
                    }
                    executed = true;
                }
                else
                {
                    throw new ScopeIOException("Unknown endpoint type");
                }
            }

            internal void WaitForCompletion()
            {
                while (!executed)
                    Thread.Sleep(1);
            }
        }
        
        private const int USB_TIMEOUT = 1000;
        private const int COMMAND_READ_ENDPOINT_SIZE = 16;
        private const short COMMAND_WRITE_ENDPOINT_SIZE = 32;

        private UsbDevice device;
        private UsbEndpointWriter commandWriteEndpoint;
        private UsbEndpointReader commandReadEndpoint;
        private UsbEndpointReader dataEndpoint;

        private object registerLock = new object();
        private string serial;
        public string Serial { get { return serial; } } 

        public SmartScopeInterfaceLibUsb(UsbDevice usbDevice)
        {
            if (usbDevice is IUsbDevice)
            {
                bool succes1 = (usbDevice as IUsbDevice).SetConfiguration(1);
                if (!succes1)
                    throw new ScopeIOException("Failed to set usb device configuration");
                bool succes2 = (usbDevice as IUsbDevice).ClaimInterface(0);
                if (!succes2)
                    throw new ScopeIOException("Failed to claim usb interface");
            }
            device = usbDevice;
            serial = usbDevice.Info.SerialString;
            dataEndpoint = usbDevice.OpenEndpointReader(ReadEndpointID.Ep01);
            commandWriteEndpoint = usbDevice.OpenEndpointWriter(WriteEndpointID.Ep02);
            commandReadEndpoint = usbDevice.OpenEndpointReader(ReadEndpointID.Ep03);

            Common.Logger.Debug("Created new ScopeUsbInterface from device with serial " + serial);

        }

        public void Destroy()
        {
            //lock (usbLock)
            {
				Common.Logger.Debug("Closing device " + serial);
            	device.Close();
				Common.Logger.Debug("Destroying device " + serial);
                Destroyed = true;
            }
        }

        public void WriteControlBytes(byte[] message, bool async)
        {
            if (message.Length > COMMAND_WRITE_ENDPOINT_SIZE)
            {
                throw new ScopeIOException("USB message too long for endpoint");
            }
            WriteControlBytesBulk(message, async);
        }

        public void WriteControlBytesBulk(byte[] message, bool async = false)
        {
            WriteControlBytesBulk(message, 0, message.Length, async);
        }

        public void WriteControlBytesBulk(byte[] message, int offset, int length, bool async = false)
        {
            if (commandWriteEndpoint == null)
                throw new ScopeIOException("Command write endpoint is null");

            byte[] buffer;
            if (offset == 0 && length == message.Length)
                buffer = message;
            else
            {
                buffer = new byte[length];
                Array.ConstrainedCopy(message, offset, buffer, 0, length);
            }

            UsbCommand cmd = new UsbCommand(commandWriteEndpoint, buffer, USB_TIMEOUT);
            cmd.Execute(usbLock);
            
            if (!async)
            {
                cmd.WaitForCompletion();

                if (cmd.bytesReadOrWritten != length)
                    throw new ScopeIOException(String.Format("Only wrote {0} out of {1} bytes", cmd.bytesReadOrWritten, length));
                switch (cmd.resultCode)
                {
                    case ErrorCode.Success:
                        break;
                    default:
                        throw new ScopeIOException("Failed to read from USB device : " + cmd.resultCode.ToString("G"));
                }
            }
        }

        public byte[] ReadControlBytes(int length)
        {
            UsbCommand cmd = new UsbCommand(commandReadEndpoint, new byte[COMMAND_READ_ENDPOINT_SIZE], USB_TIMEOUT);
            cmd.Execute(usbLock);

            //FIXME: allow async completion
            cmd.WaitForCompletion();

            switch (cmd.resultCode)
            {
                case ErrorCode.Success:
                    break;
                default:
                    throw new ScopeIOException("Failed to read from device: " + cmd.resultCode.ToString("G"));
            }
            byte[] returnBuffer = new byte[length];
            Array.Copy(cmd.buffer, returnBuffer, length);

            return returnBuffer;
        }

        public void FlushDataPipe()
        {
            //lock (usbLock)
            {
                if (!Destroyed)
                    dataEndpoint.Reset();
            }
        }

        public byte[] GetData(int numberOfBytes)
        {
            UsbCommand cmd = new UsbCommand(dataEndpoint, new byte[numberOfBytes], USB_TIMEOUT);
            cmd.Execute(usbLock);
            cmd.WaitForCompletion();

            if (cmd.bytesReadOrWritten != numberOfBytes)
                return null;
            switch (cmd.resultCode)
            {
                case ErrorCode.Success:
                    break;
                default:
                    throw new ScopeIOException("An error occured while fetching scope data: " + cmd.resultCode.ToString("G"));
            }
            //return read data
            return cmd.buffer;
        }
    }
}
