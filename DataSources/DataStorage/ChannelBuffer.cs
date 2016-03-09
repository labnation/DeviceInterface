using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
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
        private bool writing = true;
        BinaryFormatter bin = new BinaryFormatter();
        private int acquisitionsStored = 0;
        public int SamplesStored { get; private set; }

        public ChannelBuffer(string name, Channel channel)
        {
            this.filename = Path.GetTempFileName();
            this.name = name;
            this.channel = channel;
            this.stream = new FileStream(filename, FileMode.Create, FileAccess.ReadWrite);
            this.writer = new BinaryWriter(this.stream);
            this.reader = new BinaryReader(this.stream);
            this.internalDataType = channel.DataType;
            this.SamplesStored = 0;
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

        public int AddData(Array data, int chunkSize)
        {
            if (data == null) return acquisitionsStored;
            //if (data.Length == 0) return; //also need to add if data is empty! because otherwise this channel will have less entries in csv/matlab than other channels

            //thread safety! it's possible that data is still added once the reading has begun, completely corrupting the stream position
            //code should only enter when there's something wrong with thread safety, but here for safety
            if (!writing)
                return acquisitionsStored;

            Array dataToStore = data;
            if (chunkSize != data.Length)
            {
                dataToStore = Array.CreateInstance(data.GetType().GetElementType(), chunkSize);
                Array.Copy(data, data.Length - chunkSize, dataToStore, 0, chunkSize);
            }

            lock (streamLock)
            {
                MemoryStream newStream = new MemoryStream();                
                bin.Serialize(newStream, dataToStore);

                //first write how many elements will be added for this acquisition
                byte[] appendLength = BitConverter.GetBytes(newStream.Length);
                stream.Write(appendLength, 0, appendLength.Length);
                
                //now append the new data
                newStream.Position = 0;
                newStream.CopyTo(stream);
            }

            SamplesStored += chunkSize;

            return ++acquisitionsStored;
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

        public void Rewind()
        {
            stream.Seek(0, SeekOrigin.Begin);
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

                byte[] bytesToReadByteArr = new byte[8];
                stream.Read(bytesToReadByteArr, 0, 8);
                
                long bytesToRead = BitConverter.ToInt64(bytesToReadByteArr, 0);

                if (bytesToRead == 0)
                    output = null;
                else
                {
                    //get section of stream containing data of this acquisition
                    MemoryStream newStream = new MemoryStream();
                    long debugPosOrig = stream.Position;
                    CopyStream(stream, newStream, (int)bytesToRead);
                    newStream.Position = 0;

                    //deserialize
                    output = (Array)bin.Deserialize(newStream);      
                }
            }
                
            return output;
        }
    }
}
