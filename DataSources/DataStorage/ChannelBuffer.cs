using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace ECore.DataSources
{
    abstract public class ChannelBuffer<T>:IChannelBuffer
    {
        private string name;
        private string filename;
        protected FileStream stream;
        protected BinaryWriter writer;
        protected BinaryReader reader;
        protected object streamLock = new object();
        protected int readBufferSize = 2048;
        protected int sizeOfType;
        
        public ChannelBuffer(string name)
        {
            this.filename = Path.GetTempFileName();
            this.name = name;
            this.stream = new FileStream(filename, FileMode.Create, FileAccess.ReadWrite);
            this.writer = new BinaryWriter(this.stream);
            this.reader = new BinaryReader(this.stream);
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
            return typeof(T);
        }

        public void AddData(object data)
        {
            if (data == null) return;
            if(data.GetType() != typeof(T[]))
                throw new Exception("Data incompatible with this channel buffer");
            AddData(data as T[]);
        }

        public object GetData(int offset = 0, long length = -1)
        {
            return GetDataOfType(offset, length) as object;
        }

        public void AddData(T[] data)
        {
            lock (streamLock)
            {
                stream.Seek(0, SeekOrigin.End);
                byte[] byteData = new byte[data.Length * sizeOfType];
                Buffer.BlockCopy(data, 0, byteData, 0, byteData.Length);
                writer.Write(byteData);
            }
        }

        private T[] GetDataOfType(int offset = 0, long length = -1)
        {
            T[] output;
            lock (streamLock)
            {
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
        }
    }
}
