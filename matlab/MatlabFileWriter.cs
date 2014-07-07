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
            MatfileHelper.WriteHeader(writeStream);
        }

        public void Close()
        {
            writeStream.Close();
        }

        public MatLabFileArrayWriter OpenArray(Type t, string varName)
        {
            //first check if there is no array operation ongoing
            if (locker != null)
                if (!locker.HasFinished())
                    throw new Exception("Previous array still open!");

            //check whether type is not a string, as this is not supported for now
            if (t.Equals(typeof(String)))
                throw new NotImplementedException("Writing arrays of strings is not supported (as strings are arrays already)");

            MatLabFileArrayWriter arrayWriter = new MatLabFileArrayWriter(t, varName, writeStream);
            locker = (IMatlabFileWriterLocker)arrayWriter;
            return arrayWriter;
        }

        public void Write(string name, object data)
        {
            //first check if there is no array operation ongoing
            if (locker != null)
                if (!locker.HasFinished())
                    throw new Exception("Array being written! Cannot write to file until array has been finished.");

            //let's write some data
            if(data.GetType().Equals(typeof(String))) {
                    //a string is considered an array of chars
                    string dataAsString = data as string;
                    MatLabFileArrayWriter charArrayWriter = OpenArray(typeof(char), name);
                    charArrayWriter.AddRow(dataAsString.ToCharArray());
                    charArrayWriter.FinishArray(typeof(char));
            }
            else{
                Type t;
                MatLabFileArrayWriter arrayWriter;
                if (data.GetType().IsArray && (data as Array).Rank > 1) //Handle multidimensional array
                {
                    Array arr = data as Array;
                    if (arr.Rank > 2)
                        throw new Exception("Matlab write doesn't support multidimensional arrays with a rank > 2");
                    t = arr.GetValue(0, 0).GetType();
                    arrayWriter = OpenArray(t, name);
                    for (int i = 0; i < arr.GetLength(0); i++)
                    {
                        arrayWriter.AddRow(arr.SliceRow(i));
                    }
                }
                else
                {
                    if (data.GetType().IsArray)
                        t = (data as Array).GetValue(0).GetType();
                    else
                        t = data.GetType();
                    arrayWriter = OpenArray(t, name);
                    arrayWriter.AddRow(data);
                }
                arrayWriter.FinishArray(t);
            }
        }
    }
}
