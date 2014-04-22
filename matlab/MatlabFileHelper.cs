using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;

namespace MatlabFileIO
{ 
    public enum MatfileVersion { 
        MATFILE_5
    };
    public class VariableInfo
    {
        public List<UInt32> arrayDimensions;
        public Type dataType;
        public UInt32 length;
        public String name;
        public long dataOffset;
    }

    public class MatfileTag
    {
        public Type dataType;
        public UInt32 length;
        public object data; //In case of small data format, otherwise null
    }

    public class MatfileHeader
    {
        public String text;
        public MatfileVersion version;
        public MatfileHeader(byte[] bytes)
        {
            if(bytes.Length != 128)
                throw new IOException("Matlab header should be 128 charachters");

            char[] textChars = new char[116];
            Array.Copy(bytes, textChars, 116);
            this.text = new String(textChars);

            ushort version = (ushort)(bytes[125] << 8 + bytes[124]);
            if (version != 0x0100)
                throw new IOException("Unsupported version of matlab file");
            this.version = MatfileVersion.MATFILE_5;
        }
    }

    public static class MatfileHelper
    {
        public static Type[] MatfileTagType = new Type[]
        {
            null,           //0
            typeof(SByte),  //1
            typeof(Byte),   //2
            typeof(Int16),  //3
            typeof(UInt16), //4
            typeof(Int32),  //5
            typeof(UInt32), //6
            typeof(Single), //7
            null,           //8 - reserved
            typeof(Double), //9
            null,           //10 - reserved
            null,           //11 - reserved
            typeof(Int64),  //12
            typeof(UInt64), //13
            typeof(Array),  //14 MiMatrix
            null,           //15 Compressed data - not supported
            null,           //16 UTF-8  - not supported
            null,           //17 UTF-16 - not supported
            null            //18 UTF-32 - not supported
        };

        public static MatfileTag ReadTag(BinaryReader reader)
        {
            return ParseTag(reader.ReadUInt32(), reader.ReadUInt32());
        }

        public static MatfileTag ParseTag(UInt32 int1, UInt32 int2)
        {
            MatfileTag t = new MatfileTag();
            if (int1 >> 16 != 0)
            {
                t.dataType = MatfileTagType[int1 & 0xFFFF];
                t.length = int1 >> 16;
                byte[] smalldata = new byte[t.length];
                for (int i = 0; i < t.length; i++)
                    smalldata[i] = (byte)(int2 >> i * 8);
                t.data = CastToMatlabType(t.dataType, smalldata);
            }
            else //Normal data format
            {
                t.dataType = MatfileTagType[int1 & 0xFFFF];
                t.length = int2;
            }
            return t;
        }

        public static void WriteHeader(BinaryWriter writeStream)
        {
            string descriptiveText = "MATLAB MAT-file v4, Platform: " + Environment.OSVersion.Platform + ", CREATED on: " + DateTime.Now.ToString();

            //write text
            for (int i = 0; i < descriptiveText.Length; i++)
                writeStream.Write(descriptiveText[i]);

            //pad to 124 bytes
            for (int i = 0; i < 124 - descriptiveText.Length; i++)
                writeStream.Write((byte)0);

            //write version into 2 bytes
            writeStream.Write((short)0x0100);

            //write endian indicator
            writeStream.Write((byte)'I');
            writeStream.Write((byte)'M');
        }

        public static int MatlabBytesPerType(Type T)
        {
            switch (Type.GetTypeCode(T))
            {
                case TypeCode.Double:
                    return 8;
                case TypeCode.Single:
                    return 4;
                case TypeCode.Int32:
                case TypeCode.UInt32:
                    return 4;
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Char:
                    return 2;
                case TypeCode.SByte:
                case TypeCode.Byte:
                    return 1;
                default:
                    throw new NotImplementedException("Writing arrays of type " + Enum.GetName(typeof(TypeCode), T).ToString() + " to .mat file not implemented");
            }
        }

        public static Type parseArrayType(byte contentTypeInt)
        {
            switch (contentTypeInt)
            {
                case 1:
                    throw new IOException("Type <Cell array> not supported");
                case 2:
                    throw new IOException("Type <Structure> not supported");
                case 3:
                    throw new IOException("Type <Object> not supported");
                case 4:
                    return typeof(Char);
                case 5:
                    throw new IOException("Sparse array not supported");
                case 6:
                    return typeof(Double);
                case 7:
                    return typeof(Single);
                case 8:
                    return typeof(SByte);
                case 9:
                    return typeof(Byte);
                case 10:
                    return typeof(Int16);
                case 11:
                    return typeof(UInt16);
                case 12:
                    return typeof(Int32);
                case 13:
                    return typeof(UInt32);
                case 14:
                    return typeof(Int64);
                case 15:
                    return typeof(UInt64);
                default:
                    throw new Exception("Content of array not supported");
            }
        }

        public static Array CastToMatlabType(Type t, byte[] data)
        {
            Array result = Array.CreateInstance(t, data.Length / MatlabBytesPerType(t));
            Buffer.BlockCopy(data, 0, result, 0, data.Length);
            return result;
        }
    }
}
