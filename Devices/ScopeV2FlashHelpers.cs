using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using Common;
using ECore.HardwareInterfaces;

namespace ECore.Devices {
	partial class SmartScope {
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
                firmware = (byte[])Resources.ResourceManager.GetObject(fwName);
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

                //Flush whatever might be left in the datapipe
                hardwareInterface.FlushDataPipe();
                
				int bytesSent = 0; 
				int commandSize = packetsPerCommand * packetSize;
				while (bytesSent < firmware.Length) {
					if (bytesSent + commandSize > firmware.Length)
						commandSize = firmware.Length - bytesSent;
					byte [] commandBytes = new byte[commandSize];
					Array.Copy (firmware, bytesSent, commandBytes, 0, commandSize);
                    hardwareInterface.WriteControlBytesBulk(commandBytes, false);
					bytesSent += commandBytes.Length;
					int progress = (int) (bytesSent * 100 / firmware.Length);
				}
				flashStopwatch.Stop ();
				for (int j = 0; j < killMeNow; j++) {
                    hardwareInterface.WriteControlBytesBulk(dummyData, false);
				}
                
				//Send finish flashing command
                hardwareInterface.SendCommand(SmartScopeUsbInterfaceHelpers.PIC_COMMANDS.PROGRAM_FPGA_END);
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
