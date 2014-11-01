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
        private uint FPGA_VERSION_UNFLASHED = 0xffffffff;
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
            catch (Exception)
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
				firmware = Resources.Load(fwName);
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

                //FIXME: this sleep is found necessary on android tablets.
                /* The problem occurs when a scope is initialised the *2nd*
                 * time after the app starts, i.e. after replugging it.
                 * A possible explanation is that in the second run, caches
                 * are hit and the time between the PROGRAM_FPGA_START command
                 * and the first bitstream bytes is smaller than on the first run.
                 * 
                 * Indeed, if this time is smaller than the time for the INIT bit
                 * (see spartan 6 ug380 fig 2.4) to rise, the first bitstream data
                 * is missed and the configuration fails.
                 */
                System.Threading.Thread.Sleep(10);
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
                Logger.Debug(String.Format("Flashed FPGA in {0:0.00}s", (double)flashStopwatch.ElapsedMilliseconds / 1000.0));
                Logger.Debug("Flushing data pipe");
                //Flush whatever might be left in the datapipe
                hardwareInterface.FlushDataPipe();
                if(GetFpgaFirmwareVersion() == FPGA_VERSION_UNFLASHED) {
                    Logger.Error("Got firmware version of unflashed FPGA");
                    return false;
                }
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
