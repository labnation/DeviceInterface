using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace MatlabFileIO
{
    internal interface IMatlabFileWriterLocker
    {
        bool HasFinished();
    }

    public class MatLabFileArrayWriter : IMatlabFileWriterLocker
    {
        private BinaryWriter writeStream;
        bool hasFinished;

        //vars required to writeback size and dimensions at matrix finalisation
        private long totalLengthStartPosition;
        private long dataLengthStartPosition;
        private long dimensionsStartPosition;

        int[] dimensions = new int[2] { 0, 0 };
        int numberOfDimensions { get { return dimensions.Length; } }
        private long dataLength = 0;
        private long headerLength = 0;
        private int totalPaddingAdded = 0;

        public MatLabFileArrayWriter(Type t, string varName, BinaryWriter writeStream)
        {
            if(t == typeof(Int64) || t == typeof(UInt64))
                throw new Exception("64 bit integer arrays are not supported by matlab file format");
            this.writeStream = writeStream;
            this.hasFinished = false;

            long beginPosition = writeStream.BaseStream.Position;

            //array header
            WriteArrayTag();

            //flags
            WriteFlags(t);

            //dimensions            
            WriteDimensionsPlaceholder();

            // array  name
            WriteName(varName);

            //keep track of how many bytes were already consumed for this header
            headerLength = writeStream.BaseStream.Position - beginPosition;            

            //data header
            WriteDataHeader(t);

            //reset dataLength, as this might have been affected by padding
            dataLength = 0;
            totalPaddingAdded = 0;
        }

        private void WriteDataHeader(Type t)
        {
            //type of contents
            writeStream.Write(MatfileHelper.MatlabArrayTypeNumber(t));
            
            //store position, so we can later overwrite this placeholder
            dataLengthStartPosition = writeStream.BaseStream.Position;
            
            //add placeholder for size
            for (int i = 0; i < 4; i++)
                writeStream.Write((byte)0xcc);            
        }

        public void AddRow(object dataToAppend)
        {
            Array data = dataToAppend as Array;
            if(data == null) 
            {
                data = Array.CreateInstance(dataToAppend.GetType(), 1);
                data.SetValue(dataToAppend, 0);
            }
            //store this dimension size, and check if it is the same as any previous data stored
            if (dimensions[1] == 0) //first data
                dimensions[1] = data.Length;
            else //not first data
                if (dimensions[1] != data.Length) //different size!
                    throw new Exception("Data to be appended has a different size than previously appended data!");
            
            //dump data in stream
            int size = 0;
            if (data.GetType().Equals(typeof(byte[])))
            {
                byte[] castedDataByte = data as byte[];
                for (int i = 0; i < data.Length; i++)
                    writeStream.Write(castedDataByte[i]);
                size = sizeof(byte);
            }
            else if (data.GetType().Equals(typeof(double[]))) {
                    double[] castedDataDouble = data as double[];
                    for (int i = 0; i < data.Length; i++)
                        writeStream.Write(castedDataDouble[i]);
                    size = sizeof(double);
            }
            else if (data.GetType().Equals(typeof(float[])))
            {
                    float[] castedDataSingle = data as float[];
                    for (int i = 0; i < data.Length; i++)
                        writeStream.Write(castedDataSingle[i]);
                    size = sizeof(float);
            }
            else if (data.GetType().Equals(typeof(Int16[])))
            {
                    Int16[] castedDataI16 = data as Int16[];
                    for (int i = 0; i < data.Length; i++)
                        writeStream.Write(castedDataI16[i]);
                    size = sizeof(Int16);
            }
            else if (data.GetType().Equals(typeof(UInt16[])))
            {
                    UInt16[] castedDataUI16 = data as UInt16[];
                    for (int i = 0; i < data.Length; i++)
                        writeStream.Write(castedDataUI16[i]);
                    size = sizeof(UInt16);
            }
            else if (data.GetType().Equals(typeof(Int32[])))
            {
                    Int32[] castedDataI32 = data as Int32[];
                    for (int i = 0; i < data.Length; i++)
                        writeStream.Write(castedDataI32[i]);
                    size = sizeof(Int32);
            }
            else if (data.GetType().Equals(typeof(UInt32[])))
            {
                    UInt32[] castedDataUI32 = data as UInt32[];
                    for (int i = 0; i < data.Length; i++)
                        writeStream.Write(castedDataUI32[i]);
                    size = sizeof(UInt32);
            }
            else if (data.GetType().Equals(typeof(Int64[])))
            {
                Int64[] castedDataI64 = data as Int64[];
                for (int i = 0; i < data.Length; i++)
                    writeStream.Write(castedDataI64[i]);
                size = sizeof(Int64);
            }
            else if (data.GetType().Equals(typeof(UInt64[])))
            {
                UInt64[] castedDataUI64 = data as UInt64[];
                for (int i = 0; i < data.Length; i++)
                    writeStream.Write(castedDataUI64[i]);
                size = sizeof(UInt64);
            }
            else if (data.GetType().Equals(typeof(sbyte[]))) {
                    sbyte[] castedDataChar = data as sbyte[];
                    for (int i = 0; i < data.Length; i++)
                    {
                        writeStream.Write(castedDataChar[i]);
                    }
                    size = sizeof(sbyte);
            }
            else if (data.GetType().Equals(typeof(char[]))) //Char is internally sbyte
            {
                char[] castedDataChar = data as char[];
                for (int i = 0; i < data.Length; i++)
                {
                    writeStream.Write(castedDataChar[i]);
                    writeStream.Write((byte)0);
                }
                size = sizeof(char);
            }

            else
                throw new NotImplementedException("Writing arrays of " + data.GetType().ToString() + " to .mat file not implemented");

            dataLength += data.Length * size;

            //needed for array dimensions
            dimensions[0]++;
        }        

        public void FinishArray(Type t)
        {
            totalPaddingAdded += writeStream.AdvanceTo8ByteBoundary();
            
            //now need to overwrite the dimensions with the correct value
            writeStream.Seek((int)dimensionsStartPosition, SeekOrigin.Begin);
            //silly matlab format dimension definition wasn't made for realtime streaming... without the following, strings would need to be transposed in matlab to be readable
            if (t.Equals(typeof(char)))
            {                
                writeStream.Write(dimensions[0]);
                writeStream.Write(dimensions[1]);
            }
            else
            {
                writeStream.Write(dimensions[1]);
                writeStream.Write(dimensions[0]);
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

        private void WriteArrayTag()
        {
            //miMatrix type (always 14)
            writeStream.Write((int)14);

            //store position, so we can later overwrite this placeholder
            totalLengthStartPosition = writeStream.BaseStream.Position;

            //placeholder for total array size
            for (int i = 0; i < 4; i++)
                writeStream.Write((byte)0xff);
        }

        private void WriteFlags(Type arrayElementDataType)
        {
            //write 4 values for flag block

            //Array flags use uint32 data type
            writeStream.Write(MatfileHelper.MatlabDataTypeNumber(typeof(UInt32)));

            //flag block length (always 8)
            writeStream.Write((int)8);

            //array class
            writeStream.Write(MatfileHelper.MatlabArrayTypeNumber(arrayElementDataType));

            //padding (always 0)
            writeStream.Write((int)0);            
        }

        private void WriteDimensionsPlaceholder()
        {            
            //data type contained in dimensions subelement: Int32
            writeStream.Write(MatfileHelper.MatlabDataTypeNumber(typeof(Int32)));

            //always 8 bytes long
            writeStream.Write((int)8);

            //store position, so we can later overwrite these 2 placeholders
            dimensionsStartPosition = writeStream.BaseStream.Position;

            //placeholder for first dimension
            for (int i = 0; i < dimensions.Length * MatfileHelper.MatlabBytesPerType(typeof(Int32)); i++)
                writeStream.Write((byte)0xee);

            totalPaddingAdded += writeStream.AdvanceTo8ByteBoundary();
        }

        private void WriteName(string name)
        {
            //write 4 values for name block

            //data type contained in name block (always 1)
            writeStream.Write(MatfileHelper.MatlabDataTypeNumber(typeof(SByte)));

            //size (without padding!)
            int nameLength = name.Length;
            writeStream.Write((int)nameLength);

            //write name itself
            for (int i = 0; i < nameLength; i++)
                writeStream.Write((byte)name[i]);
                
            totalPaddingAdded += writeStream.AdvanceTo8ByteBoundary();
        }        
            
        public bool HasFinished() {  return this.hasFinished; }
    }
}
