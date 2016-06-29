#define DEBUGFILE

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
#if DEBUGFILE
        StreamWriter debugFile;
#endif
        private bool running = true;
        internal ISmartScopeInterfaceUsb hwInterface;
        private const int RECEIVE_TIMEOUT = 10000; //10sec

        BandwidthMonitor bwDown = new BandwidthMonitor(new TimeSpan(0, 0, 0, 0, 100));
        BandwidthMonitor bwUp = new BandwidthMonitor(new TimeSpan(0, 0, 0, 0, 100));
        string strBandwidthDown = "";
        string strBandwidthUp = "";
        Thread tcpListenerThread;
        
        public InterfaceServer(ISmartScopeInterfaceUsb hwInterface)
        {
#if DEBUGFILE
            debugFile = new StreamWriter("debug.txt");
#endif
            this.hwInterface = hwInterface;
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
        private void RegisterZeroConf()
        {
            service = new RegisterService();

			service.Name = Dns.GetHostName();
            service.RegType = Constants.SERVICE_TYPE;
            service.ReplyDomain = Constants.REPLY_DOMAIN;
			service.Port = (short)(((IPEndPoint)tcpListener.LocalEndpoint).Port);
            service.Register();

            LogMessage(LogTypes.ZEROCONF, "ZeroConf service posted");
        }
        private void UnregisterZeroConf()
        {
            if (service != null)
                service.Dispose();

            LogMessage(LogTypes.ZEROCONF, "ZeroConf service retracted");
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

        //null returned means network error
        List<Message> ReceiveMessage(Socket socket)
        {
            int bytesReceived = 0;
            try
            {
                bytesReceived = socket.Receive(rxBuffer);
            }
            catch
            {
                LogMessage(LogTypes.NETWORK, "Network connection closed unexpectedly => resetting");
                return null; //in case of non-graceful disconnects (crash, network failure)
            }
            
#if DEBUGFILE
            DateTime now = DateTime.Now;
            debugFile.WriteLine(now.Second + "-" + now.Millisecond + " Bytes received:" + bytesReceived.ToString());
            debugFile.Flush();
#endif
            
            if (bytesReceived >= rxBuffer.Length)
                throw new Exception("TCP/IP socket buffer overflow!");

            bwDown.Update(bytesReceived, out strBandwidthDown);

            if (bytesReceived == 0) //this would indicate a network error
            {
                LogMessage(LogTypes.NETWORK, "Nothing received from network socket => resetting");
                return null;
            }

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
            bool disconnect;


#if DEBUGFILE
            tcpListener = new TcpListener(IPAddress.Any, 25482);
#else
        tcpListener = new TcpListener(IPAddress.Any, this.port);
#endif


            while (running)
            {
                tcpListener.Start();
                LogMessage(LogTypes.DECORATION, "==================== New session started =======================");
                LogMessage(LogTypes.NETWORK, "SmartScope Server listening for incoming connections on port " + ((IPEndPoint)tcpListener.LocalEndpoint).Port.ToString());                

                RegisterZeroConf();

                Socket socket;
                try
                {
                    socket = tcpListener.Server.Accept();
                    socket.ReceiveTimeout = RECEIVE_TIMEOUT;
                }
                catch (Exception e)
                {
                    Logger.Error("Socket aborted");
                    return;
                }

                LogMessage(LogTypes.NETWORK, "Connection accepted from " + socket.RemoteEndPoint);
                UnregisterZeroConf();

                disconnect = false;
                while (running && !disconnect)
                {
                    List<Message> msgList = ReceiveMessage(socket);

                    if (msgList != null) //if no network error
                    {
                        foreach (Message m in msgList)
                        {
#if DEBUGFILE
                            {
                                DateTime now = DateTime.Now;
                                StringBuilder sb = new StringBuilder();
                                sb.Append(now.Second + "-" + now.Millisecond + " Command: ");
                                if (m.command != null)
                                    sb.Append(m.command.ToString());
                                if (m.data != null)
                                {
                                    sb.Append("  Data: ");
                                    for (int i = 0; i < m.data.Length; i++)
                                        sb.Append(i.ToString() + ":" + m.data[i].ToString("000") + " ");
                                }
                                debugFile.WriteLine(sb);
                                debugFile.Flush();
                            }
#endif
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

#if DEBUGFILE
                                {
                                    DateTime now = DateTime.Now;
                                    debugFile.WriteLine(now.Second + "-" + now.Millisecond + " Before send");
                                    debugFile.Flush();
                                }
#endif

                                socket.Send(answer);
#if DEBUGFILE
                                {
                                    DateTime now = DateTime.Now;
                                    debugFile.WriteLine(now.Second + "-" + now.Millisecond + " After send");
                                    debugFile.Flush();
                                }
#endif

                                bwUp.Update(answer.Length, out strBandwidthUp);
                            }
                            else if (command == Constants.Commands.FLUSH)
                            {
                                hwInterface.FlushDataPipe();
                            }
                            else if (command == Constants.Commands.DISCONNECT)
                            {
                                hwInterface.FlushDataPipe();
                                disconnect = true;

                                LogMessage(LogTypes.NETWORK, "Request to disconnect from " + socket.RemoteEndPoint);
                            }
                            else
                            {
                                throw new Exception(String.Format("Unsupported command {0:G}", command));
                            }
                        }

                        bandwidthPrintedLast = true;
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
                    }
                    else
                    {
                        disconnect = true;
                        LogMessage(LogTypes.NETWORK, "Connection closed");
                        socket.Close();
                        tcpListener.Stop();
                        System.Threading.Thread.Sleep(500);
                    }
                }
            }
        }

        enum LogTypes
        {
            NETWORK,
            ZEROCONF,
            DECORATION,
        }
        bool bandwidthPrintedLast = false;        
        private void LogMessage(LogTypes logType, string message)
        {
            if (bandwidthPrintedLast)
                Logger.LogC(LogLevel.INFO, "\n", ConsoleColor.Yellow);

            switch (logType)
            {
                case LogTypes.NETWORK:
                    Logger.LogC(LogLevel.INFO, "[Network ] ", ConsoleColor.Yellow);
                    break;
                case LogTypes.ZEROCONF:
                    Logger.LogC(LogLevel.INFO, "[ZeroConf] ", ConsoleColor.Cyan);
                    break;
                default:
                    break;
            }

            Logger.LogC(LogLevel.INFO, message + "\n", ConsoleColor.Gray);

            bandwidthPrintedLast = false;
        }

        private void ReadHispeedData(Socket socket, int readLength)
        {
#if DEBUGFILE
            {
                DateTime now = DateTime.Now;
                debugFile.WriteLine(now.Second + "-" + now.Millisecond + " Before GetHiSpeedData "+readLength.ToString());
                debugFile.Flush();
            }
#endif 

            byte[] answer = hwInterface.GetData(readLength);
			if (answer == null) {
				Logger.Error ("Failed to read HSPEED data - null");
			}
#if DEBUGFILE
            {
                DateTime now = DateTime.Now;
                debugFile.WriteLine(now.Second + "-" + now.Millisecond + " Before netsend HiSpeedData");
                debugFile.Flush();
            }
#endif 

            socket.Send(answer);
#if DEBUGFILE
            {
                DateTime now = DateTime.Now;
                debugFile.WriteLine(now.Second + "-" + now.Millisecond + " After netsend HiSpeedData");
                debugFile.Flush();
            }
#endif 


            bwUp.Update(answer.Length, out strBandwidthUp);
        }

        private void ReadControlBytes(Socket socket, byte readLength)
        {
#if DEBUGFILE
            {
                DateTime now = DateTime.Now;
                debugFile.WriteLine(now.Second + "-" + now.Millisecond + " Before GetControlBytes "+readLength.ToString());
                debugFile.Flush();
            }
#endif 

            byte[] answer = hwInterface.ReadControlBytes(readLength);
			if (answer == null) {
				Logger.Error ("Failed to read data - null");
			}
			Logger.Debug ("Read package sending {0:d}/{1:d} bytes", answer.Length, readLength);
#if DEBUGFILE
            {
                DateTime now = DateTime.Now;
                debugFile.WriteLine(now.Second + "-" + now.Millisecond + " Before netsend ControlBytes");
                debugFile.Flush();
            }
#endif 

            socket.Send(answer);
#if DEBUGFILE
            {
                DateTime now = DateTime.Now;
                debugFile.WriteLine(now.Second + "-" + now.Millisecond + " After netsend ControlBytes");
                debugFile.Flush();
            }
#endif 


            bwUp.Update(answer.Length, out strBandwidthUp);
        }

        private void SendControlMessage(byte[] buffer)
        {
            hwInterface.WriteControlBytesBulk(buffer, false);
        }
    }
}
