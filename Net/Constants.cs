using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LabNation.DeviceInterface.Net
{
    class Constants
    {
        public const string SERVICE_NAME = "SmartScopeServer";
        public const string SERVICE_TYPE = "_sss._tcp";
        public const string REPLY_DOMAIN = "local.";
        public const int PORT = 25482;
        public const string VERSION = "0.0.0.1";
        public const int BUF_SIZE = 1024 * 1024;

        //COMMANDS
        public enum Commands
        {
            SEND = 10,
            READ = 11,
            READ_HBW = 12,
            SERIAL = 13,
            FLUSH = 14,
        }
    }
}
