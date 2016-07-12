//#define DEBUGFILE

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
        private Socket configSocket;
        private Socket dataSocket;
        internal ISmartScopeInterfaceUsb hwInterface;
        private const int RECEIVE_TIMEOUT = 10000; //10sec
        private string lastZeroConfPrintChar = "|";

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
            configListener.Stop();
            zeroconfService.Dispose();
            tcpListenerThread.Join(1000);
        }

        RegisterService zeroconfService;
        //will post and renew ZeroConf every interval
        private void ZeroConfThreadStart()
        {
            while (running && (configSocket == null || !configSocket.Connected))
            {
                zeroconfService = new RegisterService();
                lock (zeroconfService)
                {
                    UnregisterZeroConf(true);
                    RegisterZeroConf(true);
                }
                Thread.Sleep(Constants.ZEROCONF_INTERVAL);
            }
        }
        private void RegisterZeroConf(bool update = false)
        {
            zeroconfService = new RegisterService();

			zeroconfService.Name = Dns.GetHostName();
            zeroconfService.RegType = Constants.SERVICE_TYPE;
            zeroconfService.ReplyDomain = Constants.REPLY_DOMAIN;
			zeroconfService.Port = (short)(((IPEndPoint)configListener.LocalEndpoint).Port);
            zeroconfService.Register();

            switch (lastZeroConfPrintChar)
            {
                case "|":
                    lastZeroConfPrintChar = "-";
                    break;
                case "-":
                    lastZeroConfPrintChar = "|";
                    break;
                default:
                    break;
            }

            if (update)
                LogMessage(LogTypes.ZEROCONF, "ZeroConf service updated " + lastZeroConfPrintChar, true);
            else
                LogMessage(LogTypes.ZEROCONF, "ZeroConf service posted");
        }
        private void UnregisterZeroConf(bool update = false)
        {
            if (zeroconfService != null)
                zeroconfService.Dispose();

            if (!update)
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

        TcpListener configListener;
        TcpClient dataClient = new TcpClient();
        private void TcpIpController()
        {
            bool disconnect;


#if DEBUGFILE
            tcpListener = new TcpListener(IPAddress.Any, 25482);
#else
        configListener = new TcpListener(IPAddress.Any, 0);
#endif


            while (running)
            {
                configListener.Start();
                LogMessage(LogTypes.DECORATION, "==================== New session started =======================");
                LogMessage(LogTypes.NETWORK, "SmartScope Server listening for incoming connections on port " + ((IPEndPoint)configListener.LocalEndpoint).Port.ToString());

                //start zeroconf thread which will post and renew every interval
                Thread zeroconfThread = new Thread(ZeroConfThreadStart);
                zeroconfThread.Start();
                
                try
                {
                    configSocket = configListener.Server.Accept();
                    //configSocket.ReceiveTimeout = RECEIVE_TIMEOUT; simply needs to be re-activated!
                }
                catch (Exception e)
                {
                    Logger.Error("Socket aborted");
                    return;
                }

                LogMessage(LogTypes.DECORATION, "\n"); //newline required to terminate ZeroConf update line
                LogMessage(LogTypes.NETWORK, "Connection accepted from " + configSocket.RemoteEndPoint);                
                UnregisterZeroConf();                

                disconnect = false;
                while (running && !disconnect)
                {
                    List<Message> msgList = ReceiveMessage(configSocket);

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
                                ReadControlBytes(configSocket, length);
                            }
                            else if (command == Constants.Commands.READ_HBW)
                            {
                                int length = (m.data[0] << 8) + (m.data[1]);
                                ReadHispeedData(configSocket, length);
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

                                configSocket.Send(answer);
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

                                LogMessage(LogTypes.NETWORK, "Request to disconnect from " + configSocket.RemoteEndPoint);
                            }
                            else if (command == Constants.Commands.STARTDATALINK)
                            {
                                int port = (m.data[0] << 8) + (m.data[1]);
                                dataClient.Connect(((IPEndPoint)configSocket.RemoteEndPoint).Address, port);
                                LogMessage(LogTypes.NETWORK, "Connected data endpoint to " + dataClient.Client.RemoteEndPoint);

                                Thread dataFetchThread = new Thread(DataFetchThreadStart);
                                dataFetchThread.Start();
                            }
                            else
                            {
                                throw new Exception(String.Format("Unsupported command {0:G}", command));
                            }
                        }

                        /*
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
                         * */
                    }
                    else
                    {
                        disconnect = true;
                        LogMessage(LogTypes.NETWORK, "Connection closed");
                        configSocket.Close();
                        configListener.Stop();
                        System.Threading.Thread.Sleep(500);
                    }
                }
            }
        }

        private bool dataFetchThreadRunning = true;
        private void DataFetchThreadStart()
        {
            NetworkStream unbufferedStream = dataClient.GetStream();
            BufferedStream dataNetStream = new BufferedStream(unbufferedStream, Constants.BUF_SIZE);

            LogMessage(LogTypes.SYSTEM, "DataFetchThread started");
            dataFetchThreadRunning = true;
            while (dataFetchThreadRunning)
            {
                //check we have a valid hwInterface
                if (hwInterface == null)
                {
                    dataFetchThreadRunning = false;
                    throw new Exception("hw exception -- need to handle properly"); //FIXME
                    break;
                }

                //get header bytes or die
                byte[] headerBuffer;
                try
                {
                    headerBuffer = hwInterface.GetData(SmartScope.BYTES_PER_BURST);
                }
                catch 
                {
                    dataFetchThreadRunning = false;
                    throw new Exception("getdata exception -- need to handle properly"); //FIXME
                    break;
                }
                if (headerBuffer == null)
                {
                    dataFetchThreadRunning = false;
                    throw new Exception("no data received -- need to handle properly"); //FIXME
                    break;
                }

                //parse header
                SmartScopeHeader header;
                try
                {
                    header = new SmartScopeHeader(headerBuffer);
                }
                catch
                {
                    dataFetchThreadRunning = false;
                    throw new Exception("error parsing header -- need to handle properly"); //FIXME
                    break;
                }

                //get data payload
                byte[] dataBuffer = null;
                if (header.OverviewBuffer)
                    dataBuffer = hwInterface.GetData(SmartScope.OVERVIEW_BUFFER_SIZE * SmartScope.BYTES_PER_SAMPLE);                
                else if (header.FullAcquisitionDump)
                    dataBuffer = hwInterface.GetData(header.Samples * SmartScope.BYTES_PER_SAMPLE);
                else if (!header.ImpossibleDump && !(header.NumberOfPayloadBursts == 0 || header.TimedOut))
                    dataBuffer = hwInterface.GetData(header.Samples * SmartScope.BYTES_PER_SAMPLE);
             
                //throw it all to client
                int packageLength = headerBuffer.Length;
                if (dataBuffer != null)
                    packageLength += dataBuffer.Length;
                byte[] packageLengthBArray = new byte[] { (byte)(packageLength >> (8*3)), (byte)(packageLength >> (8*2)), (byte)(packageLength >> 8), (byte)(packageLength)};
                dataNetStream.Write(packageLengthBArray, 0, packageLengthBArray.Length);
                dataNetStream.Write(headerBuffer, 0, headerBuffer.Length);
                if (dataBuffer != null)
                    dataNetStream.Write(dataBuffer, 0, dataBuffer.Length);
                dataNetStream.Flush();

                LogMessage(LogTypes.SYSTEM, "Sent datapackage of " + packageLength + "+4 bytes");
            }
        }

        enum LogTypes
        {
            NETWORK,
            ZEROCONF,
            DECORATION,
            SYSTEM,
        }
        bool bandwidthPrintedLast = false;        
        private void LogMessage(LogTypes logType, string message, bool update = false)
        {
            string updateString = "\r";
            if (!update) updateString = "";

            if (bandwidthPrintedLast)
                Logger.LogC(LogLevel.INFO, "\n", ConsoleColor.Yellow);

            switch (logType)
            {
                case LogTypes.NETWORK:
                    Logger.LogC(LogLevel.INFO, updateString + "[Network ] ", ConsoleColor.Yellow);
                    break;
                case LogTypes.ZEROCONF:
                    Logger.LogC(LogLevel.INFO, updateString + "[ZeroConf] ", ConsoleColor.Cyan);
                    break;
                case LogTypes.SYSTEM:
                    Logger.LogC(LogLevel.INFO, updateString + "[System  ] ", ConsoleColor.Magenta);
                    break;
                default:
                    break;
            }

            Logger.LogC(LogLevel.INFO, message, ConsoleColor.Gray);
            if (!update)
                Logger.LogC(LogLevel.INFO, "\n", ConsoleColor.Gray);

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
