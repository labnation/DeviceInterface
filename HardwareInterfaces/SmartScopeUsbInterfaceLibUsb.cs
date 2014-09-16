using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;
using LibUsbDotNet.Main;
using LibUsbDotNet;
using System.Threading;
using H=ECore.HardwareInterfaces.SmartScopeUsbInterfaceHelpers;

namespace ECore.HardwareInterfaces
{
    internal class SmartScopeUsbInterfaceLibUsb : ISmartScopeUsbInterface
    {
        private bool destroyed = false;
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

        public SmartScopeUsbInterfaceLibUsb(UsbDevice usbDevice)
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

            Common.Logger.Debug("Created new ScopeUsbInterface");

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
                if (!destroyed)
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
