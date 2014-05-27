using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LibUsbDotNet;
using LibUsbDotNet.Main;
using LibUsbDotNet.LibUsb;

namespace ECore.HardwareInterfaces
{
    internal class ScopeUsbInterface: EDeviceHWInterface, IScopeHardwareInterface, IDisposable
    {
        private enum Operation { READ, WRITE };

        private const int USB_TIMEOUT = 1000;
        private const int COMMAND_READ_ENDPOINT_SIZE = 16;

        private UsbEndpointWriter commandWriteEndpoint;
        private UsbEndpointReader commandReadEndpoint;
        private UsbEndpointReader dataEndpoint;
        private object registerLock = new object();

        internal ScopeUsbInterface(UsbDevice usbDevice)
        {
            bool succes1 = (usbDevice as IUsbDevice).SetConfiguration(1);
            if (!succes1)
                throw new Exception("Failed to set usb device configuration");
            bool succes2 = (usbDevice as IUsbDevice).ClaimInterface(0);
            if (!succes2)
                throw new Exception("Failed to claim usb interface6");
            

            dataEndpoint = usbDevice.OpenEndpointReader(ReadEndpointID.Ep01);
            commandWriteEndpoint = usbDevice.OpenEndpointWriter(WriteEndpointID.Ep02);
            commandReadEndpoint = usbDevice.OpenEndpointReader(ReadEndpointID.Ep03);
            
            Logger.AddEntry(null, LogLevel.Debug, "Created new ScopeUsbInterface");
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
            int bytesWritten;
            ErrorCode code = commandWriteEndpoint.Write(message, USB_TIMEOUT, out bytesWritten);
            if (code != ErrorCode.Success)
                Logger.AddEntry(this, LogLevel.Error, "Failed to write `ontrol bytes : " + code.ToString("G"));
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
                //log
                string logString = "";
                foreach (byte b in readBuffer)
                    logString += b.ToString() + ",";

                //Logger.AddEntry(this, LogMessageType.ECoreInfo, "Answer received from HW: [" + logString + "]");

                //extract required data
                byte[] returnBuffer = new byte[length];
                for (int i = 0; i < length; i++)
                    returnBuffer[i] = readBuffer[i];

                //return read data
                return returnBuffer;
            }
            catch (Exception ex)
            {
                Logger.AddEntry(this, LogLevel.Error, "Reading control bytes failed");
                Logger.AddEntry(this, LogLevel.Error, "ExceptionMessage: " + ex.Message);
                Logger.AddEntry(this, LogLevel.Error, "USB ErrorCode: " + errorCode);
                Logger.AddEntry(this, LogLevel.Error, "requested length: " + length.ToString());

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
                Logger.AddEntry(this, LogLevel.Error, "Streaming data from camera failed");
                Logger.AddEntry(this, LogLevel.Error, "ExceptionMessage: " + ex.Message);
                Logger.AddEntry(this, LogLevel.Error, "USB ErrorCode: " + errorCode);
                Logger.AddEntry(this, LogLevel.Error, "requested length: " + numberOfBytes.ToString());

                return null;
            }
        }

        #region ScopeInterface

        public void GetControllerRegister(ScopeController ctrl, int address, int length, out byte[] data)
        {
            //In case of FPGA (I2C), first write address we're gonna read from to FPGA
            if (ctrl == ScopeController.FPGA || ctrl == ScopeController.FPGA_ROM)
                SetControllerRegister(ctrl, address, null);

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
            //strip away first 4 bytes as these are not data
            data = new byte[length];
            Array.Copy(readback, 4, data, 0, length);
        }

        public void SetControllerRegister(ScopeController ctrl, int address, byte[] data)
        {
            int length = data != null ? data.Length : 0;
            byte[] header = UsbCommandHeader(ctrl, Operation.WRITE, address, length);

            //Paste header and data together and send it
            byte[] toSend = new byte[header.Length + length];
            Array.Copy(header, toSend, header.Length);
            if (length > 0)
                Array.Copy(data, 0, toSend, header.Length, data.Length);
            WriteControlBytes(toSend);
        }

        private static byte[] UsbCommandHeader(ScopeController ctrl, Operation op, int address, int length)
        {
            byte[] header = null;

            if (ctrl == ScopeController.PIC)
            {
                if (op == Operation.WRITE)
                {
                    header = new byte[4] {
                                        123, //message for FPGA
                                          2, //HOST_COMMAND_SET_PIC_REGISTER
                            (byte)(address),
                             (byte)(length)  //first I2C byte: FPGA i2c address (5) + '0' as LSB, indicating write operation
                        };
                }
                else if (op == Operation.READ)
                {
                    header = new byte[4] {
                                        123, //message for FPGA
                                          3, //HOST_COMMAND_GET_PIC_REGISTER
                            (byte)(address),
                             (byte)(length)  //first I2C byte: FPGA i2c address (5) + '0' as LSB, indicating write operation
                        };
                }
            }
            else if (ctrl == ScopeController.FPGA)
            {
                if (op == Operation.WRITE)
                {
                    header = new byte[5] {
                                        123, //message for FPGA
                                         10, //I2C send/read
                         (byte)(length + 2), //data and 2 more bytes: the FPGA I2C address, and the register address inside the FPGA
                             (byte)(5 << 1), //first I2C byte: FPGA i2c address (5) + '0' as LSB, indicating write operation
                              (byte)address  //second I2C byte: address of the register inside the FPGA
                    };
                }
                else if (op == Operation.READ)
                {
                    header = new byte[4] {
                                        123, //message for FPGA
                                         11, //I2C send/read
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
                                        123, //message for FPGA
                                         10, //I2C send/read
                         (byte)(length + 2), //data and 2 more bytes: the FPGA I2C address, and the register address inside the FPGA
                            //FIXME: should be a different address
                             (byte)(5 << 1), //first I2C byte: FPGA i2c address (5) + '0' as LSB, indicating write operation
                              (byte)address  //second I2C byte: address of the register inside the FPGA
                    };
                }
                else if (op == Operation.READ)
                {
                    header = new byte[4] {
                                        123, //message for FPGA
                                         11, //I2C send/read
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
