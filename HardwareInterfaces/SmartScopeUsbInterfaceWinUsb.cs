using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;
using MadWizard.WinUSBNet;
using System.Threading;
using H=ECore.HardwareInterfaces.SmartScopeUsbInterfaceHelpers;

namespace ECore.HardwareInterfaces
{
    internal class SmartScopeUsbInterfaceWinUsb : ISmartScopeUsbInterface
    {
        private bool destroyed = false;
        private object usbLock = new object();
        
        private enum EndPoint { CMD_WRITE, CMD_READ, DATA };
        private class UsbCommand
        {
            public USBPipe endPoint;
            public byte[] buffer;
            public int timeout;
            public byte[] result;
            public bool executed;
            public bool success = true;
            public int bytesReadOrWritten;
            public UsbCommand(USBPipe ep, byte[] buffer, int timeout)
            {
                this.endPoint = ep;
                this.buffer = buffer;
                this.timeout = timeout;
                this.bytesReadOrWritten = -1;
                this.executed = false;
                this.result = null;
            }

            public void Execute(object usbLock)
            {
                if (endPoint.IsOut)
                {
                    try
                    {
                        endPoint.Write(buffer);//, 0, buffer.Length, new AsyncCallback(WriteCallback), null);
                        bytesReadOrWritten = buffer.Length;
                    }
                    catch (USBException e)
                    {
                        success = false;
                    }
                }
                else if (endPoint.IsIn)
                {
                    try
                    {
                        bytesReadOrWritten = endPoint.Read(buffer);//, 0, buffer.Length, new AsyncCallback(ReadCallback), null);
                    }
                    catch (USBException e)
                    {
                        success = false;
                        bytesReadOrWritten = 0;
                        buffer = null;
                    }
                }
                else
                {
                    throw new ScopeIOException("Unknown endpoint type");
                }
                executed = true;
            }
            /*
            private void WriteCallback(IAsyncResult r)
            {
                bytesReadOrWritten = buffer.Length;
                executed = true;
            }

            private void ReadCallback(IAsyncResult r)
            {
                bytesReadOrWritten = buffer.Length;
                executed = true;
            }
            */
            internal void WaitForCompletion()
            {
                while (!executed)
                    Thread.Sleep(1);
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

        public SmartScopeUsbInterfaceWinUsb(USBDevice usbDevice)
        {
            device = usbDevice;
            serial = usbDevice.Descriptor.SerialNumber;
            foreach (USBPipe p in device.Pipes)
            {
                USBPipePolicy pol = p.Policy;
                pol.PipeTransferTimeout = 1000;
                if (p.IsIn)
                {
                    pol.AllowPartialReads = false;
                    pol.IgnoreShortPackets = true;
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
            //lock (usbLock)
            {
                destroyed = true;
            }
        }

        public string GetSerial()
        {
            return serial;
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

                if (!cmd.success)
                    throw new ScopeIOException("Failed to write to scope");

                if (cmd.bytesReadOrWritten != length)
                    throw new ScopeIOException(String.Format("Only wrote {0} out of {1} bytes", cmd.bytesReadOrWritten, length));
            }
        }

        public byte[] ReadControlBytes(int length)
        {
            UsbCommand cmd = new UsbCommand(commandReadEndpoint, new byte[COMMAND_READ_ENDPOINT_SIZE], USB_TIMEOUT);
            cmd.Execute(usbLock);

            //FIXME: allow async completion
            cmd.WaitForCompletion();

            byte[] returnBuffer = new byte[length];
            Array.Copy(cmd.buffer, returnBuffer, length);

            return returnBuffer;
        }

        public void FlushDataPipe()
        {
            //lock (usbLock)
            {
                if (!destroyed)
                {
                    dataEndpoint.Abort();
                    dataEndpoint.Flush();
                }
            }
        }

        public byte[] GetData(int numberOfBytes)
        {
            UsbCommand cmd = new UsbCommand(dataEndpoint, new byte[numberOfBytes], USB_TIMEOUT);
            cmd.Execute(usbLock);
            cmd.WaitForCompletion();

            if (cmd.bytesReadOrWritten != numberOfBytes)
                return null;
            //return read data
            return cmd.buffer;
        }

        public void GetControllerRegister(ScopeController ctrl, uint address, uint length, out byte[] data)
        {
            //In case of FPGA (I2C), first write address we're gonna read from to FPGA
            //FIXME: this should be handled by the PIC firmware
            if (ctrl == ScopeController.FPGA || ctrl == ScopeController.FPGA_ROM)
                SetControllerRegister(ctrl, address, null);

            if (ctrl == ScopeController.FLASH && (address + length) > (H.FLASH_USER_ADDRESS_MASK + 1))
            {
                throw new ScopeIOException(String.Format("Can't read flash rom beyond 0x{0:X8}", H.FLASH_USER_ADDRESS_MASK));
            }

            byte[] header = H.UsbCommandHeader(ctrl, H.Operation.READ, address, length);
            this.WriteControlBytes(header, false);

            //EP3 always contains 16 bytes xxx should be linked to constant
            //FIXME: use endpoint length or so, or don't pass the argument to the function
            byte[] readback = ReadControlBytes(16);

            int readHeaderLength;
            if (ctrl == ScopeController.FLASH)
                readHeaderLength = 5;
            else
                readHeaderLength = 4;

            //strip away first 4 bytes as these are not data
            data = new byte[length];
            Array.Copy(readback, readHeaderLength, data, 0, length);
        }

        public void SetControllerRegister(ScopeController ctrl, uint address, byte[] data)
        {
            if (data != null && data.Length > H.I2C_MAX_WRITE_LENGTH)
            {
                if (ctrl != ScopeController.AWG)
                    throw new Exception(String.Format("Can't do writes of this length ({0}) to controller {1:G}", data.Length, ctrl));

                int offset = 0;
                byte[] toSend = new byte[32];

                //Begin I2C - send start condition
                WriteControlBytes(H.UsbCommandHeader(ctrl, H.Operation.WRITE_BEGIN, address, 0), false);

                while (offset < data.Length)
                {
                    int length = Math.Min(data.Length - offset, H.I2C_MAX_WRITE_LENGTH_BULK);
                    byte[] header = H.UsbCommandHeader(ctrl, H.Operation.WRITE_BODY, address, (uint)length);
                    Array.Copy(header, toSend, header.Length);
                    Array.Copy(data, offset, toSend, header.Length, length);
                    WriteControlBytes(toSend, false);
                    offset += length;
                }
                WriteControlBytes(H.UsbCommandHeader(ctrl, H.Operation.WRITE_END, address, 0), false);
            }
            else
            {
                uint length = data != null ? (uint)data.Length : 0;
                byte[] header = H.UsbCommandHeader(ctrl, H.Operation.WRITE, address, length);

                //Paste header and data together and send it
                byte[] toSend = new byte[header.Length + length];
                Array.Copy(header, toSend, header.Length);
                if (length > 0)
                    Array.Copy(data, 0, toSend, header.Length, data.Length);
                WriteControlBytes(toSend, false);
            }
        }

        public void SendCommand(H.PIC_COMMANDS cmd)
        {
            byte[] toSend = new byte[2] { H.HEADER_CMD_BYTE, (byte)cmd };
            WriteControlBytes(toSend, false);
        }

        public void LoadBootLoader()
        {
            this.SendCommand(H.PIC_COMMANDS.PIC_BOOTLOADER);
        }

        public void Reset()
        {
            this.SendCommand(H.PIC_COMMANDS.PIC_RESET);
        }
    }
}
