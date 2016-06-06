//#define DEBUGFILE

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace LabNation.DeviceInterface.Hardware
{
    public class SmartScopeUsbInterfaceEthernet:ISmartScopeUsbInterface
    {
        private bool connected = false;
        private IPAddress serverIp;
        private int serverPort;
#if DEBUGFILE
        StreamWriter debugFile;
#endif
        BufferedStream stream;
        public SmartScopeUsbInterfaceEthernet(IPAddress serverIp, int serverPort)
        {
            this.serverIp = serverIp;
            this.serverPort = serverPort;
            
#if DEBUGFILE
            debugFile = new StreamWriter("debug.txt");
#endif
        }

        private void Connect()
        {
            TcpClient tcpclnt = new TcpClient();
            tcpclnt.Connect(this.serverIp, this.serverPort);
            NetworkStream unbufferedStream = tcpclnt.GetStream();
            this.stream = new BufferedStream(unbufferedStream, 1000000);
        }

        public string Serial { 
            get 
            {
                if (!connected)
                    Connect();

                if (!BitConverter.IsLittleEndian)
                    throw new Exception("This system is bigEndian -- not supported!");

                byte[] message = new byte[3] { 0, 3, 13 };
                byte[] answer;
                lock (this)
                {
                    stream.Write(message, 0, message.Length);


                    if (message[2] == 15)
                    {
                        int fsdsf = 0;
                    }

                    answer = new byte[11];
                    stream.Read(answer, 0, answer.Length);
                }

                return System.Text.Encoding.UTF8.GetString(answer, 0, answer.Length);
            } 
        }

        public void WriteControlBytes(byte[] message, bool async)
        {
            if (!connected)
                Connect();

            if (message.Length == 6)
            {
                if (message[0] == 192 && message[1] == 10 && message[3] == 25 && message[5] == 15)
                {
                    int fsd = 0;
                }
            }

            byte[] wrapper = new byte[message.Length + 3];

            int totalLength = message.Length + 3;
            wrapper[0] = (byte)(totalLength >> 8);
            wrapper[1] = (byte)(totalLength);
            wrapper[2] = 10;
            for (int i = 0; i < message.Length; i++)
                wrapper[i + 3] = message[i];

            lock (this)
            {
                stream.Write(wrapper, 0, wrapper.Length);
            }

            if (wrapper[2] == 15)
            {
                int fsdsf = 0;
            }

            DateTime now = DateTime.Now;
#if DEBUGFILE
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < message.Length; i++)
                sb.Append(i.ToString() + ":" + message[i].ToString("000") + " ");
            debugFile.WriteLine(now.Second + "-" + now.Millisecond + " Written:" + sb);
            debugFile.Flush();
#endif
        }

        public void WriteControlBytesBulk(byte[] message, bool async)
        {
            WriteControlBytes(message, async);
        }

        public void WriteControlBytesBulk(byte[] message, int offset, int length, bool async)
        {
            byte[] buffer;
            if (offset == 0 && length == message.Length)
                buffer = message;
            else
            {
                buffer = new byte[length];
                Array.ConstrainedCopy(message, offset, buffer, 0, length);
            }

            WriteControlBytes(buffer, async);
        }

        public byte[] ReadControlBytes(int length)
        {
            if (!connected)
                Connect();

            DateTime now = DateTime.Now;
#if DEBUGFILE
            debugFile.WriteLine(now.Second + "-" + now.Millisecond + " ReadRequest:" + length.ToString());
            debugFile.Flush();
#endif

            byte[] message = new byte[] { 0, 4, 11, (byte)length };
            byte[] answer;
            lock (this)
            {
                stream.Write(message, 0, message.Length);
                stream.Flush();


                if (message[2] == 15)
                {
                    int fsdsf = 0;
                }

                now = DateTime.Now;
#if DEBUGFILE
                debugFile.WriteLine(now.Second + "-" + now.Millisecond + " Waiting");
                debugFile.Flush();
#endif

                answer = new byte[length];
                stream.Read(answer, 0, length);
            }

            now = DateTime.Now;
#if DEBUGFILE
            debugFile.WriteLine(now.Second + "-" + now.Millisecond + " received:" + BitConverter.ToString(answer));
            debugFile.Flush();
#endif

            return answer;
        }

        public byte[] GetData(int numberOfBytes)
        {
            if (!connected)
                Connect();

            DateTime now = DateTime.Now;
#if DEBUGFILE
            debugFile.WriteLine(now.Second + "-" + now.Millisecond + " Data ReadRequest:" + numberOfBytes.ToString());
            debugFile.Flush();
#endif

            byte[] message = new byte[] { 0, 5, 12, (byte)(numberOfBytes >> 8), (byte)numberOfBytes };
            if (message[2] == 15)
            {
                int fsdsf = 0;
            }

            byte[] answer;
            lock (this)
            {
                stream.Write(message, 0, message.Length);
                stream.Flush();

                now = DateTime.Now;
#if DEBUGFILE
                debugFile.WriteLine(now.Second + "-" + now.Millisecond + " Waiting");
                debugFile.Flush();
#endif

                answer = new byte[numberOfBytes];
                stream.Read(answer, 0, numberOfBytes);
            }

            now = DateTime.Now;
#if DEBUGFILE
            debugFile.WriteLine(now.Second + "-" + now.Millisecond + " received:" + BitConverter.ToString(answer));
            debugFile.Flush();
#endif

            return answer;
        }

        public bool Destroyed { get { return false; } }
        public void Destroy()
        { }
        public void FlushDataPipe()
        {
            if (!connected)
                Connect();

            byte[] message = new byte[] { 0, 3, 14 };
            lock (this)
            {
                stream.Write(message, 0, message.Length);
            }

            if (message[2] == 15)
            {
                int fsdsf = 0;
            }
        }
    }
}
