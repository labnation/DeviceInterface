using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace MatlabFileIO
{
    public class MatlabFileArrayReader
    {
        private BinaryReader streamReader;
        private int firstDimension; //number of elements in one row
        private int secondDimension;//number of rows
        private TypeCode contentType;
        private int currentRow;

        public MatlabFileArrayReader(string varName, BinaryReader streamReader)
        {
            this.streamReader = streamReader;
            this.currentRow = 0;

            //check whether version is supported
            streamReader.BaseStream.Seek(124, SeekOrigin.Begin);
            short matVersion = (short)streamReader.ReadUInt16();
            if (matVersion != 0x0100)
                throw new Exception("Error in mat file header: Unsupported matlab version");

            //check endianness
            char endianness = streamReader.ReadChar();
            if (endianness != 'I')
                throw new Exception("Error in mat file header: Unsupported endianness");
            endianness = streamReader.ReadChar();
            if (endianness != 'M')
                throw new Exception("Error in mat file header: Unsupported endianness");

            //scroll to requested variable
            bool varFound = false;
            int positionOfNextVariable;
            while (!varFound && (streamReader.BaseStream.Position < streamReader.BaseStream.Length))
            {
                //read type of next variable
                int varType = streamReader.ReadInt32();

                //read total length of this variable
                int totalLength = streamReader.ReadInt32();
                positionOfNextVariable = (int)streamReader.BaseStream.Position + totalLength;

                switch (varType)
                {
                    case 14: //this variable is an array
                        varFound = OpenArray(varName);
                        break;
                    default:
                        throw new Exception("Encountered variable of unsupported type "+varType.ToString());
                }

                //if this is not the variable we were looking for: move pointer to beginning of next var
                if (!varFound)
                    streamReader.BaseStream.Seek(positionOfNextVariable, SeekOrigin.Begin);
            }
        }

        private bool OpenArray(string requestedVariableName)
        {
            //first definition of type of data inside array: only 3rd value is of interest
            streamReader.BaseStream.Seek(8, SeekOrigin.Current);
            uint contentTypeInt = streamReader.ReadUInt32();
            contentType = Type.GetTypeCode(MatfileHelper.parseArrayType((byte)contentTypeInt));
            streamReader.BaseStream.Seek(4, SeekOrigin.Current);

            //array dimesions: skip first value
            streamReader.BaseStream.Seek(4, SeekOrigin.Current);

            //array dimesions: number of dimensions
            int numberOfDimensions = streamReader.ReadInt32() / 4;
            if (numberOfDimensions != 2)
                throw new Exception("Array encountered with more than 2 dimensions -> unsupported");

            //array dimesions: read and store actual dimensions
            firstDimension = streamReader.ReadInt32();
            secondDimension = streamReader.ReadInt32();

            //variable name: type of chars
            int typeOfChars = streamReader.ReadInt16();
            if (typeOfChars != 1)
                throw new Exception("Unsupported type of name detected");

            //variable name: length of name
            int nameLength = streamReader.ReadInt16();
            string variableName = "";
            for (int i = 0; i < nameLength; i++)
                variableName += streamReader.ReadChar();

            //move pointer to next multiple of 8
            int toSkip = 8 - (nameLength % 8);
            if (toSkip == 8) toSkip = 0;
            streamReader.BaseStream.Seek(toSkip, SeekOrigin.Current);

            //array contents: type. but we don't really care about this, as it was defined by the array type
            int contentTypeDummy = streamReader.ReadInt16();

            //we don't care about the total length of the array content, as we know the dimensions.
            //could add a check here as code improvement
            int contentSize = streamReader.ReadInt16();                       

            //if this is the variable we were looking for: just keep open and return true
            if (variableName == requestedVariableName)
            {
                return true;
            }
            else //otherwise: skip to next variable and return false
            {
                return false;
            }
        }

        public UInt16[] ReadRowUInt16()
        {
            //check if type is correct
            if (this.contentType != TypeCode.UInt16)
                throw new Exception("This array does not contain UInt16 data");

            //check if there is still data inside array
            if (currentRow >= secondDimension)
                throw new Exception("New row requested to be read from array, but array already fully read");

            //increment row counter
            currentRow++;

            //read correct number of bytes
            int bytesToRead = firstDimension * MatfileHelper.MatlabBytesPerType(typeof(UInt16));
            byte[] byteArray = streamReader.ReadBytes(bytesToRead);

            //convert to correct type
            UInt16[] convertedData = new UInt16[byteArray.Length/2];
            Buffer.BlockCopy(byteArray, 0, convertedData, 0, byteArray.Length);
            
            //and return
            return convertedData;
        }

        public float[] ReadRowFloat()
        {
            //check if type is correct
            if (this.contentType != TypeCode.Single)
                throw new Exception("This array does not contain Float data");

            //check if there is still data inside array
            if (currentRow >= secondDimension)
                throw new Exception("New row requested to be read from array, but array already fully read");

            //increment row counter
            currentRow++;

            //read correct number of bytes
            int bytesToRead = firstDimension * MatfileHelper.MatlabBytesPerType(typeof(Single));
            byte[] byteArray = streamReader.ReadBytes(bytesToRead);

            //convert to correct type
            float[] convertedData = new float[byteArray.Length / 4];
            Buffer.BlockCopy(byteArray, 0, convertedData, 0, byteArray.Length);

            //and return
            return convertedData;
        }

        public double[] ReadRowDouble()
        {
            //check if type is correct
            if (this.contentType != TypeCode.Double)
                throw new Exception("This array does not contain Float data");

            //check if there is still data inside array
            if (currentRow >= secondDimension)
                throw new Exception("New row requested to be read from array, but array already fully read");

            //increment row counter
            currentRow++;

            //read correct number of bytes
            int bytesToRead = firstDimension * MatfileHelper.MatlabBytesPerType(typeof(Double));
            byte[] byteArray = streamReader.ReadBytes(bytesToRead);

            //convert to correct type
            double[] convertedData = new double[byteArray.Length / MatfileHelper.MatlabBytesPerType(typeof(Double))];
            Buffer.BlockCopy(byteArray, 0, convertedData, 0, byteArray.Length);

            //and return
            return convertedData;
        }

        public int TotalRows { get { return secondDimension; } }
        public int CurrentRow { get { return currentRow; } }
    }
}

