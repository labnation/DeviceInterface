using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LabNation.Common;
using LabNation.DeviceInterface.Net;
using System.IO;

namespace SmartScopeServer
{
    class Program
    {
        static void Main(string[] args)
        {            
            ConsoleLogger consoleLog = new ConsoleLogger(LogLevel.DEBUG);

            Server server = new Server();

            Logger.LogC(LogLevel.INFO, "--- Press any key to stop server ---\n", ConsoleColor.Green);
            Console.ReadKey();

            server.Stop();

            consoleLog.Stop();
        }        
    }
}
