using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;
using MadWizard.WinUSBNet;
using System.Threading;

namespace LabNation.DeviceInterface.Hardware
{
    public class SmartScopeHardwareWinUsb : ISmartScopeHardwareUsb
    {
        public bool Destroyed { get; private set; }
        private object usbLock = new object();
        
        private enum EndPoint { CMD_WRITE, CMD_READ, DATA };
        private class UsbCommand
        {
            public USBPipe endPoint;
            public byte[] buffer;
            private int offset;
            private int length;
            
            private IAsyncResult asyncResult;
            
            public UsbCommand(USBPipe ep, byte[] buffer, int offset, int length)
            {
                this.offset = offset;
                this.length = length;
                this.endPoint = ep;
                this.buffer = buffer;
            }

            public void Execute(object usbLock)
            {
                try
                {
                    if (endPoint.IsOut)
                    {
                        asyncResult = endPoint.BeginWrite(buffer, offset, length, null, null);
                    }
                    else if (endPoint.IsIn)
                    {
                        asyncResult = endPoint.BeginRead(buffer, offset, length, null, null);
                    }
                    else
                    {
                        throw new ScopeIOException("Unknown endpoint type");
                    }
                }
                catch (USBException e)
                {
                    throw new ScopeIOException("I/O with scope failed (" + e.Message + ")");
                }
            }

            internal void WaitForCompletion()
            {
                if (asyncResult == null)
                    throw new ScopeIOException("Can't wait for command's completion before executing command");

                try
                {
                    if (endPoint.IsOut)
                    {
                        endPoint.EndWrite(asyncResult);
                    }
                    else if (endPoint.IsIn)
                    {
                        endPoint.EndRead(asyncResult);
                    }
                    else
                    {
                        throw new ScopeIOException("Unknown endpoint type");
                    }
                }
                catch (Exception e)
                {
                    throw new ScopeIOException("USB Error occurred: " + e.Message);
                }

                USBAsyncResult usbresult = (USBAsyncResult)asyncResult;
                if (usbresult.Error != null)
                    throw new ScopeIOException("USB Error occurred: " + usbresult.Error.Message);
                if (usbresult.BytesTransfered != length)
                    throw new ScopeIOException(String.Format("Only transferred {0:d} out of {1:d} bytes", usbresult.BytesTransfered, length));
            }
        }
        
        private const int USB_TIMEOUT = 1000;
        private const int COMMAND_READ_ENDPOINT_SIZE = 16;
        private const short COMMAND_WRITE_ENDPOINT_SIZE = 32;

        private USBDevice device;
        private USBPipe commandWriteEndpoint;
        private USBPipe commandReadEndpoint;
        private USBPipe dataEndpoint;

        private object registerLock = new object();
        private string serial;

        public SmartScopeHardwareWinUsb(USBDevice usbDevice)
        {
            Destroyed = false;
            device = usbDevice;
            serial = usbDevice.Descriptor.SerialNumber;
            foreach (USBPipe p in device.Pipes)
            {
                p.Abort();
                USBPipePolicy pol = p.Policy;
                pol.PipeTransferTimeout = USB_TIMEOUT;
                if (p.IsIn)
                {
                    p.Flush();
                    pol.AllowPartialReads = true;
                    pol.IgnoreShortPackets = false;
                }
            }
            USBPipe[] pipes = device.Pipes.ToArray();
            dataEndpoint = pipes[0];
            commandWriteEndpoint = pipes[1];
            commandReadEndpoint = pipes[2];
            Common.Logger.Debug("Created new WinUSB ScopeUsbInterface");
        }
        
        public void Destroy()
        {
            Destroyed = true;
        }

        public string Serial
        {
            get
            {
                return serial;
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
            UsbCommand cmd = new UsbCommand(commandWriteEndpoint, message, offset, length);
            cmd.Execute(usbLock);
            
            if (!async)
                cmd.WaitForCompletion();
        }

        public void ReadControlBytes(byte[] buffer, int offset, int length)
        {
            UsbCommand cmd = new UsbCommand(commandReadEndpoint, buffer, offset, length);
            cmd.Execute(usbLock);

            //FIXME: allow async completion
            cmd.WaitForCompletion();
        }

        public void FlushDataPipe()
        {
            //lock (usbLock)
            {
                if (!Destroyed)
                {
                    try
                    {
                        dataEndpoint.Policy.PipeTransferTimeout = 10;
                        dataEndpoint.Read(new byte[dataEndpoint.MaximumPacketSize]);
                        dataEndpoint.Read(new byte[dataEndpoint.MaximumPacketSize]);

                    }
                    catch (Exception)
                    {
                    }
                    finally
                    {
                        dataEndpoint.Policy.PipeTransferTimeout = USB_TIMEOUT;
                    }
                }
            }
        }

        public void GetData(byte[] buffer, int offset, int length)
        {
            UsbCommand cmd = new UsbCommand(dataEndpoint, buffer, offset, length);
            cmd.Execute(usbLock);
            cmd.WaitForCompletion();
        }
    }
}
