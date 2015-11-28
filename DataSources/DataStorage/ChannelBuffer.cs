using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using LabNation.Interfaces;
using LabNation.DeviceInterface.Devices;
using System.Runtime.Serialization.Formatters.Binary;

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
        private bool writing = false;
        BinaryFormatter bin = new BinaryFormatter();

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
            writing = true;            

            lock (streamLock)
            {
                MemoryStream newStream = new MemoryStream();                
                bin.Serialize(newStream, data);

                //first write how many elements will be added for this acquisition
                byte[] appendLength = BitConverter.GetBytes(newStream.Length);
                stream.Write(appendLength, 0, appendLength.Length);
                
                //now append the new data
                newStream.Position = 0;
                newStream.CopyTo(stream);
            }
        }

        private void CopyStream(Stream input, Stream output, int bytes)
        {
            byte[] buffer = new byte[32768];
            int read;
            while (bytes > 0 &&
                   (read = input.Read(buffer, 0, Math.Min(buffer.Length, bytes))) > 0)
            {
                output.Write(buffer, 0, read);
                bytes -= read;
            }
        }

        public Array GetDataOfNextAcquisition()
        {
            Array output = null;

            if (stream.Length == 0)
                return null;

            lock (streamLock)
            {
                //in case of first read, the stream has to be rolled back to position 0
                if (writing)
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    writing = false;
                }

                long bytesToRead = BitConverter.ToInt64(reader.ReadBytes(8), 0);

                //get section of stream containing data of this acquisition
                MemoryStream newStream = new MemoryStream();
                CopyStream(stream, newStream, (int)bytesToRead);
                newStream.Position = 0;

                //deserialize
                output = (Array)bin.Deserialize(newStream);
            }
                
            return output;
        }
    }
}
