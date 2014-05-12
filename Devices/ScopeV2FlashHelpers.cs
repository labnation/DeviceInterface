using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace ECore.Devices {
	partial class ScopeV2 {
		#region flash helpers

		public void FlashPIC2 (StreamReader readerStream)
		{
			//PIC18LF14K50_Flasher picFlasher = new PIC18LF14K50_Flasher(eDevice, readerStream);
		}

		public enum PicFlashResult {
			Success,
			ReadFromRomFailure,
			TrialFailedWrongDataReceived,
			WriteToRomFailure,
			ErrorParsingHexFile,
			FailureDuringVerificationReadback

		}

		public PicFlashResult FlashPIC ()
		{
			int i = 0;
			//////////////////////////////////////////////////////////////////////////////////////////////////////////////
			// Convert HEX file into dictionary
			string fileName = "usb_controller.production.hex";
			StreamReader reader = new StreamReader (fileName);

			Dictionary<uint, byte []> flashData = new Dictionary<uint, byte []> ();
			uint upperAddress = 0;
			while (!reader.EndOfStream) {
				//see http://embeddedfun.blogspot.be/2011/07/anatomy-of-hex-file.html

				string line = reader.ReadLine ();
				ushort bytesInThisLine = Convert.ToUInt16 (line.Substring (1, 2), 16);
				ushort lowerAddress = Convert.ToUInt16 (line.Substring (3, 4), 16);
				ushort contentType = Convert.ToUInt16 (line.Substring (7, 2), 16);

				if (contentType == 00) { //if this is a data record
					byte [] bytes = new byte[bytesInThisLine];
					for (i = 0; i < bytesInThisLine; i++)
						bytes [i] = Convert.ToByte (line.Substring (9 + i * 2, 2), 16);

					flashData.Add (upperAddress + lowerAddress, bytes);
				} else if (contentType == 04) { //contains 2 bytes: the upper address
					upperAddress = Convert.ToUInt32 (line.Substring (9, 4), 16) << 16;
				}
			}
			//////////////////////////////////////////////////////////////////////////////////////////////////////////////

			byte [] sendBytesForUnlock = new byte[] { 123, 5 };
			byte [] sendBytesForFwVersion = new byte[] { 123, 1 };

			//////////////////////////////////////////////////////////////////////////////////////////////////////////////
			//Fetch and print original FW version
			hardwareInterface.WriteControlBytes (sendBytesForFwVersion);
			//System.Threading.Thread.Sleep(100);
			byte [] readFwVersion1 = hardwareInterface.ReadControlBytes (16);
			Console.Write ("Original FW version: ");
			for (i = 2; i < 5; i++)
				Console.Write (readFwVersion1 [i].ToString () + ";");
			Console.WriteLine ();
			//////////////////////////////////////////////////////////////////////////////////////////////////////////////

			//////////////////////////////////////////////////////////////////////////////////////////////////////////////
			//Try to read from dummy location
			//read 8 bytes from location 0x1FC0
			byte [] sendBytesForRead = new byte[5];
			i = 0;
			sendBytesForRead [i++] = 123;    //preamble
			sendBytesForRead [i++] = 7;      //progRom read
			sendBytesForRead [i++] = 31;     //progRom address MSB
			sendBytesForRead [i++] = 192;    //progRom address LSB
			sendBytesForRead [i++] = 8;      //read 8 bytes

			//send over to HW, to perform read operation
			hardwareInterface.WriteControlBytes (sendBytesForRead);

			//now data is stored in EP3 of PIC, so read it
			byte [] readBuffer = hardwareInterface.ReadControlBytes (16);
			if (readBuffer.Length != 16)
				return PicFlashResult.ReadFromRomFailure;
			Console.WriteLine ("Trial read successful");
			//////////////////////////////////////////////////////////////////////////////////////////////////////////////


			//////////////////////////////////////////////////////////////////////////////////////////////////////////////
			//Try unlock-erase-write-read on dummy location
			//unlock            
			hardwareInterface.WriteControlBytes (sendBytesForUnlock);
			//erase            
			byte [] sendBytesForErase = new byte[] { 123, 9, 31, 192 };
			hardwareInterface.WriteControlBytes (sendBytesForErase);
			//write
			byte [] sendBytesForWrite1 = new byte[] {
				123,
				8,
				31,
				192,
				8,
				1,
				0,
				1,
				2,
				3,
				4,
				5,
				6,
				7
			};
			hardwareInterface.WriteControlBytes (sendBytesForWrite1);
			byte [] sendBytesForWrite2 = new byte[] {
				123,
				8,
				31,
				192,
				8,
				0,
				8,
				9,
				10,
				11,
				12,
				13,
				14,
				15
			};
			hardwareInterface.WriteControlBytes (sendBytesForWrite2);
			//readback
			hardwareInterface.WriteControlBytes (sendBytesForRead);
			byte [] readBuffer1 = hardwareInterface.ReadControlBytes (16);
			byte [] sendBytesForRead2 = new byte[] { 123, 7, 31, 200, 8 };
			hardwareInterface.WriteControlBytes (sendBytesForRead2);
			byte [] readBuffer2 = hardwareInterface.ReadControlBytes (16);
			//lock again, in case check crashes
			byte [] sendBytesForLock = new byte[] { 123, 6 };
			//hardwareInterface.WriteControlBytes(sendBytesForLock);

			//check
			for (i = 0; i < 8; i++)
				if (readBuffer1 [5 + i] != i)
					return PicFlashResult.TrialFailedWrongDataReceived;
			for (i = 0; i < 8; i++)
				if (readBuffer2 [5 + i] != 8 + i)
					return PicFlashResult.TrialFailedWrongDataReceived;
			Console.WriteLine ("Trial erase - write - read successful");
			//////////////////////////////////////////////////////////////////////////////////////////////////////////////

			//////////////////////////////////////////////////////////////////////////////////////////////////////////////
			//Full upper memory erase
			//unlock
			hardwareInterface.WriteControlBytes (sendBytesForUnlock);

			//full erase of upper block, done in blocks of 64B at once
			for (i = 0x2000; i < 0x3FFF; i = i + 64) {
				byte addressMSB = (byte) (i >> 8);
				byte addressLSB = (byte) i;
				byte [] sendBytesForBlockErase = new byte[] {
					123,
					9,
					addressMSB,
					addressLSB
				};
				hardwareInterface.WriteControlBytes (sendBytesForBlockErase);
				//Console.WriteLine("Erased memblock 0x" + i.ToString("X"));
			}

			//simple check: read data at 0x2000 -- without erase this is never FF
			byte [] sendBytesForRead3 = new byte[] { 123, 7, 0x20, 0, 8 };
			hardwareInterface.WriteControlBytes (sendBytesForRead3);
			byte [] readBuffer3 = hardwareInterface.ReadControlBytes (16);
			for (i = 0; i < 8; i++)
				if (readBuffer3 [5 + i] != 0xFF)
					return PicFlashResult.TrialFailedWrongDataReceived;
			Console.WriteLine ("Upper memory area erased successfuly");
			//////////////////////////////////////////////////////////////////////////////////////////////////////////////

			//////////////////////////////////////////////////////////////////////////////////////////////////////////////
			//Write full memory area with content read from file
			hardwareInterface.WriteControlBytes (sendBytesForUnlock);
			//prepare packages
			byte [] writePackage1 = new byte[14];
			byte [] writePackage2 = new byte[14];

			foreach (KeyValuePair<uint, byte[]> kvp in flashData) {
				//only flash upper mem area
				if ((kvp.Key >= 0x2000) && (kvp.Key < 0x3FFF)) {
					byte [] byteArr = kvp.Value;

					//fill first packet
					i = 0;
					writePackage1 [i++] = 123;
					writePackage1 [i++] = 8;
					writePackage1 [i++] = (byte) (kvp.Key >> 8);
					writePackage1 [i++] = (byte) (kvp.Key);
					writePackage1 [i++] = 8;
					writePackage1 [i++] = 1; //first data                    
					for (i = 0; i < 8; i++)
						if (byteArr.Length > i)
							writePackage1 [6 + i] = byteArr [i];
						else
							writePackage1 [6 + i] = 0xEE;

					//fill second packet
					i = 0;
					writePackage2 [i++] = 123;
					writePackage2 [i++] = 8;
					writePackage2 [i++] = (byte) (kvp.Key >> 8);
					writePackage2 [i++] = (byte) (kvp.Key);
					writePackage2 [i++] = 8;
					writePackage2 [i++] = 0; //not first data
					byte [] last8Bytes = new byte[8];
					for (i = 0; i < 8; i++)
						if (byteArr.Length > 8 + i)
							writePackage2 [6 + i] = byteArr [8 + i];
						else
							writePackage2 [6 + i] = 0xFF;

					//send first packet
					hardwareInterface.WriteControlBytes (writePackage1);
					//send second packet, including the 16th byte, after which the write actually happens
					hardwareInterface.WriteControlBytes (writePackage2);
				}
			}

			//don't lock here! need to verify memory first.
			//hardwareInterface.WriteControlBytes(sendBytesForLock);

			Console.WriteLine ("Writing of upper memory area finished");
			//////////////////////////////////////////////////////////////////////////////////////////////////////////////

			//////////////////////////////////////////////////////////////////////////////////////////////////////////////
			//Verify by reading back from PIC memory and comparing to contents from file
			foreach (KeyValuePair<uint, byte[]> kvp in flashData) {
				//only flash upper mem area
				if ((kvp.Key >= 0x2000) && (kvp.Key < 0x3FFF)) {
					byte [] byteArr = kvp.Value;

					//read 2 bytes at address
					byte [] sendBytesForVerificationRead1 = new byte[] {
						123,
						7,
						(byte) (kvp.Key >> 8),
						(byte) kvp.Key,
						8
					};
					hardwareInterface.WriteControlBytes (sendBytesForVerificationRead1);
					byte [] readVerificationBytes1 = hardwareInterface.ReadControlBytes (16);

					uint addr = kvp.Key + 8; //need to do this, as there's a possiblity of overflowing
					byte [] sendBytesForVerificationRead2 = new byte[] {
						123,
						7,
						(byte) (addr >> 8),
						(byte) addr,
						8
					};
					hardwareInterface.WriteControlBytes (sendBytesForVerificationRead2);
					byte [] readVerificationBytes2 = hardwareInterface.ReadControlBytes (16);

					//compare
					for (i = 0; i < 8; i++)
						if (byteArr.Length > i)
						if (readVerificationBytes1 [5 + i] != byteArr [i])
							return PicFlashResult.FailureDuringVerificationReadback;
					for (i = 0; i < 8; i++)
						if (byteArr.Length > 8 + i)
						if (readVerificationBytes2 [5 + i] != byteArr [8 + i])
							return PicFlashResult.FailureDuringVerificationReadback;
				}
			}
			Console.WriteLine ("Upper area memory validation passed succesfully!");
			//////////////////////////////////////////////////////////////////////////////////////////////////////////////

			//////////////////////////////////////////////////////////////////////////////////////////////////////////////
			//Lock again!
			hardwareInterface.WriteControlBytes (sendBytesForLock);

			//and print FW version number to console
			hardwareInterface.WriteControlBytes (sendBytesForFwVersion);
			byte [] readFwVersion2 = hardwareInterface.ReadControlBytes (16);
			Console.Write ("New FW version: ");
			for (i = 2; i < 5; i++)
				Console.Write (readFwVersion2 [i].ToString () + ";");
			Console.WriteLine ();
			//////////////////////////////////////////////////////////////////////////////////////////////////////////////

			return PicFlashResult.Success;
		}
		//#if IPHONE
		//#else
		System.Threading.Thread fpgaFlashThread;

		public void FlashHW ()
		{

			if (fpgaFlashThread != null && fpgaFlashThread.IsAlive) {
				Logger.AddEntry (this, LogLevel.Warning, "FPGA already being flashed");
				return;
			}
			fpgaFlashThread = new System.Threading.Thread (FlashFpgaInternal);
			fpgaFlashThread.Start ();
		}

		private void FlashFpgaInternal ()
		{
			int packetSize = 32;//hardwareInterface.WriteControlMaxLength ();
			int packetsPerCommand = 64;

			if (packetSize <= 0)
				return;
			string fileName = "smartscope.bin";

			byte [] firmware = null;
			DateTime firmwareModified = DateTime.Now;
			int killMeNow = 2048 / 8;
            
			//Data to send to keep clock running after all data was sent
			byte [] dummyData = new byte[packetSize];
			for (int i = 0; i < dummyData.Length; i++)
				dummyData [i] = 255;

			//Get FW contents
			try {
#if ANDROID
				Stream inStream;
				BinaryReader reader = null;
				//show all embedded resources
				System.Reflection.Assembly [] assemblies = AppDomain.CurrentDomain.GetAssemblies ();
				for (int assyIndex = 0; assyIndex < assemblies.Length; assyIndex++) {
					if (reader == null) { //dirty patch! otherwise this loop will crash, as there are some assemblies at the end of the list that don't support the following operations and crash
						System.Reflection.Assembly assy = assemblies [assyIndex];
						string [] assetList = assy.GetManifestResourceNames ();
						for (int a = 0; a < assetList.Length; a++) {
							Logger.AddEntry (this, LogMessageType.Persistent, "ER: " + assetList [a]);
							if (assetList [a].Contains (fileName)) {
								inStream = assy.GetManifestResourceStream (assetList [a]);
								reader = new BinaryReader (inStream);
								Logger.AddEntry (this, LogMessageType.Persistent, "Connected to FW Flash file");
								firmware = Utils.BinaryReaderStuffer(reader, inStream.Length, packetSize, 0xff);
							}
						}
					}	
				}

				//show all assets
				/*string[] assetList2 = Assets.List("");
				for (int a=0; a<assetList2.Length; a++) {
					Logger.AddEntry (this, LogMessageType.Persistent, "Asset: "+assetList2[a]);
				}
				//inStream = Assets.Open(fileName, Android.Content.Res.Access.Streaming);
				//reader = new BinaryReader(inStream);
*/

#else
                firmwareModified = new FileInfo(fileName).LastWriteTime;
                firmware = Utils.FileToByteArray(fileName, packetSize, 0xff);
#endif
			} catch (Exception e) {
				Logger.AddEntry (this, LogLevel.Error, "Opening FPGA FW file failed");
				Logger.AddEntry (this, LogLevel.Error, e.Message);
				return;
			}
			if(firmware == null) {
				Logger.AddEntry(this, LogLevel.Error, "Failed to read FW");
			}
				
			Logger.AddEntry(this, LogLevel.Info, "Got firmware of length " + firmware.Length);

			//Send FW to FPGA
			try {
				Stopwatch flashStopwatch = new Stopwatch ();
				flashStopwatch.Start ();
				String fwModifiedString = Utils.GetPrettyDate (firmwareModified);
				Logger.AddEntry(this, LogMessageType.ECoreInfo, "Firmware was created " + fwModifiedString);
				UInt16 commands = (UInt16) (firmware.Length / packetSize + killMeNow);
				//PIC: enter FPGA flashing mode
				byte [] toSend1 = new byte[6];
				int i = 0;
				toSend1 [i++] = 123; //message for PIC
				toSend1 [i++] = 12; //HOST_COMMAND_FLASH_FPGA
				toSend1 [i++] = (byte) (commands >> 8);
				toSend1 [i++] = (byte) (commands);
				hardwareInterface.WriteControlBytes (toSend1);
                
				int bytesSent = 0; 
				int commandSize = packetsPerCommand * packetSize;
				while (bytesSent < firmware.Length) {
					if (bytesSent + commandSize > firmware.Length)
						commandSize = firmware.Length - bytesSent;
					byte [] commandBytes = new byte[commandSize];
					Array.Copy (firmware, bytesSent, commandBytes, 0, commandSize);
					int sent = hardwareInterface.WriteControlBytes (commandBytes);
					if (sent == 0) {
						Logger.AddEntry (this, LogLevel.Error, "No bytes written - aborting flash operation");
						return;
					}
					bytesSent += sent;
					int progress = (int) (bytesSent * 100 / firmware.Length);
					Logger.AddEntry(this, LogLevel.Debug, String.Format ("Flashing FPGA + " + progress.ToString () + "% in {0:0.00}s - " + fwModifiedString, (double) flashStopwatch.ElapsedMilliseconds / 1000.0));
				}
				flashStopwatch.Stop ();
				for (int j = 0; j < killMeNow; j++) {
					hardwareInterface.WriteControlBytes (dummyData);
				}
                
				//Send finish flashing command
				hardwareInterface.WriteControlBytes (new byte[] { 123, 13 });
                Logger.AddEntry(this, LogLevel.Info, String.Format("Flashed FPGA in {0:0.00}s", (double)flashStopwatch.ElapsedMilliseconds / 1000.0));
			} catch (Exception e) {
				Logger.AddEntry (this, LogLevel.Error, "Flashing FPGA failed failed");
                Logger.AddEntry(this, LogLevel.Error, e.Message);
				return;
			}
		}

		#endregion
	}
}
