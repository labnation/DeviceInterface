using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace MatlabFileIO
{ 
    public static class MatlabFileHelper
    {
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

        public static int MatlabBytesPerType<T>()
        {
            int bytesPerData = 0;

            switch (Type.GetTypeCode(typeof(T)))
            {
                case TypeCode.Double:
                    bytesPerData = 8;
                    break;
                case TypeCode.Single:
                    bytesPerData = 4;
                    break;
                case TypeCode.Int16:
                    bytesPerData = 2;
                    break;
                case TypeCode.UInt16:
                    bytesPerData = 2;
                    break;
                case TypeCode.Int32:
                    bytesPerData = 4;
                    break;
                case TypeCode.UInt32:
                    bytesPerData = 4;
                    break;
                case TypeCode.Char:
                    bytesPerData = 2;
                    break;
                default:
                    throw new NotImplementedException("Writing arrays of " + Type.GetTypeCode(typeof(T)).ToString() + " to .mat file not implemented");
                    break;
            }

            return bytesPerData;
        }
    }
}
