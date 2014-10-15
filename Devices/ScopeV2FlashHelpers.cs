using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using Common;
using ECore.HardwareInterfaces;
using System.Reflection;

namespace ECore.Devices {
	partial class SmartScope {
		private bool FlashFpga ()
		{
            this.flashed = false;
			int packetSize = 32;//hardwareInterface.WriteControlMaxLength ();
			int packetsPerCommand = 64;

			if (packetSize <= 0)
				return false;

            string fwName;
            try
            {
                Common.SerialNumber s = new SerialNumber(this.Serial);
				fwName = String.Format("SmartScope_{0}.bin", Base36.Encode((long)s.model, 3).ToUpper());
            }
            catch (Exception e)
            {
                return false;
            }

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
				Stream str = context.Assets.Open(fwName);
                List<byte> fw = new List<byte>();
                while(true) {    
                        byte[] buffer = new byte[1024];
                        int read = str.Read(buffer, 0, buffer.Length);
                    if(read == 0) break;
                    fw.AddRange(buffer.Take(read));
                }
                firmware = fw.ToArray();
                #elif IOS
				System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
				for (int assyIndex = 0; assyIndex < assemblies.Length; assyIndex++) {
					try{
						System.Reflection.Assembly assy = assemblies[assyIndex];
						string[] assetList = assy.GetManifestResourceNames();
						for (int a=0; a<assetList.Length; a++) {
							try{
								string unescapedName = assetList[a].Replace("__","_");
								if (unescapedName.Contains(fwName))
								{
									Stream inStream = assy.GetManifestResourceStream(assetList[a]);
									BinaryReader reader = new BinaryReader(inStream);
									firmware = reader.ReadBytes((int)reader.BaseStream.Length);
									Logger.Info ("Connected to FW Flash file");
								}
							}catch{
								Logger.Error("Exception while going through assetlist");
							}
						}
					}	catch{
						Logger.Error ("Exception while going through assemblylist");
					}
				}                
                #else
				Assembly ass = Assembly.GetExecutingAssembly();

				using(Stream s = ass.GetManifestResourceStream(String.Format("{0}.{1}", ass.GetName().Name, fwName)))
				using(BinaryReader r = new BinaryReader(s))
					firmware = r.ReadBytes((int)s.Length);
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
				    SmartScopeUsbInterfaceHelpers.HEADER_CMD_BYTE,
                    (byte)SmartScopeUsbInterfaceHelpers.PIC_COMMANDS.PROGRAM_FPGA_START,
				    (byte) (commands >> 8),
				    (byte) (commands),
                };
                hardwareInterface.WriteControlBytes(msg, false);
                hardwareInterface.FlushDataPipe();

				int bytesSent = 0; 
				int commandSize = packetsPerCommand * packetSize;
				while (bytesSent < firmware.Length) {
					if (bytesSent + commandSize > firmware.Length)
						commandSize = firmware.Length - bytesSent;
                    hardwareInterface.WriteControlBytesBulk(firmware, bytesSent, commandSize, false);
					bytesSent += commandSize;
				}
				flashStopwatch.Stop ();
				for (int j = 0; j < killMeNow; j++) {
                    hardwareInterface.WriteControlBytesBulk(dummyData, false);
				}
                
				//Send finish flashing command
                hardwareInterface.SendCommand(SmartScopeUsbInterfaceHelpers.PIC_COMMANDS.PROGRAM_FPGA_END);
                Logger.Info(String.Format("Flashed FPGA in {0:0.00}s", (double)flashStopwatch.ElapsedMilliseconds / 1000.0));
                Logger.Info("Flushing data pipe");
                //Flush whatever might be left in the datapipe
                hardwareInterface.FlushDataPipe();
                
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
