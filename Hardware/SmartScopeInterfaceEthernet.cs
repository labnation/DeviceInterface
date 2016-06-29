using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;
using LabNation.DeviceInterface.Net;
using LabNation.DeviceInterface.Hardware;

namespace LabNation.DeviceInterface.Hardware
{
    public delegate void OnInterfaceDisconnect(SmartScopeInterfaceEthernet hardwareInterface);

    public class SmartScopeInterfaceEthernet:ISmartScopeInterface
    {
        
		public bool connected { get { return this.tcpclnt.Connected; } }
        private IPAddress serverIp;
        private int serverPort;
        private OnInterfaceDisconnect onDisconnect;
        BufferedStream stream;
		TcpClient tcpclnt = new TcpClient();
        public SmartScopeInterfaceEthernet(IPAddress serverIp, int serverPort, OnInterfaceDisconnect onDisconnect)
        {
            this.serverIp = serverIp;
            this.serverPort = serverPort;
            this.onDisconnect = onDisconnect;

            this.Connect();
        }
			
        private void Connect()
        {
            tcpclnt.Connect(this.serverIp, this.serverPort);
            NetworkStream unbufferedStream = tcpclnt.GetStream();
            this.stream = new BufferedStream(unbufferedStream, Constants.BUF_SIZE);
        }

        //method encapsulating stream.Read, as this will throw an error upon ungraceful disconnect
        private int ProtectedRead(byte[] array, int offset, int count)
        {
            try
            {
                return stream.Read(array, offset, count);
            }
            catch
            {
                if (onDisconnect != null)
                    onDisconnect(this);

                LabNation.Common.Logger.Warn("EthernetInterface Read error -- probably disconnection");
                return 0;
            }
        }

        public string Serial { 
            get 
            {
                if (!BitConverter.IsLittleEndian)
                    throw new Exception("This system is bigEndian -- not supported!");

                byte[] message = Constants.Commands.SERIAL.msg();
                byte[] answer;
                lock (this)
                {
                    stream.Write(message, 0, message.Length);
					answer = new byte[11];
                    ProtectedRead(answer, 0, answer.Length);
                }

                return System.Text.Encoding.UTF8.GetString(answer, 0, answer.Length);
            } 
        }

        public void WriteControlBytes(byte[] message, bool async)
        {
            byte[] wrapper = Constants.Commands.SEND.msg(message);

            lock (this)
            {
                stream.Write(wrapper, 0, wrapper.Length);
            }
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
            byte[] message = Constants.Commands.READ.msg( new byte[] {(byte)length });
            byte[] answer;
            lock (this)
            {
                stream.Write(message, 0, message.Length);
                stream.Flush();

                answer = new byte[length];

                int offset = 0;
				while(offset < length)
                    offset += ProtectedRead(answer, offset, length - offset);
            }

            return answer;
        }

        public byte[] GetData(int numberOfBytes)
        {
            byte[] message = Constants.Commands.READ_HBW.msg( new byte[] { (byte)(numberOfBytes >> 8), (byte)numberOfBytes });

            byte[] answer;
            lock (this)
            {
                stream.Write(message, 0, message.Length);
                stream.Flush();

                answer = new byte[numberOfBytes];

				int offset = 0;
                while (offset < numberOfBytes)
                {
                    int bytesReceived = ProtectedRead(answer, offset, numberOfBytes - offset);
                    offset += bytesReceived;

                    if (bytesReceived == 0)
                        return null;
                }
            }

            return answer;
        }

        public bool Destroyed { get { return false; } }
        public void Destroy() { }
        public void FlushDataPipe()
        {
            byte[] message = Constants.Commands.FLUSH.msg();
            lock (this)
            {
                stream.Write(message, 0, message.Length);
            }
        }
    }
}
