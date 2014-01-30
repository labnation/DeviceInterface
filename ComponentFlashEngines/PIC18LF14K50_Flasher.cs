using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace ECore
{
    public class PIC18LF14K50_Flasher
    {
        public enum PicFlashResult { Success, ReadFromRomFailure, TrialFailedWrongDataReceived, WriteToRomFailure, ErrorParsingHexFile, FailureDuringVerificationReadback }
        private EDevice eDevice;

        public PIC18LF14K50_Flasher(EDevice eDevice, StreamReader readerStream)
        {
            this.eDevice = eDevice;            

            Dictionary<uint, byte[]> flashData = ConvertHexFile(readerStream);
            PrintFwVersion();
            TrialRead();
            TrialUnlockEraseWriteReadback();
            EraseFullUpperMemory();
            WriteFullUpperMemory(flashData);
            VerifyFullUpperMemory(flashData);
            PrintFwVersion();
        }

        

        private Dictionary<uint, byte[]> ConvertHexFile(StreamReader reader)
        {
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////
            // Convert HEX file into dictionary
            int i = 0;            

            Dictionary<uint, byte[]> flashData = new Dictionary<uint, byte[]>();
            uint upperAddress = 0;
            while (!reader.EndOfStream)
            {
                //see http://embeddedfun.blogspot.be/2011/07/anatomy-of-hex-file.html

                string line = reader.ReadLine();
                ushort bytesInThisLine = Convert.ToUInt16(line.Substring(1, 2), 16);
                ushort lowerAddress = Convert.ToUInt16(line.Substring(3, 4), 16);
                ushort contentType = Convert.ToUInt16(line.Substring(7, 2), 16);

                if (contentType == 00) //if this is a data record
                {
                    byte[] bytes = new byte[bytesInThisLine];
                    for (i = 0; i < bytesInThisLine; i++)
                        bytes[i] = Convert.ToByte(line.Substring(9 + i * 2, 2), 16);

                    flashData.Add(upperAddress + lowerAddress, bytes);
                }
                else if (contentType == 04) //contains 2 bytes: the upper address
                {
                    upperAddress = Convert.ToUInt32(line.Substring(9, 4), 16) << 16;
                }
            }

            return flashData;
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        }
        private void PrintFwVersion()
        {
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //Fetch and print FW version
            byte[] sendBytesForFwVersion = new byte[] { 123, 1 };
            eDevice.HWInterface.WriteControlBytes(sendBytesForFwVersion);
            //System.Threading.Thread.Sleep(100);
            byte[] readFwVersion1 = eDevice.HWInterface.ReadControlBytes(16);
            Console.Write("Active FW version: ");
            for (int i = 2; i < 5; i++)
                Console.Write(readFwVersion1[i].ToString() + ";");
            Console.WriteLine();
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        }
        private PicFlashResult TrialRead()
        {
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //Try to read from dummy location
            //read 8 bytes from location 0x1FC0
            byte[] sendBytesForRead = new byte[5];
            int i = 0;
            sendBytesForRead[i++] = 123;    //preamble
            sendBytesForRead[i++] = 7;      //progRom read
            sendBytesForRead[i++] = 31;     //progRom address MSB
            sendBytesForRead[i++] = 192;    //progRom address LSB
            sendBytesForRead[i++] = 8;      //read 8 bytes

            //send over to HW, to perform read operation
            eDevice.HWInterface.WriteControlBytes(sendBytesForRead);

            //now data is stored in EP3 of PIC, so read it
            byte[] readBuffer = eDevice.HWInterface.ReadControlBytes(16);
            if (readBuffer.Length != 16) return PicFlashResult.ReadFromRomFailure;
            Console.WriteLine("Trial read successful");
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////

            return PicFlashResult.Success;
        }
        private PicFlashResult TrialUnlockEraseWriteReadback()
        {
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //Try unlock-erase-write-read on dummy location
            //unlock 
            byte[] sendBytesForUnlock = new byte[] { 123, 5 };                       
            eDevice.HWInterface.WriteControlBytes(sendBytesForUnlock);
            //erase            
            byte[] sendBytesForErase = new byte[] { 123, 9, 31, 192 };
            eDevice.HWInterface.WriteControlBytes(sendBytesForErase);
            //write
            byte[] sendBytesForWrite1 = new byte[] { 123, 8, 31, 192, 8, 1, 0, 1, 2, 3, 4, 5, 6, 7 };
            eDevice.HWInterface.WriteControlBytes(sendBytesForWrite1);
            byte[] sendBytesForWrite2 = new byte[] { 123, 8, 31, 192, 8, 0, 8, 9, 10, 11, 12, 13, 14, 15 };
            eDevice.HWInterface.WriteControlBytes(sendBytesForWrite2);
            //readback
            byte[] sendBytesForRead = new byte[5];
            int i = 0;
            sendBytesForRead[i++] = 123;    //preamble
            sendBytesForRead[i++] = 7;      //progRom read
            sendBytesForRead[i++] = 31;     //progRom address MSB
            sendBytesForRead[i++] = 192;    //progRom address LSB
            sendBytesForRead[i++] = 8;      //read 8 bytes
            eDevice.HWInterface.WriteControlBytes(sendBytesForRead);
            byte[] readBuffer1 = eDevice.HWInterface.ReadControlBytes(16);
            byte[] sendBytesForRead2 = new byte[] { 123, 7, 31, 200, 8 };
            eDevice.HWInterface.WriteControlBytes(sendBytesForRead2);
            byte[] readBuffer2 = eDevice.HWInterface.ReadControlBytes(16);
            //lock again, in case check crashes
            byte[] sendBytesForLock = new byte[] { 123, 6 };
            //eDevice.HWInterface.WriteControlBytes(sendBytesForLock);

            //check
            for (i = 0; i < 8; i++)
                if (readBuffer1[5 + i] != i)
                    return PicFlashResult.TrialFailedWrongDataReceived;
            for (i = 0; i < 8; i++)
                if (readBuffer2[5 + i] != 8 + i)
                    return PicFlashResult.TrialFailedWrongDataReceived;
            Console.WriteLine("Trial erase - write - read successful");

            return PicFlashResult.Success;
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        }
        private PicFlashResult EraseFullUpperMemory()
        {
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //Full upper memory erase
            //unlock
            byte[] sendBytesForUnlock = new byte[] { 123, 5 };                       
            eDevice.HWInterface.WriteControlBytes(sendBytesForUnlock);

            //full erase of upper block, done in blocks of 64B at once
            for (int i = 0x2000; i < 0x39FF; i = i + 64)
            {
                byte addressMSB = (byte)(i >> 8);
                byte addressLSB = (byte)i;
                byte[] sendBytesForBlockErase = new byte[] { 123, 9, addressMSB, addressLSB };
                eDevice.HWInterface.WriteControlBytes(sendBytesForBlockErase);
                //Console.WriteLine("Erased memblock 0x" + i.ToString("X"));
            }

            //simple check: read data at 0x2000 -- without erase this is never FF
            byte[] sendBytesForRead3 = new byte[] { 123, 7, 0x20, 0, 8 };
            eDevice.HWInterface.WriteControlBytes(sendBytesForRead3);
            byte[] readBuffer3 = eDevice.HWInterface.ReadControlBytes(16);
            for (int i = 0; i < 8; i++)
                if (readBuffer3[5 + i] != 0xFF)
                    return PicFlashResult.TrialFailedWrongDataReceived;
            Console.WriteLine("Upper memory area erased successfuly");
            return PicFlashResult.Success;
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        }
        private PicFlashResult WriteFullUpperMemory(Dictionary<uint, byte[]> flashData)
        {
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //Write full memory area with content read from file
            byte[] sendBytesForUnlock = new byte[] { 123, 5 };                       
            eDevice.HWInterface.WriteControlBytes(sendBytesForUnlock);
            //prepare packages
            byte[] writePackage1 = new byte[14];
            byte[] writePackage2 = new byte[14];

            foreach (KeyValuePair<uint, byte[]> kvp in flashData)
            {
                //only flash upper mem area
                if ((kvp.Key >= 0x2000) && (kvp.Key < 0x39FF))
                {
                    byte[] byteArr = kvp.Value;

                    //fill first packet
                    int i = 0;
                    writePackage1[i++] = 123;
                    writePackage1[i++] = 8;
                    writePackage1[i++] = (byte)(kvp.Key >> 8);
                    writePackage1[i++] = (byte)(kvp.Key);
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
                    writePackage2[i++] = (byte)(kvp.Key >> 8);
                    writePackage2[i++] = (byte)(kvp.Key);
                    writePackage2[i++] = 8;
                    writePackage2[i++] = 0; //not first data
                    byte[] last8Bytes = new byte[8];
                    for (i = 0; i < 8; i++)
                        if (byteArr.Length > 8 + i)
                            writePackage2[6 + i] = byteArr[8 + i];
                        else
                            writePackage2[6 + i] = 0xFF;

                    //send first packet
                    eDevice.HWInterface.WriteControlBytes(writePackage1);
                    //send second packet, including the 16th byte, after which the write actually happens
                    eDevice.HWInterface.WriteControlBytes(writePackage2);
                }
            }

            //don't lock here! need to verify memory first.
            //eDevice.HWInterface.WriteControlBytes(sendBytesForLock);

            Console.WriteLine("Writing of upper memory area finished");
            return PicFlashResult.Success;
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        }
        private PicFlashResult VerifyFullUpperMemory(Dictionary<uint, byte[]> flashData)
        {
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //Verify by reading back from PIC memory and comparing to contents from file
            foreach (KeyValuePair<uint, byte[]> kvp in flashData)
            {
                //only flash upper mem area
                if ((kvp.Key >= 0x2000) && (kvp.Key < 0x39FF))
                {
                    byte[] byteArr = kvp.Value;

                    //read 2 bytes at address
                    byte[] sendBytesForVerificationRead1 = new byte[] { 123, 7, (byte)(kvp.Key >> 8), (byte)kvp.Key, 8 };
                    eDevice.HWInterface.WriteControlBytes(sendBytesForVerificationRead1);
                    byte[] readVerificationBytes1 = eDevice.HWInterface.ReadControlBytes(16);

                    uint addr = kvp.Key + 8; //need to do this, as there's a possiblity of overflowing
                    byte[] sendBytesForVerificationRead2 = new byte[] { 123, 7, (byte)(addr >> 8), (byte)addr, 8 };
                    eDevice.HWInterface.WriteControlBytes(sendBytesForVerificationRead2);
                    byte[] readVerificationBytes2 = eDevice.HWInterface.ReadControlBytes(16);

                    //compare
                    for (int i = 0; i < 8; i++)
                        if (byteArr.Length > i)
                            if (readVerificationBytes1[5 + i] != byteArr[i])
                                return PicFlashResult.FailureDuringVerificationReadback;
                    for (int i = 0; i < 8; i++)
                        if (byteArr.Length > 8 + i)
                            if (readVerificationBytes2[5 + i] != byteArr[8 + i])
                                return PicFlashResult.FailureDuringVerificationReadback;
                }
            }
            Console.WriteLine("Upper area memory validation passed succesfully!");

            //Lock again!
            byte[] sendBytesForLock = new byte[] { 123, 6 };
            eDevice.HWInterface.WriteControlBytes(sendBytesForLock);

            return PicFlashResult.Success;
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        }
    }
}
