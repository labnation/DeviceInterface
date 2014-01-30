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

        public MatlabFileReader(string fileName)
        {
            //create stream from filename
            FileStream fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            this.readStream = new BinaryReader(fileStream);
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
