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
    public delegate void ServerChangeHandler(InterfaceServer s);
    public enum ServerState { Uninitialized, Stopped, Stopping, Started, Starting, Destroying, Destroyed };

    public class InterfaceServer
    {
        public ServerChangeHandler OnStateChanged;
        private ServerState stateRequested;
        private ServerState _state;
        public ServerState State {
            get { return _state; }
            private set
            {
                if (Thread.CurrentThread != stateThread)
                    throw new Exception(String.Format("State changing from wrong thread {0}", Thread.CurrentThread.Name));
                _state = value;
                if (OnStateChanged != null)
                    OnStateChanged(this);
            }
        }


        public SmartScopeInterfaceUsb hwInterface;
        public int Port { get {
                return State == ServerState.Started? ((IPEndPoint)ControlSocketListener.LocalEndpoint).Port : -1;
            } }
        private const int RECEIVE_TIMEOUT = 10000; //10sec

        Thread controlSocketThread;
        Thread dataSocketThread;
        Thread stateThread;

        private object bwlock = new object();
        private int bytesTx = 0;
        private int bytesRx = 0;
        public int BytesRx { get { return bytesRx; } }
        public int BytesTx { get { return bytesTx; } }

        public InterfaceServer(SmartScopeInterfaceUsb hwInterface)
        {
            this.hwInterface = hwInterface;
            
            stateThread = new Thread(ManageState)
            {
                Name = "State Manager"
            };
            stateThread.Start();
            _state = ServerState.Uninitialized;
            stateRequested = ServerState.Stopped;
        }

        private object disconnectLock = new object();
        private bool disconnectCalled;
        private void ManageState()
        {
            //Only this thread can change State
            while (State != ServerState.Destroyed)
            {
                Thread.Sleep(100);
                if (State == ServerState.Destroying || State == ServerState.Starting || State == ServerState.Stopping)
                    throw new Exception("Server state transitioning outside of state manager thread");


                //Local copy of stateRequested so code below is thread safe
                ServerState nextState = stateRequested;

                if (nextState == State)
                    continue;

                Logger.Info("Moving from state {0:G} -> {1:G}", State, nextState);

                switch (nextState)
                {
                    //From here, stateRequested can only be Started, Stopped or Destroyed
                    case ServerState.Started:
                        State = ServerState.Starting;
                        controlSocketThread = new Thread(ControlSocketServer)
                        {
                            Name = "TCP listener",
                        };
                        controlSocketThread.Start();
                        while (DataSocketListener == null || DataSocketListener.Server == null)
                            Thread.Sleep(10);
                        State = ServerState.Started;
                        break;
                    case ServerState.Stopped:
                        State = ServerState.Stopping;
                        Disconnect();
                        State = ServerState.Stopped;
                        break;
                    case ServerState.Destroyed:
                        State = ServerState.Destroying;
                        Disconnect();
                        State = ServerState.Destroyed;
                        break;
                    default:
                        throw new Exception(String.Format("Illegal target state requested {0:G}", nextState));

                }
            }
        }
        private void Disconnect()
        {
            if (Thread.CurrentThread != stateThread)
                throw new Exception("Disconnect called from wrong thread");
            lock (disconnectLock)
            {
                if (disconnectCalled)//Means disconnect was called
                {
                    if (connected)
                        Logger.Error("Wow this is bad!");
                    return;
                }
                disconnectCalled = true;
                connected = false;
            }
            UnregisterZeroConf();
            CloseSocket(dataSocketThread, DataSocketListener, DataSocket);
            CloseSocket(controlSocketThread, ControlSocketListener, ControlSocket);
        }

        public void Start()
        {
            stateRequested = ServerState.Started;
        }
        public void Stop()
        {
            stateRequested = ServerState.Stopped;
        }
        public void Destroy()
        {
            stateRequested = ServerState.Destroyed;
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

            Logger.Info("ZeroConf service posted");
        }
        private void UnregisterZeroConf()
        {
            if (service == null)
                return;
            service.Dispose();
            Logger.Info("ZeroConf service retracted");
        }

        TcpListener DataSocketListener;
        Socket DataSocket;
        TcpListener ControlSocketListener;
        Socket ControlSocket;
        bool connected;

        private byte[] smartScopeBuffer = new byte[Net.ACQUISITION_PACKET_SIZE]; // Max received = header + full acq buf

        private void DataSocketServer()
        {
            int length = 0;
            try
            {
                Logger.Info("Waiting for data connection to be opened");
                try
                {
                    DataSocket = DataSocketListener.Server.Accept();
                    DataSocket.SendBufferSize = Net.DATA_SOCKET_BUFFER_SIZE;
                }
                catch (Exception e)
                {
                    Logger.Info("Data socket aborted {0:s}", e.Message);
                    Stop();
                    return;
                }

                Logger.Info("Data socket connection accepted from " + DataSocket.RemoteEndPoint);

                while (connected)
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
                        Logger.Info("usb error {0:s}", e.Message);
                        Stop();
                        return;
                    }
                    try
                    {
                        int sent = 0;
                        while(sent < length) {
                            sent += DataSocket.Send(smartScopeBuffer, sent, length - sent, SocketFlags.None);
                        }

                    }
                    catch (SocketException e)
                    {
                        Logger.Info("Data socket aborted {0:s}", e.Message);
                        Stop();
                        return;
                    }
                    bytesTx += length;
                }
            } catch(ThreadAbortException e)
            {
                Logger.Info("Data thread aborted {0:s}", e.Message);
                hwInterface.Destroy();
                Stop();
                return;
            }
        }

        bool processing;

        private void ControlSocketServer()
        {
            disconnectCalled = false;
            processing = false;
            int msgBufferLength = 0;
            ScopeController ctrl;
            int address;
            int length;
            byte[] data;

            ControlSocketListener = new TcpListener(IPAddress.Any, 0);
            DataSocketListener = new TcpListener(IPAddress.Any, 0);

            byte[] rxBuffer = new byte[Net.BUF_SIZE];
            byte[] msgBuffer = new byte[1024*1024];

            ControlSocketListener.Start();
            Logger.Info("SmartScope Server listening for incoming connections on port " + ((IPEndPoint)ControlSocketListener.LocalEndpoint).Port.ToString());

            DataSocketListener.Start();

            RegisterZeroConf();

            try
            {
                ControlSocket = ControlSocketListener.Server.Accept();
            }
            catch (SocketException e)
            {
                Logger.Info("Control socket aborted {0:s}", e.Message);
                return;
            }
            ControlSocket.DontFragment = true;
            ControlSocket.NoDelay = true;
            Logger.Info("Connection accepted from {0}", ControlSocket.RemoteEndPoint);
            UnregisterZeroConf();

            connected = true;
            dataSocketThread = new Thread(DataSocketServer) { Name = "Data Socket" };
            dataSocketThread.Start();
                
            while (connected)
            {
                int bufLengtBefore = msgBufferLength;
                List<Net.Message> msgList = Net.ReceiveMessage(ControlSocket, msgBuffer, ref msgBufferLength);
                if (msgList == null) //this would indicate a network error
                {
                    Logger.Info("Nothing received from network socket => resetting");
                    Stop();
                    return;
                }
                bytesRx += (msgBufferLength - bufLengtBefore);

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
                                    Logger.Info("Received Disconnect request from client");
                                    hwInterface.FlushDataPipe();
                                    Stop();
                                    return;
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
                                    Logger.Info("Unsupported command {0:G}", command);
                                    Stop();
                                    return;
                            }
                            processing = false;
                        } catch(ScopeIOException e)
                        {
                            Logger.Error("Scope IO error : " + e.Message);
                            Stop();
                            try
                            {
                                hwInterface.Reset();
                            }
                            catch { }
                            return;
                        }
                        if (response != null)
                        {
                            try
                            {
                                //Logger.Debug("Command {0:G}", (Net.Command)response[3]);
                                ControlSocket.Send(response);
                            }
                            catch (Exception e)
                            {
                                Logger.Info("Failed to write to socket: " + e.Message);
                                Stop();
                                return;
                            }
                        }
                        if (response != null)
                        {
                            bytesTx += response.Length;
                        }
                    }
                }
                else
                {
                    Stop();
                    return;
                }
            }
        }

        private void CloseSocket(Thread thread, TcpListener l, Socket socket)
        {
            if (thread == null)
                return;
            while (thread.IsAlive || (socket != null && socket.Connected))
            {
                try
                {
                    if (socket != null)
                    {
                        if (socket.Connected)
                            socket.Send(Net.Command.DISCONNECT.msg());
                        socket.Close();
                        socket.Dispose();
                    }
                }
                catch (ObjectDisposedException ode)
                {
                    Logger.Info("socket disposed");
                    socket = null;
                }
                catch (Exception e)
                {
                    Logger.Info("while closing socket on thread {0} : {1}", thread.Name, e.Message);
                }
                try
                {
                    if (l != null)
                        l.Stop();
                }
                catch (ObjectDisposedException ode)
                {
                    Logger.Info("TcpListener disposed");
                    l = null;
                }
                catch (Exception e)
                {
                    Logger.Info("while stopping TcpListener on thread {0} : {1}", thread.Name, e.Message);
                }
                try
                {
                    thread.Abort();
                    thread.Interrupt();
                }
                catch(Exception e)
                {
                    Logger.Info("while aborting thread {0} : {1} (Alive = {2})", thread.Name, e.Message, thread.IsAlive);
                }
            }
        }

        enum LogTypes
        {
            NETWORK,
            DATA,
            ZEROCONF,
            SERVER,
        }
    }
}
