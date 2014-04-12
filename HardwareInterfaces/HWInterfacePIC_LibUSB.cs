using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
//#if IPHONE
//#else
using LibUsbDotNet;
using LibUsbDotNet.Main;
using LibUsbDotNet.DeviceNotify;
//#endif

namespace ECore.HardwareInterfaces
{
    //class that provides raw HW access to the device
    public class HWInterfacePIC_LibUSB: EDeviceHWInterface, IScopeHardwareInterface
    {
        private enum Operation { READ, WRITE };
        private const int USB_TIMEOUT = 1000;
		private int tempFrameCounter = 0;
		private const int COMMAND_READ_ENDPOINT_SIZE = 16;
		private bool isConnected;
#if ANDROID



		public HWInterfacePIC(){
		}
		public override void WriteControlBytes(byte[] message){
		}
		public override byte[] ReadControlBytes(int length){ return null;
		}
		public override byte[] GetData(int numberOfBytes) { return null;
		}


#else
        //needed for plug-unplug events
        private static IDeviceNotifier UsbDeviceNotifier = DeviceNotifier.OpenDeviceNotifier();

        private UsbDevice scopeUsbDevice;
        private UsbEndpointWriter commandWriteEndpoint;
        private UsbEndpointReader commandReadEndpoint;
        private UsbEndpointReader dataEndpoint;
        

        public HWInterfacePIC_LibUSB()
        {
#if IPHONE || ANDROID
#else
            // Hook the device notifier event
            UsbDeviceNotifier.OnDeviceNotify += OnDeviceNotifyEvent;            
#endif
            //and call the method, to check if device is already connected
            OnDeviceNotifyEvent(null, null);

        }

        //called at init, and each time a system event occurs
        private void OnDeviceNotifyEvent(object sender, DeviceNotifyEventArgs e)
        {           
			UsbRegDeviceList usbDeviceList = UsbDevice.AllDevices;
			Logger.AddEntry (this, LogMessageType.Persistent, "Total number of USB devices attached: "+usbDeviceList.Count.ToString ());
			foreach (UsbRegistry device in usbDeviceList)
			{
				string sAdd = string.Format("Vid:0x{0:X4} Pid:0x{1:X4} (rev:{2}) - {3}",
				                            device.Vid,
				                            device.Pid,
				                            (ushort) device.Rev,
				                            device[SPDRP.DeviceDesc]);

				Logger.AddEntry (this, LogMessageType.Persistent, sAdd);
			}

            //locate USB device
            UsbDeviceFinder scopeUsbFinder = new UsbDeviceFinder(1240, 82);
            scopeUsbDevice = UsbDevice.OpenUsbDevice(scopeUsbFinder);            
            
            //if device is attached
            if (scopeUsbDevice != null)
            {
				Logger.AddEntry(this, LogMessageType.ECoreInfo, "SmartScope connected!");

                //check whether device was already connected, as in that case we don't have to do anything
                if (!isConnected)
                {
					IUsbDevice wholeUsbDevice = scopeUsbDevice as IUsbDevice;
					if (!ReferenceEquals(wholeUsbDevice, null))
					{
						// This is a "whole" USB device. Before it can be used,
						// the desired configuration and interface must be selected.

						// Select config
						bool succes1 = wholeUsbDevice.SetConfiguration(1);


						// Claim interface
						bool succes2 = wholeUsbDevice.ClaimInterface(0);
						Logger.AddEntry (this, LogMessageType.Persistent, "Claim interface: "+succes2.ToString ());
					} 

                    //init endpoints
                    dataEndpoint = scopeUsbDevice.OpenEndpointReader(ReadEndpointID.Ep01);
                    commandWriteEndpoint = scopeUsbDevice.OpenEndpointWriter(WriteEndpointID.Ep02);
                    commandReadEndpoint = scopeUsbDevice.OpenEndpointReader(ReadEndpointID.Ep03);

		            if (commandWriteEndpoint == null)
		            {
			            Logger.AddEntry(this, LogMessageType.Persistent, "commandWriteEndpoint==null");
			            return;
		            }

                    //indicate device is connected
                    isConnected = true;
                }
            }
            else
            {
                //init endpoints
                dataEndpoint = null;
                commandWriteEndpoint = null;
                commandReadEndpoint = null;

                isConnected = false;
                Logger.AddEntry(this, LogMessageType.ECoreInfo, "No device found");
            }
            
        }

        public override int WriteControlMaxLength()
        {
            if (commandWriteEndpoint == null)
                return -1;
            return commandWriteEndpoint.EndpointInfo.Descriptor.MaxPacketSize;
        }

        public override int WriteControlBytes(byte[] message)
        {
            if (!isConnected)
                throw new Exception("Can't write to device since it's not connected");
            int bytesWritten;
            commandWriteEndpoint.Write(message, USB_TIMEOUT, out bytesWritten);
            return bytesWritten;
        }
        
        public override byte[] ReadControlBytes(int length)
        {
            //see if device is connected properly
            if (!isConnected)
            {
                throw new Exception("Can't read from device since it's not connected");
            }

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
                Logger.AddEntry(this, LogMessageType.ECoreError, "Reading control bytes failed");
                Logger.AddEntry(this, LogMessageType.ECoreWarning, "ExceptionMessage: " + ex.Message);
                Logger.AddEntry(this, LogMessageType.ECoreWarning, "USB ErrorCode: " + errorCode);
                Logger.AddEntry(this, LogMessageType.ECoreWarning, "requested length: " + length.ToString());

                return new byte[0];
            }   
        }

        public override byte[] GetData(int numberOfBytes)
        {
            //see if device is connected properly
            if (!isConnected)
            {
                throw new Exception("Can't get device data since it's not connected");
            }

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
				if (tempFrameCounter++ < 10)
				{
					string dataString = "";
					for (int i = 0; i < 10; i++) {
						dataString = dataString + readBuffer[i].ToString ()+";";
					}
                    //This line causes a deadlock on the thread.join() called from the GUI thread - should be async somehow!
					//Logger.AddEntry (this, LogMessageType.ECoreInfo, numberOfBytes.ToString () + " " + errorCode.ToString () + "| Bytes received:" + dataString);
				}

                //return read data
                return readBuffer;
            }
            catch (Exception ex)
            {
                Logger.AddEntry(this, LogMessageType.ECoreError, "Streaming data from camera failed");
                Logger.AddEntry(this, LogMessageType.ECoreWarning, "ExceptionMessage: " + ex.Message);
                Logger.AddEntry(this, LogMessageType.ECoreWarning, "USB ErrorCode: " + errorCode);
                Logger.AddEntry(this, LogMessageType.ECoreWarning, "requested length: " + numberOfBytes.ToString());

                return new byte[0];
            }   
        }
                    
        public virtual void Dispose() 
        {/*
                UsbEndpointReader epReader = this as UsbEndpointReader;
                if (!ReferenceEquals(epReader, null))
                {
                    if (epReader.DataReceivedEnabled) epReader.DataReceivedEnabled = false;
                }
                Abort();
                mUsbDevice.ActiveEndpoints.RemoveFromList(this);
                */
            //scopeUsbDevice.ActiveEndpoints.Remove(commandReadEndpoint);
            //scopeUsbDevice.ActiveEndpoints.Remove(commandWriteEndpoint);
            //scopeUsbDevice.ActiveEndpoints.Remove(dataEndpoint);

        }

        ~HWInterfacePIC_LibUSB()
        {
            //int i = 0;

            if ((dataEndpoint != null) && (!dataEndpoint.IsDisposed))
            {
                /*
                dataEndpoint.Reset();
                commandWriteEndpoint.Reset();
                commandReadEndpoint.Reset();
                */
                if (dataEndpoint != null)
                {
                    dataEndpoint.Abort();
                    dataEndpoint.Dispose();
                    dataEndpoint = null;
                }
                if (commandWriteEndpoint != null)
                {
                    commandWriteEndpoint.Abort();
                    commandWriteEndpoint.Dispose();
                    commandWriteEndpoint = null;
                }

                if (commandReadEndpoint!= null)
                {
                    commandReadEndpoint.Abort();
                    commandReadEndpoint.Dispose();
                    commandReadEndpoint = null;
                }
                
                /*
                scopeUsbDevice.ActiveEndpoints.Remove(commandReadEndpoint);
                scopeUsbDevice.ActiveEndpoints.Remove(commandWriteEndpoint);
                scopeUsbDevice.ActiveEndpoints.Remove(dataEndpoint);
                */
                /*
                dataEndpoint = scopeUsbDevice.OpenEndpointReader(ReadEndpointID.Ep01);
                commandWriteEndpoint = scopeUsbDevice.OpenEndpointWriter(WriteEndpointID.Ep02);
                commandReadEndpoint = scopeUsbDevice.OpenEndpointReader(ReadEndpointID.Ep03);
                */
                if (scopeUsbDevice == null) return;
                
                scopeUsbDevice.Close();
                scopeUsbDevice = null;
                
                UsbDeviceNotifier.Enabled = false;
                UsbDevice.Exit();
            }
        }
#endif       
		public override bool Connected
		{
			get { return isConnected; }
		}

		public override void Stop()
		{            
		}

		public override bool Start()
		{
            return this.Connected;
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

            //strip away first 4 bytes as these are not data
            data = new byte[length];
            Array.Copy(readback, 4, data, 0, readback.Length - 4);
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

            if(ctrl == ScopeController.PIC) 
            {
                if(op == Operation.WRITE) 
                {
                    header = new byte[4] {
                                        123, //message for FPGA
                                          2, //HOST_COMMAND_SET_PIC_REGISTER
                            (byte)(address),
                             (byte)(length)  //first I2C byte: FPGA i2c address (5) + '0' as LSB, indicating write operation
                        };
                }
                else if(op == Operation.READ) {
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
