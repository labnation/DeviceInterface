using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LabNation.DeviceInterface.Net
{
    static class Constants
    {
        public const string SERVICE_TYPE = "_sss._tcp";
        public const string REPLY_DOMAIN = "local.";
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

        internal static byte[] makeMessage(Constants.Commands command, byte[] data = null)
        {
            int len = 3;
            if (data != null)
                len += data.Length;

            byte[] buf = new byte[len];
            buf[0] = (byte)(len >> 8);
            buf[1] = (byte)(len);
            buf[2] = (byte)command;

            if (data != null)
                Array.ConstrainedCopy(data, 0, buf, 3, data.Length);

            return buf;
        }

        internal static byte[] msg(this Commands c)
        {
            return makeMessage(c);
        }
    }

}
