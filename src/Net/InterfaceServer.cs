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
        internal SmartScopeInterfaceUsb hwInterface;
        private const int RECEIVE_TIMEOUT = 10000; //10sec

        Thread controlSocketThread;
        Thread dataSocketThread;
        Thread bwPrintThread;

        private object bwlock = new object();
        private int bytesTx = 0;
        private int bytesRx = 0;

        public InterfaceServer(SmartScopeInterfaceUsb hwInterface)
        {
            this.hwInterface = hwInterface;
            //start TCP/IP thread
            controlSocketThread = new Thread(ControlSocketServer)
            {
                Name = "TCP listener",
            };
            
            controlSocketThread.Start();

            bwPrintThread = new Thread(PrintBandWidth) { Name = "Bandwidth printer" };
            bwPrintThread.Start();
               
        }

        public void Stop()
        {
            running = false;
            Disconnect();
            service.Dispose();
            bwPrintThread.Join();
        }

        RegisterService service;
        private void RegisterZeroConf()
        {
            service = new RegisterService();

			service.Name = Dns.GetHostName();
            service.RegType = Net.SERVICE_TYPE;
            service.ReplyDomain = Net.REPLY_DOMAIN;
			service.Port = (ushort)(((IPEndPoint)ControlSocketListener.LocalEndpoint).Port);
            service.TxtRecord = new TxtRecord();
            service.TxtRecord.Add(Net.TXT_DATA_PORT, ((IPEndPoint)DataSocketListener.LocalEndpoint).Port.ToString());
            service.Register();

            LogMessage(LogTypes.ZEROCONF, "ZeroConf service posted");
        }
        private void UnregisterZeroConf()
        {
            if (service == null)
                return;
            service.Dispose();
            LogMessage(LogTypes.ZEROCONF, "ZeroConf service retracted");
        }

        TcpListener DataSocketListener;
        Socket DataSocket;
        TcpListener ControlSocketListener;
        Socket ControlSocket;
        bool connected;

        private byte[] smartScopeBuffer = new byte[Constants.SZ_HDR + Constants.FETCH_SIZE_MAX]; // Max received = header + full acq buf

        private void DataSocketServer()
        {
            int length = 0;
            try
            {
                LogMessage(LogTypes.DATA, "Waiting for data connection to be opened");
                try
                {
                    DataSocket = DataSocketListener.Server.Accept();
                }
                catch (SocketException e)
                {
                    LogMessage(LogTypes.DATA, String.Format("Data socket aborted {0:s}", e.Message));
                    return;
                }
                DataSocket.DontFragment = true;
                DataSocket.NoDelay = true;

                LogMessage(LogTypes.DATA, "Data socket connection accepted from " + DataSocket.RemoteEndPoint);

                while (running && connected)
                {
                    while (processing) {
                        // Don't fetch data when control socket is doing stuff
                    }
                    try
                    {
                        length = hwInterface.GetAcquisition(smartScopeBuffer);
                    }
                    catch(ScopeIOException e)
                    {
                        LogMessage(LogTypes.DATA, String.Format("usb error {0:s}", e.Message));
                        continue;
                    }
                    try
                    {
                        DataSocket.Send(smartScopeBuffer, length, SocketFlags.None);
                    }
                    catch (SocketException e)
                    {
                        LogMessage(LogTypes.DATA, String.Format("Data socket aborted {0:s}", e.Message));
                        Disconnect();
                    }
                    lock (bwlock)
                    {
                        bytesTx += length;
                    }
                }
            } catch(ThreadAbortException e)
            {
                LogMessage(LogTypes.DATA, String.Format("Data thread aborted {0:s}", e.Message));
                hwInterface.Destroy();
            }
        }

        bool processing = false;

        private void ControlSocketServer()
        {
            DateTime now;
            int msgBufferLength = 0;
            ScopeController ctrl;
            int address;
            int length;
            byte[] data;

            while (running)
            {
                ControlSocketListener = new TcpListener(IPAddress.Any, 0);
                DataSocketListener = new TcpListener(IPAddress.Any, 0);

                byte[] rxBuffer = new byte[Net.BUF_SIZE];
                byte[] msgBuffer = new byte[1024*1024];

                ControlSocketListener.Start();
                LogMessage(LogTypes.DECORATION, "==================== New session started =======================");
                LogMessage(LogTypes.NETWORK, "SmartScope Server listening for incoming connections on port " + ((IPEndPoint)ControlSocketListener.LocalEndpoint).Port.ToString());

                DataSocketListener.Start();
                
                RegisterZeroConf();
                
                try
                {
                    ControlSocket = ControlSocketListener.Server.Accept();
                }
                catch (SocketException e)
                {
					Logger.Error("Control socket aborted {0:s}", e.Message);
                    return;
                }
                ControlSocket.DontFragment = true;
                ControlSocket.NoDelay = true;
                LogMessage(LogTypes.NETWORK, "Connection accepted from " + ControlSocket.RemoteEndPoint);
                UnregisterZeroConf();

                connected = true;
                dataSocketThread = new Thread(DataSocketServer) { Name = "Data Socket" };
                dataSocketThread.Start();


                while (connected)
                {
                    int bytesReceived;
                    List<Net.Message> msgList = Net.ReceiveMessage(ControlSocket, ref rxBuffer, ref msgBuffer, ref msgBufferLength, out bytesReceived);
                    if (msgList == null) //this would indicate a network error
                    {
                        LogMessage(LogTypes.NETWORK, "Nothing received from network socket => resetting");
                        Disconnect();
                    }
                    lock (bwlock) {
                        bytesRx += bytesReceived;
                    }
                    


                    if (connected && msgList != null) //if no network error
                    {
                        processing = true;
                        foreach (Net.Message m in msgList)
                        {
                            Net.Command command = m.command;
                            byte[] response = null;
                            try
                            {
                                switch (m.command)
                                {
                                    case Net.Command.SERIAL:
                                        response = m.command.msg(System.Text.Encoding.UTF8.GetBytes(hwInterface.Serial));
                                        break;
                                    case Net.Command.PIC_FW_VERSION:
                                        response = m.command.msg(hwInterface.PicFirmwareVersion);
                                        break;
                                    case Net.Command.FLUSH:
                                        hwInterface.FlushDataPipe();
                                        break;
                                    case Net.Command.FLASH_FPGA:
                                        bool result = hwInterface.FlashFpga(m.data);
                                        response = m.command.msg(new byte[] { result ? (byte)0xff : (byte)0x00 });
                                        break;
                                    case Net.Command.DISCONNECT:
                                        hwInterface.FlushDataPipe();
                                        Disconnect();
                                        break;
                                    case Net.Command.DATA:
                                        length = (m.data[0] << 8) + (m.data[1]);
                                        response = m.command.msg(hwInterface.GetData(length));
                                        break;
									case Net.Command.DATA_PORT:
										response = m.command.msg(BitConverter.GetBytes(((IPEndPoint)DataSocketListener.LocalEndpoint).Port));
										break;
									case Net.Command.ACQUISITION:
                                        length = hwInterface.GetAcquisition(smartScopeBuffer);
                                        response = m.command.msg(smartScopeBuffer, length);
                                        break;
                                    case Net.Command.SET:
                                        Net.ParseControllerHeader(m.data, out ctrl, out address, out length, out data);
                                        hwInterface.SetControllerRegister(ctrl, (uint)address, data);
                                        break;
                                    case Net.Command.GET:
                                        Net.ParseControllerHeader(m.data, out ctrl, out address, out length, out data);
                                        hwInterface.GetControllerRegister(ctrl, (uint)address, (uint)length, out data);
                                        response = Net.ControllerHeader(m.command, ctrl, address, length, data);
                                        break;
                                    default:
                                        Logger.Error("Unsupported command {0:G}", command);
                                        Disconnect();
                                        break;
                                }
                                processing = false;
                            } catch(ScopeIOException e)
                            {
                                Logger.Error("Scope IO error : " + e.Message);
                                Disconnect();
                                break;
                            }
                            if (response != null)
                            {
                                try
                                {
                                    ControlSocket.Send(response);
                                }
                                catch (Exception e)
                                {
                                    Logger.Info("Failed to write to socket: " + e.Message);
                                    Disconnect();
                                }
                            }
                            if (response != null)
                            {
                                lock (bwlock)
                                    bytesTx += response.Length;
                            }
                        }
                    }
                    else
                    {
                        Disconnect();
                    }
                }
            }
        }

        private object disconnectLock = new object();
        private bool disconnectCalled = false;
        private void Disconnect()
        {
            lock (disconnectLock)
            {
                if (disconnectCalled)//Means disconnect was called
                    return;
                disconnectCalled = true;
            }
            
            LogMessage(LogTypes.NETWORK, "Disconnecting...");

            UnregisterZeroConf();
            try
            {
                DataSocket.Shutdown(SocketShutdown.Both);
                DataSocket.Send(Net.Command.DISCONNECT.msg());
                DataSocket.Disconnect(false);
                DataSocket.Close(1000);
            }
            catch { }
            DataSocketListener.Stop();
            if(dataSocketThread != null) {
                dataSocketThread.Join(1000);
                if (dataSocketThread.IsAlive)
                    dataSocketThread.Abort();
            }

            try
            {
                ControlSocket.Send(Net.Command.DISCONNECT.msg());
                ControlSocket.Disconnect(false);
                ControlSocket.Shutdown(SocketShutdown.Both);
                ControlSocket.Close(1000);
            }
            catch { }
            ControlSocketListener.Stop();
            controlSocketThread.Join(1000);
            if (controlSocketThread.IsAlive)
                controlSocketThread.Abort();
        }

        enum LogTypes
        {
            NETWORK,
            DATA,
            ZEROCONF,
            DECORATION,
        }
        private void LogMessage(LogTypes logType, string message)
        {
            Logger.LogC(LogLevel.INFO, "\n", ConsoleColor.Yellow);

            switch (logType)
            {
                case LogTypes.NETWORK:
                    Logger.LogC(LogLevel.INFO, "[Network ] ", ConsoleColor.Yellow);
                    break;
                case LogTypes.ZEROCONF:
                    Logger.LogC(LogLevel.INFO, "[ZeroConf] ", ConsoleColor.Cyan);
                    break;
                case LogTypes.DATA:
                    Logger.LogC(LogLevel.INFO, "[Data] ", ConsoleColor.Red);
                    break;
                default:
                    break;
            }

            Logger.LogC(LogLevel.INFO, message + "\n", ConsoleColor.Gray);
        }

        private void PrintBandWidth()
        {
            DateTime lastTime = DateTime.Now;
            int bytesTxLocal;
            int bytesRxLocal;
            while (running)
            {
                Thread.Sleep(100);
                DateTime now = DateTime.Now;
                double timepassed = (now - lastTime).TotalMilliseconds;
                
                lock(bwlock)
                {
                    bytesTxLocal = bytesTx;
                    bytesTx = 0;
                    bytesRxLocal = bytesRx;
                    bytesRx = 0;
                }
                double bwTx = bytesTxLocal / timepassed * 1000 / 1024;
                double bwRx = bytesRxLocal / timepassed * 1000 / 1024;
                Logger.LogC(LogLevel.INFO, String.Format("[Bandwidth] \rTX {0:0.00}kB/s - RX {1:0.00}kB/s\r", bwTx, bwRx), ConsoleColor.Green);
                lastTime = now;
            }
        }
    }
}
