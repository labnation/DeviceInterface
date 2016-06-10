using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LabNation.Common;
using LabNation.DeviceInterface.Net;
using System.IO;
#if WINDOWS
using System.Windows.Forms;
#endif

namespace SmartScopeServer
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            ConsoleLogger consoleLog = new ConsoleLogger(LogLevel.DEBUG);

            Monitor interfaceMonitor = new Monitor();

            Logger.LogC(LogLevel.INFO, "--- Press any key to stop server ---\n", ConsoleColor.Green);
            
#if WINDOWS
            //Need the Application thread to enable winusb device detection
            Application.EnableVisualStyles();
#endif
            while (true)
            {
#if WINDOWS
                Application.DoEvents();
#endif
                System.Threading.Thread.Sleep(60);
                if (Console.KeyAvailable)
                    break;
            }
            
            interfaceMonitor.Stop();

            consoleLog.Stop();
        }        
    }
}
