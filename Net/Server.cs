#define DEBUGCONSOLE

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
using LabNation.Common;

namespace LabNation.DeviceInterface.Net
{
    class Server
    {
        private bool running = true;
        private DeviceManager deviceManager = null;
        ISmartScopeUsbInterface hwInterface;

        BandwidthMonitor bwDown = new BandwidthMonitor(new TimeSpan(0, 0, 0, 0, 100));
        BandwidthMonitor bwUp = new BandwidthMonitor(new TimeSpan(0, 0, 0, 0, 100));
        string strBandwidthDown = "";
        string strBandwidthUp = "";
        
        public Server()
        {
            //post ZeroConf service
            PostZeroConf();

            //start USB polling thread
            deviceManager = new DeviceManager(OnInterfaceConnect, null);
            deviceManager.Start();

            //start TCP/IP thread
            System.Threading.Thread tcpListenerThread = new System.Threading.Thread(TcpIpController);
            tcpListenerThread.Start();            
        }

        private void PostZeroConf()
        {
            RegisterService service = new RegisterService();
            service.Name = Constants.SERVICE_NAME;
            service.RegType = Constants.SERVICE_TYPE;
            service.ReplyDomain = Constants.REPLY_DOMAIN;
            service.Port = Constants.PORT;

            TxtRecord txt_record = new TxtRecord();
            txt_record.Add("Version", Constants.VERSION);

            service.TxtRecord = txt_record;
            service.Register();

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("[Network] ");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("ZeroConf service posted");
        }

        public void Stop()
        {
            running = false;
            deviceManager.Stop();
        }        

        private void OnInterfaceConnect(DeviceManager d, List<ISmartScopeUsbInterface> connectedList)
        {
            Logger.LogC(LogLevel.INFO, "[Hardware] ", ConsoleColor.Gray);
            if (this.hwInterface != null)
            {
                if(connectedList.Contains(hwInterface)) {
                    Logger.LogC(LogLevel.INFO, "ignored\n", ConsoleColor.Yellow);
                } else {
                    Logger.LogC(LogLevel.INFO, "removed\n", ConsoleColor.Red);
                    this.hwInterface = null;
                }
            }

            if (connectedList.Where(x => !(x is SmartScopeUsbInterfaceEthernet)).Count() == 0)
                return;

            this.hwInterface = connectedList.First();
            Logger.LogC(LogLevel.INFO, "connected\n", ConsoleColor.Green);
        }

        StreamWriter debugFile;
        byte[] appendArray = null;

        private void TcpIpController()
        {
            debugFile = new StreamWriter("ServerDebug.txt");

            TcpListener tcpListener = new TcpListener(IPAddress.Any, Constants.PORT);
            tcpListener.Start();

            //this is a blocking call until an incoming connection has been received
            Logger.LogC(LogLevel.INFO, "[Network] ", ConsoleColor.Yellow);
            Logger.LogC(LogLevel.INFO, "SmartScope Server listening for incoming connections on port " + Constants.PORT.ToString() + "\n", ConsoleColor.Gray);
            Socket socket = tcpListener.AcceptSocket();

            Logger.LogC(LogLevel.INFO, "[Network] ", ConsoleColor.Yellow);
            Logger.LogC(LogLevel.INFO, "Connection accepted from " + socket.RemoteEndPoint + Constants.PORT.ToString() + "\n\n", ConsoleColor.Gray);

            byte[] buffer = new byte[Constants.BUF_SIZE];
            while (running)
            {
                bool updateConsole = false;
                int bytesProcessed = 0;

#if DEBUGFILE
                DateTime now1 = DateTime.Now;
                debugFile.WriteLine(now1.Second.ToString("00") + "-" + now1.Millisecond.ToString("000") + " Waiting for TCP data");
                debugFile.Flush();
#endif

                int bytesReceived = socket.Receive(buffer);

                Logger.Debug(bytesReceived.ToString() + " bytes received");

#if DEBUGFILE
                DateTime now1 = DateTime.Now;
                debugFile.WriteLine(now1.Second.ToString("00") + "-" + now1.Millisecond.ToString("000") + " Received TCP data (" + bytesReceived.ToString() + " bytes, from "+buffer[0]+" "+buffer[1]+" to "+buffer[bytesReceived-2]+" "+buffer[bytesReceived-1]+")");
                debugFile.Flush();
#endif

                if (appendArray != null)
                {
                    AppendData(ref buffer, ref bytesReceived);
                }

                if (bytesReceived >= buffer.Length)
                    throw new Exception("TCP/IP socket buffer overflow!");

                bool debug = false;

                if (true)
                {
                    updateConsole |= bwDown.Update(bytesReceived, out strBandwidthDown);
                }

                int currentMessageLength = 0;
                while (bytesProcessed < bytesReceived)
                {
                    int offset = bytesProcessed;
                    currentMessageLength = (buffer[offset + 0] << 8) + buffer[offset + 1];

                    int bytesLeftForThisMessage = bytesReceived - bytesProcessed;
                    if (bytesLeftForThisMessage < currentMessageLength)
                    {
                        StoreDataToAppend(buffer, bytesProcessed, bytesLeftForThisMessage);
                        break;
                    }

#if DEBUGCONSOLE
                    {
                        Console.Write("Current message: ");
                        for (int i = 0; i < currentMessageLength; i++)
                            Console.Write(buffer[offset + i].ToString() + " ");
                        Console.WriteLine();
                    }
#endif

                    Constants.Commands command = (Constants.Commands)buffer[offset + 2];

                    if (command == Constants.Commands.SEND)
                    {
                        SendControlMessage(buffer, currentMessageLength, offset);
                    }
                    else if (command == Constants.Commands.READ)
                    {
                        updateConsole = ReadControlBytes(socket, buffer, updateConsole, offset);
                    }
                    else if (command == Constants.Commands.READ_HBW)
                    {
                        updateConsole = ReadHispeedData(socket, buffer, updateConsole, offset);
                    }
                    else if (command == Constants.Commands.SERIAL)
                    {
                        //byte[] answer = System.Text.Encoding.UTF8.GetBytes(scope.Serial);
                        byte[] answer = System.Text.Encoding.UTF8.GetBytes("0254301KA16");

                        socket.Send(answer);

                        updateConsole |= bwUp.Update(answer.Length, out strBandwidthUp);
                    }
                    else if (command == Constants.Commands.FLUSH)
                    {
                        hwInterface.FlushDataPipe();
                    }
                    else
                    {
#if DEBUGFILE
                        DateTime now = DateTime.Now;
                        StringBuilder sb = new StringBuilder();
                        for (int i = 0; i < bytesReceived; i++)
                            sb.Append(buffer[i].ToString() + " ");
                        debugFile.WriteLine(now.Second.ToString("00") + "-" + now.Millisecond.ToString("000") + " ERROR in packet:" + sb);
                        debugFile.Flush();
                        Console.WriteLine();
                        Console.WriteLine(now.Second.ToString("00") + "-" + now.Millisecond.ToString("000") + " ERROR in packet:" + sb);
#endif
                        break;
                    }

                    bytesProcessed += currentMessageLength;
                }

                if (updateConsole)
                {
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
            }
        }

        private void AppendData(ref byte[] buffer, ref int bytesReceived)
        {
            byte[] newBuffer = new byte[Constants.BUF_SIZE];
            Buffer.BlockCopy(appendArray, 0, newBuffer, 0, appendArray.Length);
            Buffer.BlockCopy(buffer, 0, newBuffer, appendArray.Length, bytesReceived);

#if DEBUGCONSOLE
            Console.WriteLine("AppendArray detected -- copied " + appendArray.Length.ToString() + " elements from appendArray (first element " + appendArray[0].ToString() + ", last element " + appendArray[appendArray.Length - 1].ToString() + ") -- copied " + bytesReceived.ToString() + " elements from new buffer (" + buffer[0].ToString() + " to " + buffer[bytesReceived - 1].ToString() + ")");
#endif
#if DEBUGFILE
            debugFile.WriteLine("AppendArray detected -- copied " + appendArray.Length.ToString() + " elements from appendArray (first element " + appendArray[0].ToString() + ", last element " + appendArray[appendArray.Length - 1].ToString() + ") -- copied " + bytesReceived.ToString() + " elements from new buffer (" + buffer[0].ToString() + " to " + buffer[bytesReceived - 1].ToString() + ")");
            debugFile.Flush();
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < appendArray.Length; i++)
                    sb.Append(i.ToString() + ":" + appendArray[i].ToString("000") + " ");
                debugFile.WriteLine("AppendArray:" + sb);
                debugFile.Flush();

                sb = new StringBuilder();
                for (int i = 0; i < bytesReceived; i++)
                    sb.Append(i.ToString() + ":" + buffer[i].ToString("000") + " ");
                debugFile.WriteLine("Buffer     :" + sb);
                debugFile.Flush();

                sb = new StringBuilder();
                for (int i = 0; i < appendArray.Length + bytesReceived; i++)
                    sb.Append(i.ToString() + ":" + newBuffer[i].ToString("000") + " ");
                debugFile.WriteLine("newBuffer  :" + sb);
                debugFile.Flush();
            }
#endif
            //swap buffer
            buffer = newBuffer;

            //update bytesReceived
            bytesReceived += appendArray.Length;

            //delete appendbuffer
            appendArray = null;
        }

        private void StoreDataToAppend(byte[] buffer, int bytesProcessed, int bytesLeftForThisMessage)
        {
            appendArray = new byte[bytesLeftForThisMessage];
            Buffer.BlockCopy(buffer, bytesProcessed, appendArray, 0, bytesLeftForThisMessage);
#if DEBUGCONSOLE
            Console.WriteLine("Not enough bytes for this message! Created appendArray of "+appendArray.Length.ToString()+" bytes, from "+appendArray[0].ToString()+" to "+appendArray[appendArray.Length-1].ToString());
#endif
#if DEBUGFILE
            debugFile.WriteLine("Not enough bytes for this message! Created appendArray of " + appendArray.Length.ToString() + " bytes, from " + appendArray[0].ToString() + " to " + appendArray[appendArray.Length - 1].ToString());
            debugFile.Flush();
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < appendArray.Length; i++)
                    sb.Append(i.ToString() + ":" + appendArray[i].ToString("000") + " ");
                debugFile.WriteLine("AppendArray:" + sb);
                debugFile.Flush();
            }
#endif
        }

        private bool ReadHispeedData(Socket socket, byte[] buffer, bool updateConsole, int offset)
        {
            int readLength = (int)(buffer[offset + 3] << 8) + (int)buffer[offset + 4];
#if DEBUGFILE
            DateTime now = DateTime.Now;
            debugFile.WriteLine(now.Second.ToString("00") + "-" + now.Millisecond.ToString("000") + " Incoming data read request " + readLength.ToString());
            debugFile.Flush();
#endif

            byte[] answer = hwInterface.GetData(readLength);

#if DEBUGFILE
            now = DateTime.Now;
            debugFile.WriteLine(now.Second.ToString("00") + "-" + now.Millisecond.ToString("000") + " Data received from smartscope " + answer.Length.ToString());
            debugFile.Flush();
#endif

            socket.Send(answer);

            updateConsole |= bwUp.Update(answer.Length, out strBandwidthUp);

#if DEBUGFILE
            now = DateTime.Now;
            debugFile.WriteLine(now.Second.ToString("00") + "-" + now.Millisecond.ToString("000") + " Data sent to TCP");
            debugFile.Flush();
#endif

#if DEBUGCONSOLE
            Console.Write("Answer received from SmartScope:");
            foreach (byte b in answer)
                Console.Write(b.ToString() + " ");
            Console.WriteLine();
#endif
            return updateConsole;
        }

        private bool ReadControlBytes(Socket socket, byte[] buffer, bool updateConsole, int offset)
        {
            byte readLength = buffer[offset + 3];

#if DEBUGFILE
            DateTime now = DateTime.Now;
            debugFile.WriteLine(now.Second.ToString("00") + "-" + now.Millisecond.ToString("000") + " Incoming read request " + readLength.ToString());
            debugFile.Flush();
#endif

            byte[] answer = hwInterface.ReadControlBytes(readLength);
#if DEBUGFILE
            DateTime now5 = DateTime.Now;
            debugFile.WriteLine(now5.Second.ToString("00") + "-" + now5.Millisecond.ToString("000") + " Received USB data");
            debugFile.Flush();
#endif

            socket.Send(answer);

            updateConsole |= bwUp.Update(answer.Length, out strBandwidthUp);

#if DEBUGFILE
            now5 = DateTime.Now;
            debugFile.WriteLine(now5.Second.ToString("00") + "-" + now5.Millisecond.ToString("000") + " Returned data over TCP: " + BitConverter.ToString(answer));
            debugFile.Flush();
            /*Console.Write("Answer received from SmartScope:");
            foreach (byte b in answer)
                Console.Write(b.ToString() + " ");
            Console.WriteLine();*/
#endif
            return updateConsole;
        }

        private void SendControlMessage(byte[] buffer, int currentMessageLength, int offset)
        {
            int sendLength = currentMessageLength - 3;
            byte[] message = new byte[sendLength];
            for (int i = 0; i < sendLength; i++)
                message[i] = buffer[offset + i + 3];

#if DEBUGFILE
            DateTime now = DateTime.Now;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < message.Length; i++)
                sb.Append(i.ToString() + ":" + message[i].ToString("000") + " ");
            debugFile.WriteLine(now.Second.ToString("00") + "-" + now.Millisecond.ToString("000") + " Incoming write:" + sb);
            debugFile.Flush();
#endif

            hwInterface.WriteControlBytesBulk(message, false);

#if DEBUGFILE
            DateTime now = DateTime.Now;
            debugFile.WriteLine(now.Second.ToString("00") + "-" + now.Millisecond.ToString("000") + " Finished writing");
            debugFile.Flush();
#endif

#if DEBUGCONSOLE
            Console.Write("Command sent to SmartScope:");
            foreach (byte b in message)
                Console.Write(b.ToString() + " ");
            Console.WriteLine();
#endif
        }
    }
}
