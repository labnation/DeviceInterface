using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace MatlabFileIO
{
    public interface IMatlabFileWriterLocker
    {
        bool HasFinished();
    }

    public class MatLabFileArrayWriter<T> : IMatlabFileWriterLocker
    {
        private BinaryWriter writeStream;
        bool hasFinished;

        //vars required to writeback size and dimensions at matrix finalisation
        private long totalLengthStartPosition;
        private long dataLengthStartPosition;
        private long dimensionsStartPosition;
        private int firstDim = 0;
        private int secondDim = 0;
        private long dataLength = 0;
        private long headerLength = 0;
        private int totalPaddingAdded = 0;

        public MatLabFileArrayWriter(string varName, BinaryWriter writeStream)
        {
            this.writeStream = writeStream;
            this.hasFinished = false;

            long beginPosition = writeStream.BaseStream.Position;

            //array header
            WriteArrayHeaderRG();

            //flags
            WriteFlagsRG<T>();

            //dimensions            
            WriteDimensionsPlaceholderRG();

            // array  name
            WriteNameRG(varName);

            //keep track of how many bytes were already consumed for this header
            headerLength = writeStream.BaseStream.Position - beginPosition;            

            //data header
            WriteDataHeader();

            //reset dataLength, as this might have been affected by padding
            dataLength = 0;
            totalPaddingAdded = 0;
        }

        private void WriteDataHeader()
        {
            //type of contents
            writeStream.Write((int)MatlabTypeNumber<T>());
            
            //store position, so we can later overwrite this placeholder
            dataLengthStartPosition = writeStream.BaseStream.Position;
            
            //add placeholder for size
            for (int i = 0; i < 4; i++)
                writeStream.Write((byte)0xcc);            
        }

        public void AddRow(T[] dataToAppend)
        {
            //store this dimension size, and check if it is the same as any previous data stored
            if (secondDim == 0) //first data
                secondDim = dataToAppend.Length;
            else //not first data
                if (secondDim != dataToAppend.Length) //different size!
                    throw new Exception("Data to be appended has a different size than previously appended data!");
            
            //dump data in stream
            switch (Type.GetTypeCode(typeof(T)))
            {
                case TypeCode.Double:
                    Double[] castedDataDouble = dataToAppend as Double[];
                    for (int i = 0; i < dataToAppend.Length; i++)
                        writeStream.Write((Double)castedDataDouble[i]);
                    break;
                case TypeCode.Single:
                    Single[] castedDataSingle = dataToAppend as Single[];
                    for (int i = 0; i < dataToAppend.Length; i++)
                        writeStream.Write((Single)castedDataSingle[i]);
                    break;
                case TypeCode.Int16:
                    Int16[] castedDataI16 = dataToAppend as Int16[];
                    for (int i = 0; i < dataToAppend.Length; i++)
                        writeStream.Write((Int16)castedDataI16[i]);
                    break;
                case TypeCode.UInt16:
                    UInt16[] castedDataUI16 = dataToAppend as UInt16[];
                    for (int i = 0; i < dataToAppend.Length; i++)
                        writeStream.Write((UInt16)castedDataUI16[i]);                    
                    break;
                case TypeCode.Int32:
                    Int32[] castedDataI32 = dataToAppend as Int32[];
                    for (int i = 0; i < dataToAppend.Length; i++)
                        writeStream.Write((Int32)castedDataI32[i]);
                    break;
                case TypeCode.UInt32:
                    UInt32[] castedDataUI32 = dataToAppend as UInt32[];
                    for (int i = 0; i < dataToAppend.Length; i++)
                        writeStream.Write((UInt32)castedDataUI32[i]);
                    break;
                case TypeCode.Char:
                    char[] castedDataChar = dataToAppend as char[];
                    for (int i = 0; i < dataToAppend.Length; i++)
                    {
                        writeStream.Write((char)castedDataChar[i]);
                        writeStream.Write((byte)0); 
                    }
                    break;
                default:
                    throw new NotImplementedException("Writing arrays of " + Type.GetTypeCode(typeof(T)).ToString() + " to .mat file not implemented");
                    //break;
            }

            dataLength += dataToAppend.Length * MatlabFileHelper.MatlabBytesPerType<T>();

            //needed for array dimensions
            firstDim++;
        }        

        private int MatlabTypeNumber<E>()
        {
            int typeNumber = 0;

            switch (Type.GetTypeCode(typeof(E)))
            {
                case TypeCode.Double:
                    typeNumber = 9;
                    break;
                case TypeCode.Single:
                    typeNumber = 7;
                    break;                
                case TypeCode.Int16:
                    typeNumber = 3;
                    break;
                case TypeCode.UInt16:
                    typeNumber = 4;
                    break;
                case TypeCode.Int32:
                    typeNumber = 5;
                    break;
                case TypeCode.UInt32:
                    typeNumber = 6;
                    break;
                case TypeCode.Char:
                    typeNumber = 4;
                    break;
                default:
                    throw new NotImplementedException("Writing arrays of " + Type.GetTypeCode(typeof(E)).ToString() + " to .mat file not implemented");
                    //break;
            }

            return typeNumber;
        }

        public void FinishArray()
        {
            AddPadding(dataLength);
            
            //now need to overwrite the dimensions with the correct value
            writeStream.Seek((int)dimensionsStartPosition, SeekOrigin.Begin);
            //silly matlab format dimension definition wasn't made for realtime streaming... without the following, strings would need to be transposed in matlab to be readable
            if (Type.GetTypeCode(typeof(T)) == TypeCode.Char)
            {                
                writeStream.Write((int)firstDim);
                writeStream.Write((int)secondDim);
            }
            else
            {
                writeStream.Write((int)secondDim);
                writeStream.Write((int)firstDim);
            }

            //and the full size of the array
            writeStream.Seek((int)totalLengthStartPosition, SeekOrigin.Begin);
            writeStream.Write((int)(headerLength+dataLength+totalPaddingAdded));

            //and the size of the data only
            writeStream.Seek((int)dataLengthStartPosition, SeekOrigin.Begin);
            writeStream.Write((int)dataLength);

            //set pointer back to end of stream
            writeStream.Seek(0, SeekOrigin.End);

            //indicate this array has finished, so the writeStream can be used by others
            this.hasFinished = true;
        }

        private void WriteArrayHeaderRG()
        {
            //data array type (always 14)
            writeStream.Write((int)14);

            //store position, so we can later overwrite this placeholder
            totalLengthStartPosition = writeStream.BaseStream.Position;

            //placeholder for total array size
            for (int i = 0; i < 4; i++)
                writeStream.Write((byte)0xff);
        }

        private void WriteFlagsRG<E>()
        {
            //write 4 values for flag block

            //data type contained in flag block (always 6)
            writeStream.Write((int)6);

            //flag block length (always 8)
            writeStream.Write((int)8);

            //array class
            switch (Type.GetTypeCode(typeof(E)))
            {
                case TypeCode.Double:
                    writeStream.Write((int)6);
                    break;
                case TypeCode.Single:
                    writeStream.Write((int)7);
                    break;
                case TypeCode.Int16:
                    writeStream.Write((int)10);
                    break;
                case TypeCode.UInt16:
                    writeStream.Write((int)11);
                    break;
                case TypeCode.Int32:
                    writeStream.Write((int)12);
                    break;
                case TypeCode.UInt32:
                    writeStream.Write((int)13);
                    break;
                case TypeCode.Char:
                    writeStream.Write((int)4);
                    break;
                default:
                    throw new NotImplementedException("Writing arrays of " + Type.GetTypeCode(typeof(E)).ToString() + " to .mat file not implemented");
                    //break;
            }

            //padding (always 0)
            writeStream.Write((int)0);            
        }

        private void WriteDimensionsPlaceholderRG()
        {            
            //data type contained in name block (always 5)
            writeStream.Write((int)5);

            //always 8 bytes long
            writeStream.Write((int)8);

            //store position, so we can later overwrite these 2 placeholders
            dimensionsStartPosition = writeStream.BaseStream.Position;

            //placeholder for first dimension
            for (int i = 0; i < 4; i++)
                writeStream.Write((byte)0xee);

            //placeholder for second dimension
            for (int i = 0; i < 4; i++)
                writeStream.Write((byte)0xdd);
        }

        private void WriteNameRG(string name)
        {
            //write 4 values for name block

            //data type contained in name block (always 1)
            writeStream.Write((int)1);

            //size (without padding!)
            int nameLength = name.Length;
            writeStream.Write((int)nameLength);

            //write name itself
            for (int i = 0; i < nameLength; i++)
                writeStream.Write((byte)name[i]);

            //pad if needed
            AddPadding(nameLength);
        }        

        private void AddPadding(long lastWrittenBlockLength)
        {
            //pad block to multiple of 8
            int mod8 = (int)(lastWrittenBlockLength % 8);
            int requiredPadding = 8 - mod8;

            if (requiredPadding == 8) requiredPadding = 0;

            //add 0s
            for (int i = 0; i < requiredPadding; i++)
                writeStream.Write((byte)0);

            //keep track of this
            totalPaddingAdded += requiredPadding;
        }

        public bool HasFinished() {  return this.hasFinished; }
    }
}
