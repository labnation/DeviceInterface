//#define DEBUGCONSOLE

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
    public class InterfaceServer
    {
        private bool running = true;
        private short port;
        internal ISmartScopeInterfaceUsb hwInterface;

        BandwidthMonitor bwDown = new BandwidthMonitor(new TimeSpan(0, 0, 0, 0, 100));
        BandwidthMonitor bwUp = new BandwidthMonitor(new TimeSpan(0, 0, 0, 0, 100));
        string strBandwidthDown = "";
        string strBandwidthUp = "";
        Thread tcpListenerThread;
        
        public InterfaceServer(ISmartScopeInterfaceUsb hwInterface, short port)
        {
            this.hwInterface = hwInterface;
            this.port = port;
            //start TCP/IP thread
            tcpListenerThread = new Thread(TcpIpController)
            {
                Name = "TCP listener",
            };
            
            tcpListenerThread.Start();            
        }

        public void Stop()
        {
            running = false;
            tcpListener.Stop();
            service.Dispose();
            tcpListenerThread.Join(1000);
        }

        RegisterService service;
        private void PostZeroConf()
        {
            service = new RegisterService();

			service.Name = Dns.GetHostName();
            service.RegType = Constants.SERVICE_TYPE;
            service.ReplyDomain = Constants.REPLY_DOMAIN;
            service.Port = this.port;
            service.Register();

            Logger.LogC(LogLevel.INFO, "[Network] ", ConsoleColor.Yellow);
            Logger.LogC(LogLevel.INFO, "ZeroConf service posted\n", ConsoleColor.Gray);
        }

        class Message
        {
            public int length;
            public Constants.Commands command;
            public byte[] data;
            
            public static Message FromBuffer(byte[] buffer, int validLength)
            {
                if (validLength < 2)
                    return null;
                
                int length = (buffer[0] << 8) + (buffer[1]);
                if (validLength < length)
                    return null;
                if (length == 0)
                    return null;

                Message m = new Message();
                m.length = length;

                if (length > 2)
                    m.command = (Constants.Commands)buffer[2];

                if (length > 3)
                {
                    int dataLen = length - 3;
                    m.data = new byte[dataLen];
                    Buffer.BlockCopy(buffer, 3, m.data, 0, dataLen);
                }
                return m;
            }
        }

        byte[] rxBuffer = new byte[Constants.BUF_SIZE];
        byte[] msgBuffer = new byte[Constants.BUF_SIZE];
        int msgBufferLength = 0;

        List<Message> receiveMessage(Socket socket)
        {
            int bytesReceived = socket.Receive(rxBuffer);
            Logger.Debug(bytesReceived.ToString() + " bytes received");
            
            if (bytesReceived >= rxBuffer.Length)
                throw new Exception("TCP/IP socket buffer overflow!");

            bwDown.Update(bytesReceived, out strBandwidthDown);

            if (bytesReceived == 0)
                return null;

            Buffer.BlockCopy(rxBuffer, 0, msgBuffer, msgBufferLength, bytesReceived);
            msgBufferLength += bytesReceived;
            
            // Parsing message and build list
            List<Message> msgList = new List<Message>();
            while (true)
            {
                Message m = Message.FromBuffer(msgBuffer, msgBufferLength);
                if (m == null)
                    break;
                
                //Move remaining valid data to beginning
                msgBufferLength -= m.length;
                Buffer.BlockCopy(msgBuffer, m.length, msgBuffer, 0, msgBufferLength);                
                msgList.Add(m);
            }
            return msgList;
        }

        TcpListener tcpListener;
        private void TcpIpController()
        {
            tcpListener = new TcpListener(IPAddress.Any, this.port);
            tcpListener.Start();
            Logger.LogC(LogLevel.INFO, "[Network] ", ConsoleColor.Yellow);
            Logger.LogC(LogLevel.INFO, "SmartScope Server listening for incoming connections on port " + this.port.ToString() + "\n", ConsoleColor.Gray);
            
            PostZeroConf();

            Socket socket;
            try
            {
                socket = tcpListener.Server.Accept();
            }
            catch (Exception e)
            {
                Logger.Error("Socket aborted");
                return;
            }

            Logger.LogC(LogLevel.INFO, "[Network] ", ConsoleColor.Yellow);
            Logger.LogC(LogLevel.INFO, "Connection accepted from " + socket.RemoteEndPoint + this.port.ToString() + "\n\n", ConsoleColor.Gray);

            while (running)
            {
                List<Message> msgList = receiveMessage(socket);
                foreach (Message m in msgList)
                {
                    Logger.Debug("MSG [{0:G}] LEN [{1:d}] DATA [{2:s}]", m.command, m.length, BitConverter.ToString(m.data == null || m.data.Length > 32 ? new byte[0] : m.data));
                    Constants.Commands command = m.command;

                    if (command == Constants.Commands.SEND)
                    {
                        SendControlMessage(m.data);
                    }
                    else if (command == Constants.Commands.READ)
                    {
                        byte length = m.data[0];
                        ReadControlBytes(socket, length);
                    }
                    else if (command == Constants.Commands.READ_HBW)
                    {
                        int length = (m.data[0] << 8) + (m.data[1]);
                        ReadHispeedData(socket, length);
                    }
                    else if (command == Constants.Commands.SERIAL)
                    {
						byte[] answer = System.Text.Encoding.UTF8.GetBytes(hwInterface.Serial);

                        socket.Send(answer);

                        bwUp.Update(answer.Length, out strBandwidthUp);
                    }
                    else if (command == Constants.Commands.FLUSH)
                    {
                        hwInterface.FlushDataPipe();
                    }
                    else
                    {
                        throw new Exception(String.Format("Unsupported command {0:G}", command));
                    }
                }
                 
                DateTime time = DateTime.Now;
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("\r" + time.Hour.ToString("00") + ":" + time.Minute.ToString("00") + ":" + time.Second.ToString("00") + ":" + time.Millisecond.ToString("000"));

                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("   Incoming: ");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write(strBandwidthDown + " KB/s");

                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("    Outgoing: ");

                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write(strBandwidthUp + " KB/s              ");

                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine();
            }
        }

        private void ReadHispeedData(Socket socket, int readLength)
        {
            byte[] answer = hwInterface.GetData(readLength);
			if (answer == null) {
				Logger.Error ("Failed to read HSPEED data - null");
			}
			Logger.Debug ("HBW package sending {0:d}/{1:d} bytes", answer.Length, readLength);
            socket.Send(answer);

            bwUp.Update(answer.Length, out strBandwidthUp);
        }

        private void ReadControlBytes(Socket socket, byte readLength)
        {
            byte[] answer = hwInterface.ReadControlBytes(readLength);
			if (answer == null) {
				Logger.Error ("Failed to read data - null");
			}
			Logger.Debug ("Read package sending {0:d}/{1:d} bytes", answer.Length, readLength);
            socket.Send(answer);

            bwUp.Update(answer.Length, out strBandwidthUp);
        }

        private void SendControlMessage(byte[] buffer)
        {
            hwInterface.WriteControlBytesBulk(buffer, false);
        }
    }
}
