using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Common;
using Android.Hardware.Usb;

namespace ECore.HardwareInterfaces
{
    class SmartScopeUsbInterfaceXamarin: ISmartScopeUsbInterface
    {
        private const int COMMAND_READ_ENDPOINT_SIZE = 16;
        private UsbEndpoint dataEndpoint;
        private UsbEndpoint commandReadEndpoint;
        private UsbEndpoint commandWriteEndpoint;
        private UsbDeviceConnection usbConnection;

        public SmartScopeUsbInterfaceXamarin(UsbDevice device)
        {
            UsbInterface interf = device.GetInterface(0);
            for (int i = 0; i < interf.EndpointCount; i++)
            {
                if (interf.GetEndpoint(i).EndpointNumber == 1)
                    dataEndpoint = interf.GetEndpoint(i);
                else if (interf.GetEndpoint(i).EndpointNumber == 2)
                    commandWriteEndpoint = interf.GetEndpoint(i);
                else if (interf.GetEndpoint(i).EndpointNumber == 3)
                    commandReadEndpoint = interf.GetEndpoint(i);
            }
            if (!usbManager.HasPermission(device))
            {
                Android.App.PendingIntent pi = Android.App.PendingIntent.GetBroadcast(applicationContext, 0, new Android.Content.Intent("com.android.example.USB_PERMISSION"), 0);
                usbManager.RequestPermission(device, pi);
            }

            int deadCounter = 0;
            while ((deadCounter++ < 10) && (!usbManager.HasPermission(device)))
            {
                Logger.Error("Permission denied");
                System.Threading.Thread.Sleep(500);
            }

            usbConnection = usbManager.OpenDevice(device);
            usbConnection.ClaimInterface(interf, true);
        }

        public override void WriteControlBytesBulk(byte[] message)
        {
            //log
            string logString = "";
            foreach (byte b in message)
                logString += b.ToString() + ",";

            //Logger.AddEntry(this, LogMessageType.ECoreInfo, "Request to send command to HW: [" + logString+"]");

            //see if device is connected properly
            if (commandWriteEndpoint == null)
            {
                Logger.Error("Trying to write to device, but commandWriteEndpoint==null");
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
                Logger.Error("Writing control bytes failed");
            }
        }

        public override byte[] ReadControlBytesBulk(int length)
        {
            //see if device is connected properly
            if (commandReadEndpoint == null)
            {
                Logger.Error("Trying to read from device, but commandReadEndpoint==null");
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
                Logger.Error("Reading control bytes failed");

                return new byte[0];
            }
        }

        public override byte[] GetData(int numberOfBytes)
        {
            //see if device is connected properly
            if (dataEndpoint == null)
            {
                Logger.Error("Trying to stream data from device, but dataEndpoint==null");
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
                    for (int i = 0; i < 10; i++)
                    {
                        dataString = dataString + readBuffer[i].ToString() + ";";
                    }
                }

                //return read data
                return readBuffer;
            }
            catch (Exception ex)
            {
                Logger.Error("Streaming data from scope failed");

                return new byte[0];
            }
        }
        override public void FlushDataPipe()
        {
            if (dataEndpoint == null)
                throw new ScopeIOException("Data endpoint is null");

            //FIXME: needs implementation
        }
    }
}
