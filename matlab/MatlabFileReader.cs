using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace MatlabFileIO
{
    public class MatlabFileReader
    {
        BinaryReader readStream;
        List<VariableInfo> variables;

        public MatlabFileReader(string fileName)
        {
            //create stream from filename
            FileStream fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            this.readStream = new BinaryReader(fileStream);

            byte[] headerBytes = readStream.ReadBytes(128);
            MatfileHeader header = new MatfileHeader(headerBytes);

            EnumerateVariables(readStream.BaseStream.Length);
        }

        private void EnumerateVariables(long length)
        {
            variables = new List<VariableInfo>();
            while(readStream.BaseStream.Position < readStream.BaseStream.Length)
            {
                long offset = readStream.BaseStream.Position;  
                VariableInfo v = new VariableInfo();

                MatfileTag t = MatfileHelper.ReadTag(readStream);
                if (t.dataType.Equals(typeof(Array))) //This means type is 14, MiMatrix
                {
                    v.dataOffset = readStream.BaseStream.Position;
                    v.length = t.length;
                    v.arrayDimensions = new List<UInt32>();
                    ParseMatlabMatrix(ref v);
                }
                else
                    throw new Exception("Not an array, don't know what to do with this stuff");
                
                variables.Add(v);
                //readStream.BaseStream.Seek(v.length, SeekOrigin.Current);
            }
        }

        private void ParseMatlabMatrix(ref VariableInfo vi)
        {
            MatfileTag t;
            //Array flags
            //Will always be too large to be in small data format
            t = MatfileHelper.ReadTag(readStream);
            UInt32 flagsClass = readStream.ReadUInt32();
            byte flags = (byte)(flagsClass >> 8) ;
            if ((flags & 0x80) == 0x80)
                throw new IOException("Complex numbers not supported");
            vi.dataType = MatfileHelper.parseArrayType((byte)flagsClass);
            readStream.ReadUInt32();//unused flags

            //Dimensions array - There are always 2 dimensions, so this
            //tag will never be of small data format
            t = MatfileHelper.ReadTag(readStream);
            for (int i = 0; i < t.length / MatfileHelper.MatlabBytesPerType(t.dataType); i++)
                vi.arrayDimensions.Add(readStream.ReadUInt32());

            //Array name
            t = MatfileHelper.ReadTag(readStream);
            if (t.data != null)
            {
                sbyte[] varname = t.data as sbyte[];
                vi.name = Encoding.UTF8.GetString(Array.ConvertAll(varname, x => (byte)x));
            } else {
                byte[] varname = readStream.ReadBytes((int)t.length);
                vi.name = vi.name = Encoding.UTF8.GetString(varname);
            }
            
            //Check if dimensions are correct
            int j = 0;
            while(readStream.BaseStream.Position < vi.dataOffset + vi.length)
            {
                t = MatfileHelper.ReadTag(readStream);
                if (t.length / MatfileHelper.MatlabBytesPerType(vi.dataType) != vi.arrayDimensions[j++])
                    throw new IOException("Read dimensions didn't correspond to header dimensions");
                readStream.BaseStream.Seek(t.length, SeekOrigin.Current);
            }
        }

        public MatlabFileArrayReader OpenArray(string varName)
        {
            MatlabFileArrayReader arrayReader = new MatlabFileArrayReader(varName, readStream);
            return arrayReader;
        }

        public void Close()
        {
            readStream.Close();
        }
    }
}
