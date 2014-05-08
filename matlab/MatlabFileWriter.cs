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

        public void Write(Type t, string name, object data)
        {
            //first check if there is no array operation ongoing
            if (locker != null)
                if (!locker.HasFinished())
                    throw new Exception("Array being written! Cannot write to file until array has been finished.");

            //let's write some data
            if(t.Equals(typeof(String))) {
                    //a string is considered an array of chars
                    string dataAsString = data as string;
                    MatLabFileArrayWriter charArrayWriter = OpenArray(typeof(char), name);
                    charArrayWriter.AddRow(dataAsString.ToCharArray());
                    charArrayWriter.FinishArray(typeof(char));
            }
            else{
                    //even singletons are/canBe saved as arrays.
                    //so let's write an array of 1 element!
                    MatLabFileArrayWriter arrayWriter = OpenArray(t, name);
                    arrayWriter.AddRow(data);
                    arrayWriter.FinishArray(t);
            }
        }
    }
}
