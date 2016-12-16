using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;
using LabNation.DeviceInterface.Net;
using LabNation.DeviceInterface.Hardware;
using LabNation.Common;
using System.Runtime.InteropServices;

namespace LabNation.DeviceInterface.Hardware
{
    public delegate void OnInterfaceDisconnect(SmartScopeInterfaceEthernet hardwareInterface);

    public class SmartScopeInterfaceEthernet: ISmartScopeInterface
    {
        
        public bool Connected { get { return this.controlClient.Connected; } }
        private IPAddress serverIp;
        private int serverPort;
        private int dataPort;
        private OnInterfaceDisconnect onDisconnect;
        TcpClient controlClient = new TcpClient();
        TcpClient dataClient = new TcpClient();
        Socket controlSocket;
        Socket dataSocket;
        public SmartScopeInterfaceEthernet(IPAddress serverIp, int serverPort, OnInterfaceDisconnect onDisconnect)
        {
            this.serverIp = serverIp;
            this.serverPort = serverPort;
            this.onDisconnect = onDisconnect;
            this.Connect();
        }
			
        private void Connect()
        {
            controlClient.ReceiveTimeout = Net.Net.TIMEOUT_RX;
            var result = controlClient.BeginConnect(this.serverIp, this.serverPort, null, null);
            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(Net.Net.TIMEOUT_CONNECT));
            if (!success)
            {
                throw new ScopeIOException("Failed to connect.");
            }

            controlClient.EndConnect(result);
            controlSocket = controlClient.Client;
            controlSocket.ReceiveTimeout = Net.Net.TIMEOUT_RX;

            byte[] serialBytes = Request(Net.Net.Command.SERIAL);
            serial = System.Text.Encoding.UTF8.GetString(serialBytes, 0, serialBytes.Length);
        }

        private string serial;
        public string Serial { 
            get 
            {
                return serial;
            } 
        }

        public void GetControllerRegister(ScopeController ctrl, uint address, uint length, out byte[] data)
        {
            data = Request(Net.Net.ControllerHeader(Net.Net.Command.GET, ctrl, (int)address, (int)length, null));
        }
        public void SetControllerRegister(ScopeController ctrl, uint address, byte[] data)
        {
            Request(Net.Net.ControllerHeader(Net.Net.Command.SET, ctrl, (int)address, data.Length, data));
        }
        public byte[] GetData(int length)
        {
            return Request(Net.Net.Command.DATA, new byte[] { (byte)(length >> 8), (byte)(length) });
        }

        private void SocketReceive(Socket s, int offset, int length, byte[] buffer)
        {
            if (destroyed)
                return;
            int recvd = 0;
			int recvdTotal = 0;
            while (length > 0)
            {
                if (!s.Connected)
                {
                    Logger.Debug("Socket not connected - Destroying");
                    Destroy();
                }
                try
                {
                    int triesLeft = Net.Net.TIMEOUT_RX;
                    while (!s.Poll(1000, SelectMode.SelectRead)) 
                    {
                        triesLeft--;
                        if (triesLeft < 0)
                        {
                            Logger.Error("Tried enough - destroying");
                            Destroy();
                            return;
                        }
                        
                    }
                    recvd = s.Receive(buffer, offset + recvdTotal, length, SocketFlags.None);
                }
                catch (Exception e)
                {
                    throw new ScopeIOException(e.Message);
                }

                if (recvd == 0)
                {
                    Logger.Debug("Nothing received from socket - Destroying");
                    Destroy();
                }
                length -= recvd;
                recvdTotal += recvd;
            }
        }

        public int GetAcquisition(byte[] buffer)
        {
            try
            {
                if (dataSocket == null)
                {
					byte[] portBytes = Request(Net.Net.Command.DATA_PORT);
					this.dataPort = BitConverter.ToUInt16(portBytes, 0);
                    dataClient.Connect(this.serverIp, this.dataPort);
                    dataClient.ReceiveBufferSize = Net.Net.DATA_SOCKET_BUFFER_SIZE;
                    dataSocket = dataClient.Client;
                    dataSocket.ReceiveTimeout = Net.Net.TIMEOUT_RX;
                }
                SocketReceive(dataSocket, 0, Constants.SZ_HDR, buffer);
                GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                SmartScopeHeader hdr = (SmartScopeHeader)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(SmartScopeHeader));
                handle.Free();
                if (!hdr.IsValid())
                {
                    Logger.Error("Invalid header magic");
                    return 0;
                }


                if (hdr.flags.HasFlag(HeaderFlags.TimedOut))
                    return Constants.SZ_HDR;

                if (hdr.flags.HasFlag(HeaderFlags.IsOverview))
                {
                    SocketReceive(dataSocket, Constants.SZ_HDR, Constants.SZ_OVERVIEW, buffer);
                    return Constants.SZ_HDR + Constants.SZ_OVERVIEW;
                }

                if (hdr.n_bursts == 0)
                    throw new ScopeIOException("number of bursts in this USB pacakge is 0, cannot fetch");

                int len = hdr.n_bursts * hdr.bytes_per_burst;
                SocketReceive(dataSocket, Constants.SZ_HDR, len, buffer);
                return Constants.SZ_HDR + len;
            } catch (SocketException se)
            {
                throw new ScopeIOException("Socket exception: " + se.Message);
            }
        }

        public byte[] PicFirmwareVersion { get { return Request(Net.Net.Command.PIC_FW_VERSION); } }
        public void Reset()
        {
            Logger.Debug("Reset requested - Destroying");
            Destroy();
        }
        public bool FlashFpga(byte[] firmware)
        {
            byte[] ret = Request(Net.Net.Command.FLASH_FPGA, firmware);
            return ret[0] > 0;
        }
        public void FlushDataPipe()
        {
            Request(Net.Net.Command.FLUSH);
        }
        private bool destroyed = false;
        public bool Destroyed { get { return destroyed; } }

        public void Destroy()
        {
            if (destroyed)
                return;
            Logger.Debug(" ----- DESTROYING ---- ");
            destroyed = true;

            if (this.onDisconnect != null)
                onDisconnect(this);

            try
            {
                Request(Net.Net.Command.DISCONNECT);
            }
            catch (ScopeIOException) { }

            try
            {
                controlSocket.Shutdown(SocketShutdown.Both);
            }
            catch { }
            controlClient.Close();

            try
            {
                dataSocket.Shutdown(SocketShutdown.Both);
            }
            catch { }
            dataClient.Close();
        }

        byte[] msgBuffer = new byte[Net.Net.BUF_SIZE];
        int msgBufferLength = 0;

        private byte[] Request(Net.Net.Command command, byte[] cmdData = null)
        {
            return Request(command.msg(cmdData));
        }
        private byte[] Request(byte[] data)
        {
            Net.Net.Command command = (Net.Net.Command)data[Net.Net.HDR_SZ - 1];
            lock (controlSocket)
            {
                try
                {
                    int toSend = data.Length;
                    while (toSend > 0)
                    {
                        toSend -= controlSocket.Send(data, data.Length - toSend, toSend, SocketFlags.None);
                    }
                } catch(Exception se)
                {
                    Logger.Error("Failure while sending to socket, destroying: {0}" + se.Message);
                    Destroy();
                    throw new ScopeIOException("Failure while sending to socket: " + se.Message);
                }

                switch (command)
                {
                    case Net.Net.Command.DATA:
                    case Net.Net.Command.GET:
                    case Net.Net.Command.PIC_FW_VERSION:
                    case Net.Net.Command.SERIAL:
                    case Net.Net.Command.FLASH_FPGA:
					case Net.Net.Command.DATA_PORT:
					case Net.Net.Command.ACQUISITION:
                        List<Net.Net.Message> l = Net.Net.ReceiveMessage(controlSocket, msgBuffer, ref msgBufferLength);
                        if (l == null)
                        {
                            Logger.Debug("Message list is null - no message received?");
                            Destroy();
                            throw new ScopeIOException("Message list is null - no message received?");
                        }

                        if (l.Count > 1)
                        {
                            int i = 0;
                            foreach (Net.Net.Message m in l)
                            {
                                Logger.Error("Message {0} : {1:G} [{2} bytes]", i, m.command, m.length);
                                i++;
                            }
                            throw new ScopeIOException("More than 1 message received");
                        }
                        if (l.Count == 0)
                            throw new ScopeIOException("No reply message received");
                        Net.Net.Message reply = l[0];
                        if (reply.command != command)
                            throw new ScopeIOException(string.Format("Reply {0:G} doesn't match request command {1:G}", reply.command, command));
                        if (command == Net.Net.Command.GET)
                        {
                            ScopeController c; int a; int b; byte[] response;
                            Net.Net.ParseControllerHeader(reply.data, out c, out a, out b, out response);
                            return response;
                        }
                        else
                            return reply.data;
                    default:
                        return null;
                } 
            }
        }
    }
}
