﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.Common;
using Android.Hardware.Usb;
using Android.Content;

namespace LabNation.DeviceInterface.Hardware
{
	public class SmartScopeHardwareXamarin : ISmartScopeHardwareUsb
	{
		private const int COMMAND_READ_ENDPOINT_SIZE = 16;
		private const short COMMAND_WRITE_ENDPOINT_SIZE = 32;
		private const int TIMEOUT = 1000;
		private UsbEndpoint dataEndpoint;
		private UsbEndpoint commandReadEndpoint;
		private UsbEndpoint commandWriteEndpoint;
		private UsbDeviceConnection usbConnection;

		public SmartScopeHardwareXamarin(Context context, UsbManager usbManager, UsbDevice device)
		{
			Destroyed = false;
			if (!usbManager.HasPermission(device))
			{
				Logger.Error("Permission denied");
				throw new Exception("Device permission not obtained");
			}

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
			usbConnection = usbManager.OpenDevice(device);
			usbConnection.ClaimInterface(interf, true);
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
			//try to send data
			try
			{
				byte[] buffer;
				if (offset == 0 && length == message.Length)
					buffer = message;
				else {
					buffer = new byte[length];
					Array.ConstrainedCopy(message, offset, buffer, 0, length);
				}
				int bytesWritten = usbConnection.BulkTransfer(commandWriteEndpoint, buffer, buffer.Length, TIMEOUT);
				if (bytesWritten != buffer.Length)
					Logger.Error(String.Format("Writing control bytes failed - wrote {0} out of {1} bytes", bytesWritten, buffer.Length));
			}
			catch (Exception ex)
			{
				Logger.Error("Writing control bytes failed" + ex.Message);
			}
		}

		public void ReadControlBytes(byte[] buffer, int offset, int length)
		{
			if (buffer.Length - offset < length)
				throw new ArgumentException("ReadControlBytes: Buffer too small to accomodate command result");

			try
			{
				//send read command
				byte[] readBuffer = new byte[COMMAND_READ_ENDPOINT_SIZE];
				int bytesRead = usbConnection.BulkTransfer(commandReadEndpoint, readBuffer, length, TIMEOUT);

				if (bytesRead != length)
					throw new ScopeIOException("Failed to read control bytes");

				Array.ConstrainedCopy(readBuffer, 0, buffer, offset, length);
			}
			catch (Exception ex)
			{
				throw new ScopeIOException("Reading control bytes failed" + ex.Message);
			}
		}

		public void GetData(byte[] buffer, int offset, int length)
		{
			try
			{
				byte[] readBuffer = new byte[length];
				int readBytes = usbConnection.BulkTransfer(dataEndpoint, readBuffer, length, TIMEOUT);
				if (readBytes != length)
					throw new ScopeIOException("Didn't read enough bytes");

				Array.ConstrainedCopy(readBuffer, 0, buffer, offset, length);
				return;
			}
			catch (Exception ex)
			{
				throw new ScopeIOException("Streaming data from scope failed " + ex.Message);
			}
		}
		public void FlushDataPipe()
		{
			if (dataEndpoint == null)
				throw new ScopeIOException("Data endpoint is null");

			int length = dataEndpoint.MaxPacketSize;
			byte[] readBuffer = new byte[length];
			usbConnection.BulkTransfer(dataEndpoint, readBuffer, length, 10);
			usbConnection.BulkTransfer(dataEndpoint, readBuffer, length, 10);
			usbConnection.BulkTransfer(dataEndpoint, readBuffer, length, 10);
		}

		public string Serial
		{
			get { return usbConnection.Serial; }
		}

		public bool Destroyed { get; private set; }

		public void Destroy()
		{
			Destroyed = true;
		}


	}
}
