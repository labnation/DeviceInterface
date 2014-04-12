using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using ECore.HardwareInterfaces;

namespace ECore.Devices
{
    public enum RomElementType { ID, CHA_Div1Mult1_Offset, CHA_Div1Mult1_Gain, CHA_Div1Mult2_Offset, CHA_Div1Mult2_Gain }

    public struct RomElementDefinition
    {
        public int addressOffset;
        public RomElementType elementType;        
        public int byteSize;
        public float minBoundary;
        public float maxBoundary;

        public RomElementDefinition(int addressOffset, RomElementType elementType, int byteSize, float minBoundary, float maxBoundary)
        {            
            this.addressOffset = addressOffset;
            this.elementType = elementType;
            this.byteSize = byteSize;
            this.minBoundary = minBoundary;
            this.maxBoundary = maxBoundary;
        }
    }

    public partial class ScopeV2
    {
        public class ScopeV2RomManager
        {
            private EDeviceHWInterface hwInterface;
            private Dictionary<RomElementType, float> romContents;
            private List<RomElementDefinition> romV1Definition;
            private int romByteSize = 0;

            public ScopeV2RomManager(EDeviceHWInterface hwInterface)
            {
                this.hwInterface = hwInterface;

                //in case new elements need to be added:
                //  - add to RomElementType enum
                //  - add below
                //  - update romByteSize below
                romV1Definition = new List<RomElementDefinition>();
                romV1Definition.Add(new RomElementDefinition(2, RomElementType.ID, 4, 0, UInt32.MaxValue));
                romV1Definition.Add(new RomElementDefinition(6, RomElementType.CHA_Div1Mult1_Offset, 1, 0, 255));
                romV1Definition.Add(new RomElementDefinition(7, RomElementType.CHA_Div1Mult1_Gain, 2, 0, 10));
                romV1Definition.Add(new RomElementDefinition(9, RomElementType.CHA_Div1Mult2_Offset, 1, 0, 255));
                romV1Definition.Add(new RomElementDefinition(10, RomElementType.CHA_Div1Mult2_Gain, 2, 0, 10));
                romByteSize = 12;
            }

            public void Upload(Dictionary<RomElementType, float> contentToUpload)
            {
                EraseROM();

                List<byte> byteList = new List<byte>();
                byteList.Add(0);                                                    // 0: ROM version MSB
                byteList.Add(1);                                                    // 1: ROM version LSB
                uint idInt = (uint)contentToUpload[RomElementType.ID];
                byteList.Add((byte)(idInt >> (8 * 3)));                             // 2: ID MSB
                byteList.Add((byte)(idInt >> (8 * 2)));                             // 3: ID
                byteList.Add((byte)(idInt >> (8 * 1)));                             // 4: ID
                byteList.Add((byte)(idInt));                                        // 5: ID LSB                
                byteList.Add((byte)(contentToUpload[RomElementType.CHA_Div1Mult1_Offset])); // 6: Div1Mult1_Offset
                uint scaledGain = (uint)(((float)contentToUpload[RomElementType.CHA_Div1Mult1_Gain]) / 10f * ushort.MaxValue);
                byteList.Add((byte)(scaledGain >> 8));                              // 7: Div1Mult1_Gain MSB
                byteList.Add((byte)(scaledGain));                                   // 8: Div1Mult1_Gain MSB

                //Dictionary<int, byte> byteDict = new Dictionary<int, byte>();
                byte[] byteArr = new byte[this.romByteSize];
                byteArr[0] = 0;                                                    // 0: ROM version MSB
                byteArr[1] = 1;                                                    // 1: ROM version LSB

                //for each element that SHOULD be provided
                foreach (RomElementDefinition elDef in romV1Definition)
                {
                    //first check whether the element is provided
                    if (!contentToUpload.ContainsKey(elDef.elementType))
                        throw new Exception(); //element required for this ROM version was not given

                    //if it is provided: scale so it fits optimally in given number of bytes
                    float scaledToFit = (contentToUpload[elDef.elementType] - elDef.minBoundary) / (elDef.maxBoundary-elDef.minBoundary) * (float)Math.Pow(2, 8 * elDef.byteSize);
                    long rounded = (long)Math.Round(scaledToFit);

                    //and cut in bytes
                    for (int i = 0; i < elDef.byteSize; i++)
                        byteArr[elDef.addressOffset +  i] = (byte)(rounded >> (8 * (elDef.byteSize - 1 - i)));
                }                

                Write16BytesToROM(0, byteArr);
            }

            public void DownloadFromDevice()
            {
                byte[] byteArr = ReadBytesFromROM(0, romByteSize);

                //automated parsing
                romContents = new Dictionary<RomElementType, float>();
                //for each element that SHOULD be provided
                foreach (RomElementDefinition elDef in romV1Definition)
                {
                    //first check if bytes are available
                    if (byteArr.Length < elDef.addressOffset + elDef.byteSize)
                        throw new Exception(); //not enough bytes read -- requested element outside array bounds

                    //first recreate decimal value
                    long decValue = 0;
                    for (int i = 0; i < elDef.byteSize; i++)
                        decValue += byteArr[elDef.addressOffset + i] << (8 * (elDef.byteSize - 1 - i));

                    romContents.Add(elDef.elementType, (float)decValue/(float)Math.Pow(2, 8 * elDef.byteSize)*(elDef.maxBoundary-elDef.minBoundary)+elDef.minBoundary);
                }

            }

            // Convert an object to a byte array
            private byte[] ObjectToByteArray(Object obj)
            {
                if (obj == null)
                    return null;
                BinaryFormatter bf = new BinaryFormatter();
                MemoryStream ms = new MemoryStream();
                bf.Serialize(ms, obj);
                return ms.ToArray();
            }

            private void Write16BytesToROM(int addressOffset, byte[] byteArr)
            {
                //unlock
                byte[] sendBytesForUnlock = new byte[] { 123, 5 };
                hwInterface.WriteControlBytes(sendBytesForUnlock);
                
                //prepare packages
                byte[] writePackage1 = new byte[14];
                byte[] writePackage2 = new byte[14];
                
                int addressAbsolute = 0x3A00 + addressOffset;

                //fill first packet
                int i = 0;
                writePackage1[i++] = 123;
                writePackage1[i++] = 8;
                writePackage1[i++] = (byte)(addressAbsolute >> 8);
                writePackage1[i++] = (byte)(addressAbsolute);
                writePackage1[i++] = 8;
                writePackage1[i++] = 1; //first data                    
                for (i = 0; i < 8; i++)
                    if (byteArr.Length > i)
                        writePackage1[6 + i] = byteArr[i];
                    else
                        writePackage1[6 + i] = 0xFF;

                //fill second packet
                i = 0;
                writePackage2[i++] = 123;
                writePackage2[i++] = 8;
                writePackage2[i++] = (byte)(addressAbsolute >> 8);
                writePackage2[i++] = (byte)(addressAbsolute);
                writePackage2[i++] = 8;
                writePackage2[i++] = 0; //not first data
                byte[] last8Bytes = new byte[8];
                for (i = 0; i < 8; i++)
                    if (byteArr.Length > 8 + i)
                        writePackage2[6 + i] = byteArr[8 + i];
                    else
                        writePackage2[6 + i] = 0xFF;

                //send first packet
                hwInterface.WriteControlBytes(writePackage1);
                //send second packet, including the 16th byte, after which the write actually happens
                hwInterface.WriteControlBytes(writePackage2);
            }

            private byte[] ReadBytesFromROM(int addressOffset, int numberOfBytes)
            {
                List<byte> byteList = new List<byte>();

                int addressPointer = 0x3A00 + addressOffset;
                while (numberOfBytes > 0)
                {
                    int numberOfBytesToBeFetched = numberOfBytes;
                    if (numberOfBytesToBeFetched > 10) numberOfBytesToBeFetched = 10;

                    //read bytes at address                    
                    byte[] sendBytesForRead = new byte[] { 123, 7, (byte)(addressPointer >> 8), (byte)addressOffset, (byte)numberOfBytesToBeFetched };
                    hwInterface.WriteControlBytes(sendBytesForRead);
                    byte[] readBytes = hwInterface.ReadControlBytes(16);

                    //append to list
                    for (int i = 0; i < numberOfBytesToBeFetched; i++)
                        byteList.Add(readBytes[i + 5]);

                    //prep for next while cycle
                    addressOffset += numberOfBytesToBeFetched;
                    numberOfBytes -= numberOfBytesToBeFetched;
                }

                //return to caller method
                return byteList.ToArray();
            }

            private void EraseROM()
            {
                byte[] sendBytesForUnlock = new byte[] { 123, 5 };
                hwInterface.WriteControlBytes(sendBytesForUnlock);

                //full erase of upper block, done in blocks of 64B at once
                for (int i = 0x3A00; i < 0x3FFF; i = i + 64)
                {
                    byte addressMSB = (byte)(i >> 8);
                    byte addressLSB = (byte)i;
                    byte[] sendBytesForBlockErase = new byte[] { 123, 9, addressMSB, addressLSB };
                    hwInterface.WriteControlBytes(sendBytesForBlockErase);
                }
                
                Console.WriteLine("ROM memory area erased successfuly");
            }
        }
    }
}
