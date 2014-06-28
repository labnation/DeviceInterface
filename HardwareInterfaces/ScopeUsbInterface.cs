using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LibUsbDotNet;
using LibUsbDotNet.Main;
using LibUsbDotNet.LibUsb;
using Common;

namespace ECore.HardwareInterfaces
{
#if INTERNAL
	public
#else
    internal
#endif
    class ScopeUsbInterface: EDeviceHWInterface, IScopeHardwareInterface, IDisposable
    {
        internal enum PIC_COMMANDS
        {
            PIC_VERSION         =  1,
            PIC_WRITE           =  2,
            PIC_READ            =  3,
            PIC_RESET		    =  4,
            PIC_BOOTLOADER      =  5,
            EEPROM_READ         =  6,
            EEPROM_WRITE        =  7,
            FLASH_ROM_READ      =  8,
            FLASH_ROM_WRITE     =  9,
            I2C_WRITE           = 10,
            I2C_READ		    = 11,
            PROGRAM_FPGA_START  = 12,
            PROGRAM_FPGA_END    = 13,
        }
        internal const byte HEADER_CMD_BYTE = 0xC0; //C0 as in Command
        internal const byte HEADER_RESPONSE_BYTE = 0xAD; //AD as in Answer Dude
        const int FLASH_USER_ADDRESS_MASK = 0x0FFF;

        private enum Operation { READ, WRITE };

        private const int USB_TIMEOUT = 1000;
        private const int COMMAND_READ_ENDPOINT_SIZE = 16;

        private UsbEndpointWriter commandWriteEndpoint;
        private UsbEndpointReader commandReadEndpoint;
        private UsbEndpointReader dataEndpoint;
        private object registerLock = new object();

        internal ScopeUsbInterface(UsbDevice usbDevice)
        {
            if (usbDevice is IUsbDevice)
            {
                bool succes1 = (usbDevice as IUsbDevice).SetConfiguration(1);
                if (!succes1)
                    throw new Exception("Failed to set usb device configuration");
                bool succes2 = (usbDevice as IUsbDevice).ClaimInterface(0);
                if (!succes2)
                    throw new Exception("Failed to claim usb interface6");
            }
            dataEndpoint = usbDevice.OpenEndpointReader(ReadEndpointID.Ep01);
            commandWriteEndpoint = usbDevice.OpenEndpointWriter(WriteEndpointID.Ep02);
            commandReadEndpoint = usbDevice.OpenEndpointReader(ReadEndpointID.Ep03);
            
            Logger.Debug("Created new ScopeUsbInterface");
        }

        public void Dispose()
        {
            //cleanup
        }

        public override int WriteControlMaxLength()
        {
            if (commandWriteEndpoint == null)
                return -1;
            return commandWriteEndpoint.EndpointInfo.Descriptor.MaxPacketSize;
        }

        public override int WriteControlBytes(byte[] message)
        {
            if (message.Length > WriteControlMaxLength())
            {
                Logger.Error("USB message too long for endpoint");
                return 0;
            }
            return WriteControlBytesBulk(message); ;
        }
        public int WriteControlBytesBulk(byte[] message)
        {
            int bytesWritten;
            ErrorCode code = commandWriteEndpoint.Write(message, USB_TIMEOUT, out bytesWritten);
            if (code != ErrorCode.Success)
                Logger.Error("Failed to write control bytes : " + code.ToString("G"));
            return bytesWritten;
        }

        public override byte[] ReadControlBytes(int length)
        {
            //try to read data
            ErrorCode errorCode = ErrorCode.None;
            try
            {
                //send read command
                byte[] readBuffer = new byte[COMMAND_READ_ENDPOINT_SIZE];
                int bytesRead;
                errorCode = commandReadEndpoint.Read(readBuffer, USB_TIMEOUT, out bytesRead);

                //extract required data
                byte[] returnBuffer = new byte[length];
                Array.Copy(readBuffer, returnBuffer, length);

                return returnBuffer;
            }
            catch (Exception ex)
            {
                Logger.Error("Reading control bytes failed");
                Logger.Error("ExceptionMessage: " + ex.Message);
                Logger.Error("USB ErrorCode: " + errorCode);
                Logger.Error("requested length: " + length.ToString());

                return new byte[0];
            }
        }

        public override byte[] GetData(int numberOfBytes)
        {
            //try to read data
            ErrorCode errorCode = ErrorCode.None;
            try
            {
                //send read command
                byte[] readBuffer = new byte[numberOfBytes];
                int bytesRead;
                errorCode = dataEndpoint.Read(readBuffer, USB_TIMEOUT, out bytesRead);
                // Asynchronously check for data
                /*
                UsbTransfer dataReadTransfer;
                errorCode = dataEndpoint.SubmitAsyncTransfer(readBuffer, 0, 4096, 100, out dataReadTransfer);
                if(errorCode != ErrorCode.None) throw new Exception("Failed to send async USB transfer");
                dataReadTransfer.AsyncWaitHandle.WaitOne(200);
                if (!dataReadTransfer.IsCompleted) dataReadTransfer.Cancel();
                errorCode = dataReadTransfer.Wait(out bytesRead);
                dataReadTransfer.Dispose();
                */
                if (bytesRead == 0) return null;

                //return read data
                return readBuffer;
            }
            catch (Exception ex)
            {
                Logger.Error("Streaming data from camera failed");
                Logger.Error("ExceptionMessage: " + ex.Message);
                Logger.Error("USB ErrorCode: " + errorCode);
                Logger.Error("requested length: " + numberOfBytes.ToString());

                return null;
            }
        }

        #region ScopeInterface

        public void GetControllerRegister(ScopeController ctrl, uint address, uint length, out byte[] data)
        {
            //In case of FPGA (I2C), first write address we're gonna read from to FPGA
            //FIXME: this should be handled by the PIC firmware
            if (ctrl == ScopeController.FPGA || ctrl == ScopeController.FPGA_ROM)
                SetControllerRegister(ctrl, address, null);

            if (ctrl == ScopeController.FLASH && (address + length) > (FLASH_USER_ADDRESS_MASK + 1))
            {
                Logger.Error(String.Format("Can't read flash rom beyond 0x{0:X8}", FLASH_USER_ADDRESS_MASK));
                data = null;
                return;
            }

            byte[] header = UsbCommandHeader(ctrl, Operation.READ, address, length);
            this.WriteControlBytes(header);

            //EP3 always contains 16 bytes xxx should be linked to constant
            //FIXME: use endpoint length or so, or don't pass the argument to the function
            byte[] readback = ReadControlBytes(16);
            if (readback == null)
            {
                data = null;
                return;
            }

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
            uint length = data != null ? (uint)data.Length : 0;
            byte[] header = UsbCommandHeader(ctrl, Operation.WRITE, address, length);

            //Paste header and data together and send it
            byte[] toSend = new byte[header.Length + length];
            Array.Copy(header, toSend, header.Length);
            if (length > 0)
                Array.Copy(data, 0, toSend, header.Length, data.Length);
            WriteControlBytes(toSend);
        }

        internal void SendCommand(PIC_COMMANDS cmd)
        {
            byte[] toSend = new byte[2] { HEADER_CMD_BYTE, (byte)cmd };
            WriteControlBytes(toSend);
        }

        public void LoadBootLoader()
        {
            this.SendCommand(PIC_COMMANDS.PIC_BOOTLOADER);
        }
        
        public void Reset()
        {
            this.SendCommand(PIC_COMMANDS.PIC_RESET);
        }

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
                             (byte)(5 << 1), //first I2C byte: FPGA i2c address (5) + '0' as LSB, indicating write operation
                              (byte)address  //second I2C byte: address of the register inside the FPGA
                    };
                }
                else if (op == Operation.READ)
                {
                    header = new byte[4] {
                               HEADER_CMD_BYTE,
                (byte)PIC_COMMANDS.I2C_READ,
                                  (byte)(5), //this has to be i2c address immediately, not bitshifted or anything!
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
                         (byte)(length + 2), //data and 2 more bytes: the FPGA I2C address, and the register address inside the FPGA
                            //FIXME: should be a different address
                             (byte)(5 << 1), //first I2C byte: FPGA i2c address (5) + '0' as LSB, indicating write operation
                              (byte)address  //second I2C byte: address of the register inside the FPGA
                    };
                }
                else if (op == Operation.READ)
                {
                    header = new byte[4] {
                               HEADER_CMD_BYTE,
                (byte)PIC_COMMANDS.I2C_READ,
                                  (byte)(6), //this has to be i2c address immediately, not bitshifted or anything!
                             (byte)(length) 
                    };
                }
            }
            return header;
        }

        #endregion
    }
}
