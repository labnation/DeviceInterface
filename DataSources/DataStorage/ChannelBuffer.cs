using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using LabNation.Interfaces;
using LabNation.DeviceInterface.Devices;

namespace LabNation.DeviceInterface.DataSources
{
    internal class ChannelBuffer:IChannelBuffer
    {
        private string name;
        private string filename;
        private Channel channel;
        private Type internalDataType;
        protected FileStream stream;
        protected BinaryWriter writer;
        protected BinaryReader reader;
        protected object streamLock = new object();
        protected int readBufferSize = 2048;

        public ChannelBuffer(string name, Channel channel)
        {
            this.filename = Path.GetTempFileName();
            this.name = name;
            this.channel = channel;
            this.stream = new FileStream(filename, FileMode.Create, FileAccess.ReadWrite);
            this.writer = new BinaryWriter(this.stream);
            this.reader = new BinaryReader(this.stream);
            this.internalDataType = channel.DataType;
        }

        public void Destroy()
        {
            lock (streamLock)
            {
                writer.Dispose();
                reader.Dispose();
                stream.Dispose();
                File.Delete(filename);
            }
        }

        public string GetName() { return name; }
        public long BytesStored() { return stream.Length; }

        public Type GetDataType()
        {
            return internalDataType;
        }

        public void AddData(Array data)
        {
            if (data == null) return;
            if (data.Length == 0) return;

            lock (streamLock)
            {
                stream.Seek(0, SeekOrigin.End);
                byte[] byteData = null;
                //object dataSample = data.GetValue(0);
                if (internalDataType == typeof(float))
                {
                    int sizeOfType = sizeof(float);
                    byteData = new byte[data.Length * sizeOfType];
                    Buffer.BlockCopy(data, 0, byteData, 0, byteData.Length);
                }
                else if (internalDataType == typeof(byte))
                {
                    byteData = (byte[])data;
                }
                else if (internalDataType == typeof(DecoderOutput))
                {
                    List<byte> byteList = new List<byte>();
                    DecoderOutput[] decoderOutputArray = (DecoderOutput[])data;
                    foreach (DecoderOutput decOut in decoderOutputArray)
                        byteList.AddRange(decOut.Serialize());

                    byteData = byteList.ToArray();
                }
                else
                {
                    Common.Logger.Error("Unsupported type for temporary storage");
                }
                
                writer.Write(byteData);
            }
        }

        public Array GetData(int offset = 0, long length = -1)
        {
            //T[] output;
            lock (streamLock)
            {
                /*
                offset *= sizeOfType;
                stream.Seek(offset, SeekOrigin.Begin);
                if (length == -1) length = stream.Length - offset;

                long bytesToRead = Math.Max(0, Math.Min(stream.Length - offset, length * sizeOfType));
                int bytesRead = 0;
                output = new T[bytesToRead / sizeOfType];

                byte[] readBuffer;
                while (bytesRead < bytesToRead)
                {
                    int readLength = (int)Math.Min(bytesToRead - bytesRead, Math.Min(stream.Length - offset, readBufferSize));
                    readBuffer = reader.ReadBytes(readLength);
                    Buffer.BlockCopy(readBuffer, 0, output, bytesRead, readBuffer.Length);
                    bytesRead += readBuffer.Length;
                }
            
            }
            return output;
                 */
            }
                return null;
        }
    }
}
