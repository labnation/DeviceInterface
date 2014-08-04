using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LibUsbDotNet;
using LibUsbDotNet.Main;
using LibUsbDotNet.LibUsb;
using C=Common;

namespace ECore.HardwareInterfaces
{
    public class ScopeIOException : Exception
    {
        public ScopeIOException(string msg) : base(msg) { }
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
 class ScopeUsbInterface
    {
        internal enum PIC_COMMANDS
        {
            PIC_VERSION = 1,
            PIC_WRITE = 2,
            PIC_READ = 3,
            PIC_RESET = 4,
            PIC_BOOTLOADER = 5,
            EEPROM_READ = 6,
            EEPROM_WRITE = 7,
            FLASH_ROM_READ = 8,
            FLASH_ROM_WRITE = 9,
            I2C_WRITE = 10,
            I2C_READ = 11,
            PROGRAM_FPGA_START = 12,
            PROGRAM_FPGA_END = 13,
            I2C_WRITE_START = 14,
            I2C_WRITE_BULK = 15,
            I2C_WRITE_STOP = 16,

        }
        internal const byte HEADER_CMD_BYTE = 0xC0; //C0 as in Command
        internal const byte HEADER_RESPONSE_BYTE = 0xAD; //AD as in Answer Dude
        const int FLASH_USER_ADDRESS_MASK = 0x0FFF;
        const byte FPGA_I2C_ADDRESS_SETTINGS = 0x0C;
        const byte FPGA_I2C_ADDRESS_ROM = 0x0D;
        const byte FPGA_I2C_ADDRESS_AWG = 0x0E;
        const int I2C_MAX_WRITE_LENGTH = 27;
        const int I2C_MAX_WRITE_LENGTH_BULK = 29;

        private enum Operation { READ, WRITE, WRITE_BEGIN, WRITE_BODY, WRITE_END };

        private const int USB_TIMEOUT = 1000;
        private const int COMMAND_READ_ENDPOINT_SIZE = 16;

        private UsbDevice device;
        private UsbEndpointWriter commandWriteEndpoint;
        private UsbEndpointReader commandReadEndpoint;
        private UsbEndpointReader dataEndpoint;

        private object registerLock = new object();
        private string serial;

        internal ScopeUsbInterface(UsbDevice usbDevice)
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

			C.Logger.Debug("Created new ScopeUsbInterface");
        }

        internal void Destroy()
        {
            /*
            lock (endpointAccessLock)
            {
                commandWriteEndpoint.Dispose();
                commandReadEndpoint.Dispose();
                dataEndpoint.Dispose();

                commandWriteEndpoint = null;
                commandReadEndpoint = null;
                dataEndpoint = null;
            }
            device.Close();   
             */
        }

        internal string GetSerial()
        {
            return serial;
        }

        internal int WriteControlMaxLength()
        {
            int length;
            if (commandWriteEndpoint == null)
                throw new ScopeIOException("Command write endpoint is null");
            length = commandWriteEndpoint.EndpointInfo.Descriptor.MaxPacketSize;
            return length;
        }

        internal void WriteControlBytes(byte[] message)
        {
            if (message.Length > WriteControlMaxLength())
            {
                throw new ScopeIOException("USB message too long for endpoint");
            }
            WriteControlBytesBulk(message);
        }
        internal void WriteControlBytesBulk(byte[] message)
        {
            int bytesWritten;
            ErrorCode code;

            if (commandWriteEndpoint == null)
                throw new ScopeIOException("Command write endpoint is null");
            code = commandWriteEndpoint.Write(message, USB_TIMEOUT, out bytesWritten);

            if (bytesWritten != message.Length)
                throw new ScopeIOException(String.Format("Only wrote {0} out of {1} bytes", bytesWritten, message.Length));
            switch (code)
            {
                case ErrorCode.Success:
                    break;
                default:
                    throw new ScopeIOException("Failed to read from USB device : " + code.ToString("G"));
            }
        }

        internal byte[] ReadControlBytes(int length)
        {
            //try to read data
            ErrorCode errorCode = ErrorCode.None;
            //send read command
            byte[] readBuffer = new byte[COMMAND_READ_ENDPOINT_SIZE];
            int bytesRead;

            if (commandReadEndpoint == null)
                throw new ScopeIOException("Command read endpoint is null");
            errorCode = commandReadEndpoint.Read(readBuffer, USB_TIMEOUT, out bytesRead);
            switch (errorCode)
            {
                case ErrorCode.Success:
                    break;
                default:
                    throw new ScopeIOException("Failed to read from device: " + errorCode.ToString("G"));
            }
            byte[] returnBuffer = new byte[length];
            Array.Copy(readBuffer, returnBuffer, length);

            return returnBuffer;
        }

        internal void FlushDataPipe()
        {
            if (dataEndpoint == null)
                throw new ScopeIOException("Data endpoint is null");
            dataEndpoint.Reset();
        }

        internal byte[] GetData(int numberOfBytes)
        {
            //try to read data
            ErrorCode errorCode = ErrorCode.None;
            //send read command
            byte[] readBuffer = new byte[numberOfBytes];
            int bytesRead;

            {
                if (dataEndpoint == null)
                    throw new ScopeIOException("Data endpoint is null");
                errorCode = dataEndpoint.Read(readBuffer, USB_TIMEOUT, out bytesRead);
            }
            if (bytesRead != numberOfBytes)
                return null;
            switch (errorCode)
            {
                case ErrorCode.Success:
                    break;
                default:
                    throw new ScopeIOException("An error occured while fetching scope data: " + errorCode.ToString("G"));
            }
            //return read data
            return readBuffer;
        }

        #region ScopeInterface - the internal interface

#if INTERNAL
        public
#else
    internal 
#endif
 void GetControllerRegister(ScopeController ctrl, uint address, uint length, out byte[] data)
        {
            //In case of FPGA (I2C), first write address we're gonna read from to FPGA
            //FIXME: this should be handled by the PIC firmware
            if (ctrl == ScopeController.FPGA || ctrl == ScopeController.FPGA_ROM)
                SetControllerRegister(ctrl, address, null);

            if (ctrl == ScopeController.FLASH && (address + length) > (FLASH_USER_ADDRESS_MASK + 1))
            {
                throw new ScopeIOException(String.Format("Can't read flash rom beyond 0x{0:X8}", FLASH_USER_ADDRESS_MASK));
            }

            byte[] header = UsbCommandHeader(ctrl, Operation.READ, address, length);
            this.WriteControlBytes(header);

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

#if INTERNAL
        public
#else
    internal 
#endif
 void SetControllerRegister(ScopeController ctrl, uint address, byte[] data)
        {
            if (data != null && data.Length > I2C_MAX_WRITE_LENGTH)
            {
                if (ctrl != ScopeController.AWG)
                    throw new Exception(String.Format("Can't do writes of this length ({0}) to controller {1:G}", data.Length, ctrl));

                int offset = 0;
                byte[] toSend = new byte[32];
                
                //Begin I2C - send start condition
                WriteControlBytes(UsbCommandHeader(ctrl, Operation.WRITE_BEGIN, address, 0));

                while(offset < data.Length)
                {
                    int length = Math.Min(data.Length - offset, I2C_MAX_WRITE_LENGTH_BULK);
                    byte[] header = UsbCommandHeader(ctrl, Operation.WRITE_BODY, address, (uint)length);
                    Array.Copy(header, toSend, header.Length);
                    Array.Copy(data, offset, toSend, header.Length, length);
                    WriteControlBytes(toSend);
                    offset += length;
                }
                WriteControlBytes(UsbCommandHeader(ctrl, Operation.WRITE_END, address, 0));
            }
            else
            {
                uint length = data != null ? (uint)data.Length : 0;
                byte[] header = UsbCommandHeader(ctrl, Operation.WRITE, address, length);

                //Paste header and data together and send it
                byte[] toSend = new byte[header.Length + length];
                Array.Copy(header, toSend, header.Length);
                if (length > 0)
                    Array.Copy(data, 0, toSend, header.Length, data.Length);
                WriteControlBytes(toSend);
            }
        }

        internal void SendCommand(PIC_COMMANDS cmd)
        {
            byte[] toSend = new byte[2] { HEADER_CMD_BYTE, (byte)cmd };
            WriteControlBytes(toSend);
        }

        internal void LoadBootLoader()
        {
            this.SendCommand(PIC_COMMANDS.PIC_BOOTLOADER);
        }

        internal void Reset()
        {
            this.SendCommand(PIC_COMMANDS.PIC_RESET);
        }

        #endregion

        #region helper for header

        private static byte[] UsbCommandHeader(ScopeController ctrl, Operation op, uint address, uint length)
        {
            byte[] header = null;

            if (ctrl == ScopeController.PIC)
            {
                if (op == Operation.WRITE)
                {
                    header = new byte[4] {
                               HEADER_CMD_BYTE,
               (byte)PIC_COMMANDS.PIC_WRITE, 
                            (byte)(address),
                             (byte)(length)  //first I2C byte: FPGA i2c address (5) + '0' as LSB, indicating write operation
                        };
                }
                else if (op == Operation.READ)
                {
                    header = new byte[4] {
                               HEADER_CMD_BYTE,
                (byte)PIC_COMMANDS.PIC_READ, 
                            (byte)(address),
                             (byte)(length)  //first I2C byte: FPGA i2c address (5) + '0' as LSB, indicating write operation
                        };
                }
            }
            else if (ctrl == ScopeController.ROM)
            {
                if (op == Operation.WRITE)
                {
                    header = new byte[4] {
                               HEADER_CMD_BYTE,
            (byte)PIC_COMMANDS.EEPROM_WRITE, 
                            (byte)(address),
                             (byte)(length)
                        };
                }
                else if (op == Operation.READ)
                {
                    header = new byte[4] {
                               HEADER_CMD_BYTE,
             (byte)PIC_COMMANDS.EEPROM_READ, 
                            (byte)(address),
                             (byte)(length)
                        };
                }
            }
            else if (ctrl == ScopeController.FLASH)
            {
                if (op == Operation.WRITE)
                {
                    header = new byte[5] {
                               HEADER_CMD_BYTE,
         (byte)PIC_COMMANDS.FLASH_ROM_WRITE, 
                            (byte)(address),
                             (byte)(length),
                       (byte)(address >> 8),
                        };
                }
                else if (op == Operation.READ)
                {
                    header = new byte[5] {
                               HEADER_CMD_BYTE,
          (byte)PIC_COMMANDS.FLASH_ROM_READ, 
                            (byte)(address),
                             (byte)(length),
                       (byte)(address >> 8),
                        };
                }
            }
            else if (ctrl == ScopeController.FPGA)
            {
                if (op == Operation.WRITE)
                {
                    header = new byte[5] {
                               HEADER_CMD_BYTE,
               (byte)PIC_COMMANDS.I2C_WRITE,
                         (byte)(length + 2), //data and 2 more bytes: the FPGA I2C address, and the register address inside the FPGA
     (byte)(FPGA_I2C_ADDRESS_SETTINGS << 1), //first I2C byte: FPGA i2c address bit shifted and LSB 0 indicating write
                              (byte)address  //second I2C byte: address of the register inside the FPGA
                    };
                }
                else if (op == Operation.READ)
                {
                    header = new byte[4] {
                               HEADER_CMD_BYTE,
                (byte)PIC_COMMANDS.I2C_READ,
          (byte)(FPGA_I2C_ADDRESS_SETTINGS), //first I2C byte: FPGA i2c address bit shifted and LSB 1 indicating read
                             (byte)(length) 
                    };
                }
            }
            else if (ctrl == ScopeController.FPGA_ROM)
            {
                if (op == Operation.WRITE)
                {
                    header = new byte[5] {
                            HEADER_CMD_BYTE,
               (byte)PIC_COMMANDS.I2C_WRITE,
                         (byte)(length + 2), 
    (byte)((FPGA_I2C_ADDRESS_ROM << 1) + 0), //first I2C byte: FPGA i2c address bit shifted and LSB 1 indicating read
                              (byte)address,
                    };
                }
                else if (op == Operation.READ)
                {
                    header = new byte[4] {
                               HEADER_CMD_BYTE,
                (byte)PIC_COMMANDS.I2C_READ,
                (byte)(FPGA_I2C_ADDRESS_ROM), //first I2C byte: FPGA i2c address, not bitshifted
                             (byte)(length) 
                    };
                }
            }
            else if (ctrl == ScopeController.AWG)
            {
                if (op == Operation.WRITE)
                {
                    header = new byte[5] {
                               HEADER_CMD_BYTE,
               (byte)PIC_COMMANDS.I2C_WRITE,
                         (byte)(length + 2), //data and 2 more bytes: the FPGA I2C address, and the register address inside the FPGA
     (byte)(FPGA_I2C_ADDRESS_AWG << 1), //first I2C byte: FPGA i2c address bit shifted and LSB 0 indicating write
                              (byte)address  //second I2C byte: address of the register inside the FPGA
                    };
                }
                if (op == Operation.WRITE_BEGIN)
                {
                    header = new byte[5] {
                            HEADER_CMD_BYTE,
         (byte)PIC_COMMANDS.I2C_WRITE_START,
                         (byte)(length + 2), //data and 2 more bytes: the FPGA I2C address, and the register address inside the FPGA
          (byte)(FPGA_I2C_ADDRESS_AWG << 1), //first I2C byte: FPGA i2c address bit shifted and LSB 0 indicating write
                              (byte)address  //second I2C byte: address of the register inside the FPGA
                    };
                }
                if (op == Operation.WRITE_BODY)
                {
                    header = new byte[3] {
                               HEADER_CMD_BYTE,
             (byte)PIC_COMMANDS.I2C_WRITE_BULK,
                                (byte)(length), //data and 2 more bytes: the FPGA I2C address, and the register address inside the FPGA
                    };
                }
                if (op == Operation.WRITE_END)
                {
                    header = new byte[3] {
                               HEADER_CMD_BYTE,
             (byte)PIC_COMMANDS.I2C_WRITE_STOP,
                             (byte)(length)
                    };
                }
                else if (op == Operation.READ)
                {
                    throw new Exception("Can't read out AWG");
                }
            }
            return header;
        }

        #endregion
    }
}
