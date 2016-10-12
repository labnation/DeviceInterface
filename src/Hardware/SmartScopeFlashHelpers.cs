using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using LabNation.Common;
using LabNation.DeviceInterface.Hardware;

namespace LabNation.DeviceInterface.Hardware {
	static class SmartScopeFlashHelpers {
        public static uint FPGA_VERSION_UNFLASHED = 0xffffffff;

        public static bool FlashFpga(ISmartScopeInterface hardwareInterface, byte[] firmware)
		{
            int packetSize = 32;
			int packetsPerCommand = 64;
			int padding = 2048 / 8;
            
			//Data to send to keep clock running after all data was sent
			byte [] dummyData = new byte[packetSize];
			for (int i = 0; i < dummyData.Length; i++)
				dummyData [i] = 255;

			//Send FW to FPGA
			try {
				Stopwatch flashStopwatch = new Stopwatch ();
				flashStopwatch.Start ();
				UInt16 commands = (UInt16) (firmware.Length / packetSize + padding);
				//PIC: enter FPGA flashing mode
                byte[] msg = new byte[] {
				    SmartScopeInterfaceHelpers.HEADER_CMD_BYTE,
                    (byte)SmartScopeInterfaceHelpers.PIC_COMMANDS.PROGRAM_FPGA_START,
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
				for (int j = 0; j < padding; j++) {
                    hardwareInterface.WriteControlBytesBulk(dummyData, false);
				}
                
				//Send finish flashing command
                hardwareInterface.SendCommand(SmartScopeInterfaceHelpers.PIC_COMMANDS.PROGRAM_FPGA_END);
                Logger.Debug(String.Format("Flashed FPGA in {0:0.00}s", (double)flashStopwatch.ElapsedMilliseconds / 1000.0));
                Logger.Debug("Flushing data pipe");
                //Flush whatever might be left in the datapipe
                hardwareInterface.FlushDataPipe();
			} catch (ScopeIOException e) {
				Logger.Error("Flashing FPGA failed failed");
                Logger.Error(e.Message);
				return false;
			}
            return true;
		}
	}
}
