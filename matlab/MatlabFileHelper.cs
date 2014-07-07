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
    public class Variable
    {
        public Type dataType;
        public String name;
        public object data;
    }

    public class Tag
    {
        public Type dataType;
        public UInt32 length;
        public object data; //In case of small data format, otherwise null
    }

    public class Header
    {
        public String text;
        public MatfileVersion version;
        public Header(byte[] bytes)
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
        public const int SZ_TAG = 8; //Tag size in bytes

        public static Type[] DataType = new Type[]
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

        public static Tag ReadTag(BinaryReader reader)
        {

            byte[] bytes = reader.ReadBytes(SZ_TAG);
            Tag t = new Tag();

            t.dataType = DataType[BitConverter.ToInt16(bytes, 0)];
            if (BitConverter.ToUInt16(bytes, 2) != 0) //Small tag fmt
            {   
                t.length = BitConverter.ToUInt16(bytes, 2);
                t.data = CastToMatlabType(t.dataType, bytes, 4, (int)t.length);
            }
            else
            {
                t.length = BitConverter.ToUInt32(bytes, 4);
            }
            return t;
        }

        public static void AdvanceTo8ByteBoundary(BinaryReader r)
        {
            long offset = (8 - (r.BaseStream.Position % 8)) % 8;
            r.BaseStream.Seek(offset, SeekOrigin.Current);
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

        private static Type[] ArrayTypes = new Type[] {
            null,               //0
            null,               //1
            null,               //2
            null,               //3
            typeof(Char),       //4
            null,               //5
            typeof(Double),     //6
            typeof(Single),     //7
            typeof(SByte),      //8
            typeof(Byte),       //9
            typeof(Int16),      //10
            typeof(UInt16),     //11
            typeof(Int32),      //12
            typeof(UInt32),     //13
            typeof(Int64),      //14
            typeof(UInt64)      //15
        };

        public static Type parseArrayType(byte contentTypeInt)
        {
            Type t = ArrayTypes[contentTypeInt];
            if (t != null) return t;
            throw new Exception("Content of array not supported");
        }

        public static int MatlabTypeNumber(Type t)
        {
            int i = Array.IndexOf(ArrayTypes, t);
            if (i > 0) return i;
            throw new NotImplementedException("Arrays of " + t.ToString() + " to .mat file not implemented");
        }


        public static Array CastToMatlabType(Type t, byte[] data, int offset = 0, int length = -1)
        {
            if (length < 0)
                length = data.Length - offset;
            Array result = Array.CreateInstance(t, length / MatlabBytesPerType(t));
            Buffer.BlockCopy(data, offset, result, 0, length);
            return result;
        }

        public static Array SliceRow(this Array array, int row)
        {
            Array output = Array.CreateInstance(array.GetValue(0,0).GetType(), array.GetLength(1));
            for (var i = 0; i < array.GetLength(1); i++)
            {
                output.SetValue(array.GetValue(new int[] { row, i }), i);
            }
            return output;
        }
    }
}
