using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace MatlabFileIO
{
    public class MatlabFileWriter
    {
        BinaryWriter writeStream;
        IMatlabFileWriterLocker locker;

        public MatlabFileWriter(string fileName)
        {
            //create stream from filename
            FileStream fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write);
            this.writeStream = new BinaryWriter(fileStream);

            //write .mat file header (128 bytes)
            MatlabFileHelper.WriteHeader(writeStream);
        }

        public void Close()
        {
            writeStream.Close();
        }

        public MatLabFileArrayWriter<T> OpenArray<T>(string varName)
        {
            //first check if there is no array operation ongoing
            if (locker != null)
                if (!locker.HasFinished())
                    throw new Exception("Previous array still open!");

            //check whether type is not a string, as this is not supported for now
            if (Type.GetTypeCode(typeof(T)) == TypeCode.String)
                throw new NotImplementedException("Writing arrays of strings is not supported (as strings are arrays already)");

            MatLabFileArrayWriter<T> arrayWriter = new MatLabFileArrayWriter<T>(varName, writeStream);
            locker = (IMatlabFileWriterLocker)arrayWriter;
            return arrayWriter;
        }

        public void Write<T>(string name, T data)
        {
            //first check if there is no array operation ongoing
            if (locker != null)
                if (!locker.HasFinished())
                    throw new Exception("Array being written! Cannot write to file until array has been finished.");

            //let's write some data
            switch (Type.GetTypeCode(typeof(T)))
            {
                case TypeCode.String:
                    //a string is considered an array of chars
                    string dataAsString = data as string;
                    MatLabFileArrayWriter<char> charArrayWriter = OpenArray<char>(name);
                    charArrayWriter.AddRow(dataAsString.ToCharArray());
                    charArrayWriter.FinishArray();
                    break;
                default:
                    //even singletons are/canBe saved as arrays.
                    //so let's write an array of 1 element!
                    MatLabFileArrayWriter<T> arrayWriter = OpenArray<T>(name);
                    arrayWriter.AddRow(new T[] { data });
                    arrayWriter.FinishArray();
                    break;
            }
        }
    }
}
