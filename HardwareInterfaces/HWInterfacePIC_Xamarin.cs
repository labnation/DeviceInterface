using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.Hardware.Usb;
//using Android.Content;
//using Android;

namespace ECore.HardwareInterfaces
{
    //class that provides raw HW access to the device
    public class HWInterfacePIC_Xamarin: EDeviceHWInterface
    {
		private int tempFrameCounter = 0;
		private const int COMMAND_READ_ENDPOINT_SIZE = 16;
		private bool isConnected;
		//public Android.Content.Context applicationContext;
		private UsbDeviceConnection usbConnection;
		private UsbEndpoint dataEndpoint;
		private UsbEndpoint commandReadEndpoint;
		private UsbEndpoint commandWriteEndpoint;
		private bool hwFlashed = false;

		//public HWInterfacePIC_Xamarin(){
		//}
		/*
		public override void WriteControlBytes(byte[] message){
		}
		public override byte[] ReadControlBytes(int length){ return null;
		}
		public override byte[] GetData(int numberOfBytes) { return null;
		}*/



        //needed for plug-unplug events
		//        private static IDeviceNotifier UsbDeviceNotifier = DeviceNotifier.OpenDeviceNotifier();

		private EDeviceImplementation deviceImplementation;
		//private UsbEndpointWriter commandWriteEndpoint;
		//private UsbEndpointReader commandReadEndpoint;
		//private UsbEndpointReader dataEndpoint;
        

		public HWInterfacePIC_Xamarin(EDeviceImplementation deviceImplementation)
        {
#if IPHONE || ANDROID
#else
            // Hook the device notifier event
            UsbDeviceNotifier.OnDeviceNotify += OnDeviceNotifyEvent;            
#endif
            //and call the method, to check if device is already connected
			//OnDeviceNotifyEvent(null, null);
			this.deviceImplementation = deviceImplementation;
        }

        //called at init, and each time a system event occurs
		public void Initialize(Android.Content.Context applicationContext)
        { 
			//list all available USB devices
			UsbManager usbManager = (UsbManager)applicationContext.GetSystemService(Android.Content.Context.UsbService);
			IDictionary<string, UsbDevice> usbDeviceList = usbManager.DeviceList;
			//Logger.AddEntry (this, LogMessageType.Persistent, "Total number of USB devices attached: "+usbDeviceList.Count.ToString ());

			UsbDevice smartScope = null;
			for (int i = 0; i < usbDeviceList.Count; i++) {
				UsbDevice usbDevice = usbDeviceList.ElementAt(i).Value;
				string sAdd = string.Format("Vid:0x{0:X4} Pid:0x{1:X4} - {2}",
					usbDevice.VendorId,
					usbDevice.ProductId,
					usbDevice.DeviceName);
				//Logger.AddEntry (this, LogMessageType.Persistent, sAdd);

				if ((usbDevice.VendorId == 1240) && (usbDevice.ProductId == 82)) {
					Logger.AddEntry (this, LogMessageType.Persistent, "SmartScope connected!");
					smartScope = usbDevice;
				}
			}
			            
            //if device is attached
			if (smartScope != null)
            {
				Logger.AddEntry(this, LogMessageType.ECoreInfo, "Device attached to USB port");

                //check whether device was already connected, as in that case we don't have to do anything
                if (!isConnected)
                {
					UsbInterface interf = smartScope.GetInterface (0);
					for (int i = 0; i < interf.EndpointCount; i++) {
						if (interf.GetEndpoint(i).EndpointNumber == 1)
							dataEndpoint = interf.GetEndpoint(i);
						else if (interf.GetEndpoint(i).EndpointNumber == 2)
							commandWriteEndpoint = interf.GetEndpoint(i);
						else if (interf.GetEndpoint(i).EndpointNumber == 3)
							commandReadEndpoint = interf.GetEndpoint(i);
					}
					if (!usbManager.HasPermission(smartScope))
					{
						Android.App.PendingIntent pi = Android.App.PendingIntent.GetBroadcast(applicationContext, 0, new Android.Content.Intent("com.android.example.USB_PERMISSION"), 0);
						usbManager.RequestPermission(smartScope, pi);
					}

					int deadCounter = 0;
					while ((deadCounter++ < 10) && (!usbManager.HasPermission (smartScope))) {
						Logger.AddEntry (this, LogMessageType.ECoreInfo, "Permission denied");
						System.Threading.Thread.Sleep (500);
					}

					usbConnection = usbManager.OpenDevice (smartScope);
					usbConnection.ClaimInterface(interf, true);

					//if (dataEndpoint != null) Logger.AddEntry (this, LogMessageType.Persistent, "EP1 connected");
					//if (commandWriteEndpoint != null) Logger.AddEntry (this, LogMessageType.Persistent, "EP2 connected");
					//if (commandReadEndpoint != null) Logger.AddEntry (this, LogMessageType.Persistent, "EP3 connected");

                    //indicate device is connected
					isConnected = true;                    
                }
            }
            else
            {
                isConnected = false;
                Logger.AddEntry(this, LogMessageType.ECoreInfo, "No device found");
            }
            
        }

        public override void WriteControlBytes(byte[] message)
        {
            //log
            string logString = "";
            foreach (byte b in message)
                logString += b.ToString() + ",";

            //Logger.AddEntry(this, LogMessageType.ECoreInfo, "Request to send command to HW: [" + logString+"]");

            //see if device is connected properly
            if (commandWriteEndpoint == null)
            {
                Logger.AddEntry(this, LogMessageType.ECoreWarning, "Trying to write to device, but commandWriteEndpoint==null");
                return;
            }

            //try to send data
            try
            {
                int bytesWritten;
				usbConnection.BulkTransfer(commandWriteEndpoint, message, message.Length, 5000);        
            }
            catch (Exception ex)
            {
                Logger.AddEntry(this, LogMessageType.ECoreError, "Writing control bytes failed");
            }   
        }
        
        public override byte[] ReadControlBytes(int length)
        {
            //see if device is connected properly
            if (commandReadEndpoint == null)
            {
                Logger.AddEntry(this, LogMessageType.ECoreWarning, "Trying to read from device, but commandReadEndpoint==null");
                return new byte[0];
            }

            //try to read data
            try
            {
                //send read command
                byte[] readBuffer = new byte[COMMAND_READ_ENDPOINT_SIZE];
                int bytesRead;
				usbConnection.BulkTransfer(commandReadEndpoint, readBuffer, length, 5000);

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

                return new byte[0];
            }   
        }

        public override byte[] GetData(int numberOfBytes)
        {
            //see if device is connected properly
            if (dataEndpoint == null)
            {
                Logger.AddEntry(this, LogMessageType.ECoreWarning, "Trying to stream data from device, but dataEndpoint==null");
                return new byte[0];
            }

            //try to read data
            try
            {
                //send read command
                byte[] readBuffer = new byte[numberOfBytes];
                int bytesRead;
				usbConnection.BulkTransfer(dataEndpoint, readBuffer, numberOfBytes, 10000);

				if (tempFrameCounter++ < 10)
				{
					string dataString = "";
					for (int i = 0; i < 10; i++) {
						dataString = dataString + readBuffer[i].ToString ()+";";
					}
				}

                //return read data
                return readBuffer;
            }
            catch (Exception ex)
            {
                Logger.AddEntry(this, LogMessageType.ECoreError, "Streaming data from camera failed");

                return new byte[0];
            }   
        }
                    
        public virtual void Dispose() 
        {
		}

		public override bool Connected
		{
			get { /*
				//if (isConnected) 
				{
					if (commandWriteEndpoint != null) {
						if (!hwFlashed) {
							deviceImplementation.FlashHW ();
							hwFlashed = true;
						}
					}
				}*/

				return isConnected; 
			}
		}

		public override void StartInterface()
		{            
		}

		public override void StopInterface()
		{
		}
    }
}
