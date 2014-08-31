using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using Common;
using ECore.HardwareInterfaces;

namespace ECore.Devices {
	partial class ScopeV2 {
		private bool FlashFpga ()
		{
            this.flashed = false;
			int packetSize = 32;//hardwareInterface.WriteControlMaxLength ();
			int packetsPerCommand = 64;

			if (packetSize <= 0)
				return false;

            Common.SerialNumber s = new SerialNumber(this.Serial);
            string fwName = String.Format("SmartScope_{0}", Base36.Encode((long)s.model, 3).ToUpper());

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
#if INTERNAL
                string fileName = String.Format("{1}.bin", fwName);
                firmwareModified = new FileInfo(fileName).LastWriteTime;
                firmware = Utils.FileToByteArray(fileName, packetSize, 0xff);
#else
                firmware = (byte[])Resources.ResourceManager.GetObject(fwName);
#endif
#endif
            } catch (Exception e) {
				Logger.Error("Opening FPGA FW file failed");
				Logger.Error(e.Message);
				return false;
			}
			if(firmware == null) {
				Logger.Error("Failed to read FW");
                return false;
			}
				
			Logger.Info("Got firmware of length " + firmware.Length);

			//Send FW to FPGA
			try {
				Stopwatch flashStopwatch = new Stopwatch ();
				flashStopwatch.Start ();
				String fwModifiedString = Utils.GetPrettyDate (firmwareModified);
				Logger.Debug("Firmware was created " + fwModifiedString);
				UInt16 commands = (UInt16) (firmware.Length / packetSize + killMeNow);
				//PIC: enter FPGA flashing mode
                byte[] msg = new byte[] {
				    ScopeUsbInterface.HEADER_CMD_BYTE,
                    (byte)ScopeUsbInterface.PIC_COMMANDS.PROGRAM_FPGA_START,
				    (byte) (commands >> 8),
				    (byte) (commands),
                };
                hardwareInterface.WriteControlBytes(msg);

                //Flush whatever might be left in the datapipe
                hardwareInterface.FlushDataPipe();
                
				int bytesSent = 0; 
				int commandSize = packetsPerCommand * packetSize;
				while (bytesSent < firmware.Length) {
					if (bytesSent + commandSize > firmware.Length)
						commandSize = firmware.Length - bytesSent;
					byte [] commandBytes = new byte[commandSize];
					Array.Copy (firmware, bytesSent, commandBytes, 0, commandSize);
                    hardwareInterface.WriteControlBytesBulk(commandBytes);
					bytesSent += commandBytes.Length;
					int progress = (int) (bytesSent * 100 / firmware.Length);
				}
				flashStopwatch.Stop ();
				for (int j = 0; j < killMeNow; j++) {
                    hardwareInterface.WriteControlBytesBulk(dummyData);
				}
                
				//Send finish flashing command
                hardwareInterface.SendCommand(ScopeUsbInterface.PIC_COMMANDS.PROGRAM_FPGA_END);
                Logger.Info(String.Format("Flashed FPGA in {0:0.00}s", (double)flashStopwatch.ElapsedMilliseconds / 1000.0));
                this.flashed = true;
			} catch (ScopeIOException e) {
				Logger.Error("Flashing FPGA failed failed");
                Logger.Error(e.Message);
				return false;
			}
            return true;
		}
	}
}
