using LabNation.Common;
using LabNation.DeviceInterface.Hardware;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace LabNation.DeviceInterface.Net
{
    static class Net
    {
        public const string SERVICE_TYPE = "_sss._tcp";
        public const string REPLY_DOMAIN = "local.";
        public const string TXT_DATA_PORT = "DATA_PORT";
        public const string VERSION = "0.0.0.1";
        public const int BUF_SIZE = 8 * 1024;
        public const int ACQUISITION_PACKET_SIZE = Constants.SZ_HDR + Constants.FETCH_SIZE_MAX;
        public const int DATA_SOCKET_BUFFER_SIZE = ACQUISITION_PACKET_SIZE * 2;
        public const int HDR_SZ = 4;
        public const int TIMEOUT_RX = 5000;
        public const int TIMEOUT_TX = 5000;
        public const int TIMEOUT_CONNECT = 2000;

        //COMMANDS
        public enum Command
        {
            SERIAL = 13,
            GET = 24,
            SET = 25,
            DATA = 26,
            PIC_FW_VERSION = 27,
            FLASH_FPGA = 36,
            ACQUISITION = 52,
            FLUSH = 14,
			DATA_PORT = 42,
            DISCONNECT = 15,
        }

        internal static byte[] msgHeader(this Command command, int len)
        {
            len += HDR_SZ;
            byte[] buf = new byte[len];
            buf[0] = (byte)(len >> 16);
            buf[1] = (byte)(len >> 8);
            buf[2] = (byte)(len);
            buf[3] = (byte)command;

            return buf;
        }

        internal static byte[] msg(this Command command, byte[] data = null, int len = 0)
        {
            len = data == null ? 0 : len > 0 ? len : data.Length;
            byte[] buf = msgHeader(command, len);
            if (data != null && len > 0)
                Array.ConstrainedCopy(data, 0, buf, HDR_SZ, len);

            return buf;
        }

        internal class Message
        {
            public int length;
            public Command command;
            public byte[] data;

            public static Message FromBuffer(byte[] buffer, int offset, int validLength)
            {
                if (validLength < HDR_SZ)
                    return null;

                int length = (buffer[offset] << 16) + (buffer[offset + 1] << 8) + (buffer[offset+2]);
                if (validLength < length)
                    return null;
                if (length == 0)
                    return null;

                Message m = new Message();
                m.length = length;
                m.command = (Command)buffer[offset+3];

                if (length > HDR_SZ)
                {
                    int dataLen = length - HDR_SZ;
                    m.data = new byte[dataLen];
                    Buffer.BlockCopy(buffer, offset+HDR_SZ, m.data, 0, dataLen);
                }
                return m;
            }
        }

        internal static List<Message> ReceiveMessage(Socket socket, byte[] msgBuffer, ref int msgBufferLength)
        {
            int bytesReceived = 0;
			int bytesConsumed = 0;

			List<Message> msgList = new List<Message>();
			while (msgList.Count == 0)
			{
				try
				{

                    int triesLeft = Net.TIMEOUT_RX;
                    while (!socket.Poll(1000, SelectMode.SelectRead)) { }
                    bytesReceived = socket.Receive(msgBuffer, msgBufferLength, msgBuffer.Length - msgBufferLength, SocketFlags.None);
				}
				catch
				{
					Logger.Info("Network connection closed unexpectedly => resetting");
					return null; //in case of non-graceful disconnects (crash, network failure)
				}

#if DEBUGFILE
            DateTime now = DateTime.Now;
            debugFile.WriteLine(now.Second + "-" + now.Millisecond + " Bytes received:" + bytesReceived.ToString());
            debugFile.Flush();
#endif

                if (bytesReceived > msgBuffer.Length)
					throw new Exception("TCP/IP socket buffer overflow!");

				if (bytesReceived == 0) //this would indicate a network error
					return null;

				msgBufferLength += bytesReceived;

				// Parsing message and build list

				while (true)
				{
					Message m = Message.FromBuffer(msgBuffer, bytesConsumed, msgBufferLength - bytesConsumed);
					if (m == null)
						break;

                    //Move remaining valid data to beginning
                    bytesConsumed += m.length;
					msgList.Add(m);
				}
			}
			msgBufferLength -= bytesConsumed;
			Buffer.BlockCopy(msgBuffer, bytesConsumed, msgBuffer, 0, msgBufferLength);
            return msgList;
        }

        internal static byte[] ControllerHeader(Command cmd, ScopeController ctrl, int address, int length, byte[] data = null)
        {
            // 1 byte controller
            // 2 bytes address
            // 2 bytes length
            byte[] res;
            int len = 5;
            if (data != null)
                len += length;

            res = cmd.msgHeader(len);

            int offset = HDR_SZ;
            res[offset++] = (byte)ctrl;
            res[offset++] = (byte)(address >> 8);
            res[offset++] = (byte)(address);
            res[offset++] = (byte)(length >> 8);
            res[offset++] = (byte)(length);

            if(data != null)
                Buffer.BlockCopy(data, 0, res, offset, length);

            return res;
        }
        internal static void ParseControllerHeader(byte[] buffer, out ScopeController ctrl, out int address, out int length, out byte[] data)
        {
            ctrl = (ScopeController)buffer[0];
            address = (buffer[1] << 8) + buffer[2];
            length = (buffer[3] << 8) + buffer[4];
            int dataLength = buffer.Length - 5;
            if (dataLength > 0)
            {
                data = new byte[dataLength];
                Buffer.BlockCopy(buffer, 5, data, 0, dataLength);
            }
            else
                data = null;
        }
    }

}
