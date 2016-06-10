using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;
using MadWizard.WinUSBNet;
using System.Threading;
using H=LabNation.DeviceInterface.Hardware.SmartScopeInterfaceHelpers;

namespace LabNation.DeviceInterface.Hardware
{
    public class SmartScopeInterfaceWinUsb : ISmartScopeInterfaceUsb
    {
        public bool Destroyed { get; private set; }
        private object usbLock = new object();
        
        private enum EndPoint { CMD_WRITE, CMD_READ, DATA };
        private class UsbCommand
        {
            public USBPipe endPoint;
            public byte[] buffer;
            
            private IAsyncResult asyncResult;
            public Exception Error;
            
            public bool success = true;
            public int bytesTransferred;
            
            public UsbCommand(USBPipe ep, byte[] buffer)
            {
                this.endPoint = ep;
                this.buffer = buffer;
                this.bytesTransferred = -1;
            }

            public void Execute(object usbLock)
            {
                try
                {
                    if (endPoint.IsOut)
                    {
                        asyncResult = endPoint.BeginWrite(buffer, 0, buffer.Length, null, null);
                    }
                    else if (endPoint.IsIn)
                    {
                        asyncResult = endPoint.BeginRead(buffer, 0, buffer.Length, null, null);
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
                catch (USBException e)
                {
                    Error = e;
                    bytesTransferred = 0;
                    buffer = null;
                    return;
                }

                MadWizard.WinUSBNet.USBAsyncResult usbresult = (MadWizard.WinUSBNet.USBAsyncResult)asyncResult;
                if (usbresult.Error != null)
                {
                    Error = usbresult.Error;
                    success = false;
                    bytesTransferred = 0;
                    buffer = null;
                }
                else
                {
                    bytesTransferred = usbresult.BytesTransfered;
                }
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

        public SmartScopeInterfaceWinUsb(USBDevice usbDevice)
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
            LabNation.Common.Logger.Debug("Created new WinUSB ScopeUsbInterface");
        }
        
        public void Destroy()
        {
            //lock (usbLock)
            {
                Destroyed = true;
            }
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

            UsbCommand cmd = new UsbCommand(commandWriteEndpoint, buffer);
            cmd.Execute(usbLock);
            
            if (!async)
            {
                cmd.WaitForCompletion();

                if (!cmd.success)
                    throw new ScopeIOException("Failed to write to scope");

                if (cmd.bytesTransferred != length)
                    throw new ScopeIOException(String.Format("Only wrote {0} out of {1} bytes", cmd.bytesTransferred, length));
            }
        }

        public byte[] ReadControlBytes(int length)
        {
            UsbCommand cmd = new UsbCommand(commandReadEndpoint, new byte[COMMAND_READ_ENDPOINT_SIZE]);
            cmd.Execute(usbLock);

            //FIXME: allow async completion
            cmd.WaitForCompletion();

            if (cmd.buffer == null)
                throw new ScopeIOException("Failed to read control bytes");

            byte[] returnBuffer = new byte[length];
            Array.Copy(cmd.buffer, returnBuffer, length);

            return returnBuffer;
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
                    catch (USBException)
                    {
                    }
                    finally
                    {
                        dataEndpoint.Policy.PipeTransferTimeout = USB_TIMEOUT;
                    }
                
                }
            }
        }
#if DEBUG
        public static byte[] lastBuffer2 = null;
        public static byte[] lastBuffer = null;
#endif
        public byte[] GetData(int numberOfBytes)
        {
            UsbCommand cmd = new UsbCommand(dataEndpoint, new byte[numberOfBytes]);
            cmd.Execute(usbLock);
            cmd.WaitForCompletion();

            if (cmd.bytesTransferred == 0)
                return null;
            if (cmd.bytesTransferred != numberOfBytes)
            {
                LabNation.Common.Logger.Error(String.Format("WinUSB GetData: got {0} bytes instead of requested {1}", cmd.bytesTransferred, numberOfBytes));
                return null;
            }

#if DEBUG
            lastBuffer2 = lastBuffer;
            lastBuffer = cmd.buffer;
#endif
            //return read data
            return cmd.buffer;
        }
    }
}
