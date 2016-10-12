#define DEBUGCONSOLE

using LabNation.DeviceInterface.Devices;
using LabNation.DeviceInterface.Hardware;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Zeroconf;
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using LabNation.Common;

namespace LabNation.DeviceInterface.Net
{
    public class Monitor
    {
        Thread pollThread;
        List<InterfaceServer> servers = new List<InterfaceServer>();

        public Monitor()
        {
            //start USB polling thread
            pollThread = new Thread(PollUponStart);
            pollThread.Name = "Devicemanager Startup poll";

#if WINUSB
            InterfaceManagerWinUsb.Instance.onConnect += OnInterfaceConnect;
#else
            InterfaceManagerLibUsb.Instance.onConnect += OnInterfaceConnect;
#endif
            pollThread.Start();
            pollThread.Join();
        }

        public void Stop()
        {
            foreach(InterfaceServer s in servers)
            {
                Logger.Info("Stopping server for interface with serial " + s.hwInterface.Serial);
                s.Stop();
            }
        }

        private void PollUponStart()
        {
#if WINUSB
            InterfaceManagerWinUsb.Instance.PollDevice();
#elif !IOS
            InterfaceManagerLibUsb.Instance.PollDevice();
#endif
        }

        private void OnInterfaceConnect(ISmartScopeInterfaceUsb hardwareInterface, bool connected)
        {
            Logger.LogC(LogLevel.INFO, "[Hardware] ", ConsoleColor.Green);
            if (connected)
            {
                Logger.LogC(LogLevel.INFO, "connected\n", ConsoleColor.Gray);
                servers.Add(new InterfaceServer(hardwareInterface));
            }
            else //disconnect
            {
                //Find server with disappeared hw interface
                if (servers.Where(x => x.hwInterface == hardwareInterface).Count() != 0)
                {
                    InterfaceServer s = servers.First(x => x.hwInterface == hardwareInterface);
                    servers.Remove(s);
                    s.Stop();
                    Logger.LogC(LogLevel.INFO, "removed\n", ConsoleColor.Gray);

                }
                else
                {
                    Logger.LogC(LogLevel.INFO, "ignored\n", ConsoleColor.Gray);
                }
            }
        }
    }
}
