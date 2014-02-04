using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.EFuctionalities;
using ECore.DeviceMemories;
using System.IO;
#if IPHONE || ANDROID
#else
using System.Windows.Forms;
#endif


namespace ECore.DeviceImplementations
{
    //this is the main class which fills the EDevice with data specific to the HW implementation.
    //eg: which memories, which registers in these memories, which additional functionalities, the start and stop routines, ...
    public partial class Scop3v2:EDeviceImplementation
    {
		public static string DemoStatusText = "";
        private DeviceMemories.Scop3FpgaRegisterMemory fpgaMemory;
        private DeviceMemories.Scop3FpgaRomMemory romMemory;
        private DeviceMemories.Scop3StrobeMemory strobeMemory;
        private DeviceMemories.MAX19506Memory adcMemory;
        private DeviceMemories.Scop3PICRegisterMemory picMemory;
        //constructor relaying to base class
        public Scop3v2(EDevice eDevice) : base(eDevice) { }      
		private bool temp = false;
        private float[] calibrationCoefficients = new float[] {0.0042f, -0.0029f, 0.1028f};
        private int yOffset_Midrange0V;
        public float ChannelAYOffsetVoltage { get { return 0; } }
        public float ChannelBYOffsetVoltage { get { return (float)((fpgaMemory.GetRegister(REG.CHB_YOFFSET_VOLTAGE).InternalValue-yOffset_Midrange0V)) * calibrationCoefficients[1]; } }

		#if ANDROID
		public Android.Content.Res.AssetManager Assets;
		#endif
        
        public override EDeviceHWInterface CreateHWInterface()
        {
            //figure out which yOffset value needs to be put in order to set a 0V signal to midrange of the ADC = 128binary
            yOffset_Midrange0V = (int)((0 - 128f * calibrationCoefficients[0] - calibrationCoefficients[2]) / calibrationCoefficients[1]);

			#if ANDROID
			return new HardwareInterfaces.HWInterfacePIC_Xamarin(this);
			#else
			HardwareInterfaces.HWInterfacePIC_LibUSB hwInterface = new HardwareInterfaces.HWInterfacePIC_LibUSB();

			//check communication by reading PIC FW version
			hwInterface.WriteControlBytes(new byte[] {123, 1});
			byte[] response = hwInterface.ReadControlBytes(16);
			string resultString = "PIC FW Version readout ("+response.Length.ToString()+" bytes): ";
			foreach (byte b in response)
				resultString += b.ToString()+";";
			Logger.AddEntry(this, LogMessageType.Persistent, resultString);

			return hwInterface;

			#endif
        }

        public override Scop3v2RomManager CreateRomManager()
        {
            return new Scop3v2RomManager(eDevice);
        }

        //master method where all memories, registers etc get defined and linked together
        public override List<EDeviceMemory> CreateMemories()
        {
            memories = new List<EDeviceMemory>();

            //add PIC register memory
            Dictionary<string, int> picRegisters = new Dictionary<string, int>();
            picRegisters.Add("ForceStreaming", 0);
            picMemory = new DeviceMemories.Scop3PICRegisterMemory(eDevice, picRegisters);
            //memories.Add(picMemory);
            
            //add FPGA register memory
            fpgaMemory = new DeviceMemories.Scop3FpgaRegisterMemory(eDevice);
            memories.Add(fpgaMemory);

            //add FPGA rom memory     
            romMemory = new DeviceMemories.Scop3FpgaRomMemory(eDevice);
            memories.Add(romMemory);

            //add strobe memory
            strobeMemory = new DeviceMemories.Scop3StrobeMemory(eDevice, fpgaMemory);
            memories.Add(strobeMemory);

            //add ADC memory
            adcMemory = new DeviceMemories.MAX19506Memory(eDevice, fpgaMemory, strobeMemory, romMemory);
            memories.Add(adcMemory);

            return memories;
        }

        public void SetTriggerPos(int trigPos)
        {
            fpgaMemory.GetRegister(REG.TRIGGERLEVEL).InternalValue = (byte)trigPos;
            fpgaMemory.WriteSingle(REG.TRIGGERLEVEL);
        }

        public void SetTriggerPosBasedOnVoltage(float triggerVoltage)
        {
            float fTriggerLevel = (triggerVoltage - fpgaMemory.GetRegister(REG.CHB_YOFFSET_VOLTAGE).InternalValue * calibrationCoefficients[1] - calibrationCoefficients[2]) / calibrationCoefficients[0];
            if (fTriggerLevel < 0) fTriggerLevel = 0;
            if (fTriggerLevel > 255) fTriggerLevel = 255;

            fpgaMemory.GetRegister(REG.TRIGGERLEVEL).InternalValue = (byte)fTriggerLevel;
            fpgaMemory.WriteSingle(REG.TRIGGERLEVEL);
        }

        public override List<object> CreateFunctionalities()
        {
            List<object> functionalities = new List<object>();
            functionalities.Add(new Scope3v2CalibrationVoltage(this));
            functionalities.Add(new Scope3v2ScopeChannelB(this));
            functionalities.Add(new Scope3v2TriggerPosition(this));

            return functionalities;
        }
     
        private string LoadMultilineVHDL()
        {
            string multiline = @"
                //       ||   add VHDL      ||        
                //       \/                 \/
                
constant REG_STROBE_UPDATE	 			: INTEGER := 0;
constant REG_SPI_ADDRESS	  			: INTEGER := 1;
constant REG_SPI_WRITE_VALUE	  		: INTEGER := 2;
constant REG_CALIB_VOLTAGE		  		: INTEGER := 3;
constant REG_TRIGGERLEVEL		  		: INTEGER := 4;
constant REG_TRIGGERHOLDOFF_B0	  		: INTEGER := 5;
constant REG_TRIGGERHOLDOFF_B1	  		: INTEGER := 6;
constant REG_SAMPLECLOCKDIVIDER_B0 		: INTEGER := 7;
constant REG_SAMPLECLOCKDIVIDER_B1  		: INTEGER := 8;
constant REG_CHA_YOFFSET_VOLTAGE  		: INTEGER := 9;
constant REG_CHB_YOFFSET_VOLTAGE  		: INTEGER := 10;
constant REG_NEG_DCDC_PWM		  		: INTEGER := 11;
constant REG_RAM_CONFIGURATION	  		: INTEGER := 12;

constant STR_DEBUGCOUNTER_PIC_OUTPUT  		: INTEGER := 0;
constant STR_INIT_SPI_TRANSFER  			: INTEGER := 1;
constant STR_GLOBAL_RESET	  			: INTEGER := 2;
constant STR_DEBUG_FIFO_A				: INTEGER := 3;
constant STR_FORCE_TRIGGER				: INTEGER := 4;
constant STR_FREE_RUNNING				: INTEGER := 5;
constant STR_CHB_DIV1					: INTEGER := 6;
constant STR_CHB_DIV10				: INTEGER := 7;
constant STR_CHB_DIV100				: INTEGER := 8;
constant STR_CHB_MULT1				: INTEGER := 9;
constant STR_CHB_MULT2				: INTEGER := 10;
constant STR_CHB_MULT3				: INTEGER := 11;
constant STR_CHB_MULT4				: INTEGER := 12;
constant STR_CHB_ENABLECALIB			: INTEGER := 13;
constant STR_CHB_DCCOUPLING				: INTEGER := 14;
constant STR_RESET_DCM				: INTEGER := 15;
constant STR_CHA_DIV1					: INTEGER := 16;
constant STR_CHA_DIV10				: INTEGER := 17;
constant STR_CHA_DIV100				: INTEGER := 18;
constant STR_CHA_MULT1				: INTEGER := 19;
constant STR_CHA_MULT2				: INTEGER := 20;
constant STR_CHA_MULT3				: INTEGER := 21;
constant STR_CHA_MULT4				: INTEGER := 22;
constant STR_CHA_ENABLECALIB			: INTEGER := 23;
constant STR_CHA_DCCOUPLING				: INTEGER := 24;
constant STR_ENABLE_NEG_DCDC			: INTEGER := 25;

constant ROM_FW_MSB	  				: INTEGER := 0;
constant ROM_FW_LSB	  				: INTEGER := 1;
constant ROM_FW_BUILD	  				: INTEGER := 2;
constant ROM_SPI_RECEIVED_VALUE 			: INTEGER := 3;
constant ROM_FPGA_STATUS	 			: INTEGER := 4;


                //       /\                 /\
                //       ||    end VHDL     ||
                //";

            return multiline;
        }

        override public void StartDevice()
        {
            //raise global reset
            strobeMemory.GetRegister(STR.GLOBAL_RESET).InternalValue = 1;
            strobeMemory.WriteSingle(STR.GLOBAL_RESET);

            //flush any transfers still queued on PIC
            //eDevice.HWInterface.FlushHW();

            fpgaMemory.GetRegister(REG.RAM_CONFIGURATION).InternalValue = 0;
            fpgaMemory.WriteSingle(REG.RAM_CONFIGURATION);
            
            //set feedback loopand to 1V for demo purpose and enable
            strobeMemory.GetRegister(STR.CHA_DIV1).InternalValue = 1;
            strobeMemory.WriteSingle(STR.CHA_DIV1);

            strobeMemory.GetRegister(STR.CHB_DIV1).InternalValue = 1;
            strobeMemory.WriteSingle(STR.CHB_DIV1);            

            fpgaMemory.GetRegister(REG.CALIB_VOLTAGE).InternalValue = 78;
            fpgaMemory.WriteSingle(REG.CALIB_VOLTAGE);

            fpgaMemory.GetRegister(REG.TRIGGERLEVEL).InternalValue = 130;
            fpgaMemory.WriteSingle(REG.TRIGGERLEVEL);

            fpgaMemory.GetRegister(REG.CHA_YOFFSET_VOLTAGE).InternalValue = 50;
            fpgaMemory.WriteSingle(REG.CHA_YOFFSET_VOLTAGE);

            fpgaMemory.GetRegister(REG.CHB_YOFFSET_VOLTAGE).InternalValue = (byte)yOffset_Midrange0V;
            fpgaMemory.WriteSingle(REG.CHB_YOFFSET_VOLTAGE);

            //fpgaMemory.RegisterByName(REG.TRIGGERHOLDOFF_B1).InternalValue = 4;
            //fpgaMemory.WriteSingle(REG.TRIGGERHOLDOFF_B1);

            //fpgaMemory.RegisterByName(REG.SAMPLECLOCKDIV_B1).InternalValue = 1;
            //fpgaMemory.WriteSingle(REG.SAMPLECLOCKDIV_B1);

            //fpgaMemory.RegisterByName(REG.SAMPLECLOCKDIV_B1).InternalValue = 1;
            //fpgaMemory.WriteSingle(REG.SAMPLECLOCKDIV_B1);

            //set ADC output resistance to 300ohm instead of 50ohm
            adcMemory.GetRegister(MAX19506.CHA_TERMINATION).InternalValue = 4;
            adcMemory.WriteSingle(MAX19506.CHA_TERMINATION);
            adcMemory.GetRegister(MAX19506.CHB_TERMINATION).InternalValue = 4;
            adcMemory.WriteSingle(MAX19506.CHB_TERMINATION);

            //set ADC to offset binary output (required for FPGA triggering)
            adcMemory.GetRegister(MAX19506.FORMAT_PATTERN).InternalValue = 16;
            adcMemory.WriteSingle(MAX19506.FORMAT_PATTERN);

			//output counter by default
            strobeMemory.GetRegister(STR.CHA_DCCOUPLING).InternalValue = 1;
            strobeMemory.WriteSingle(STR.CHA_DCCOUPLING);
            strobeMemory.GetRegister(STR.CHB_DCCOUPLING).InternalValue = 1;
            strobeMemory.WriteSingle(STR.CHB_DCCOUPLING);

            //freerunning as global enable
            strobeMemory.GetRegister(STR.FREE_RUNNING).InternalValue = 1;
            strobeMemory.WriteSingle(STR.FREE_RUNNING);                        

            //lower global reset
            strobeMemory.GetRegister(STR.GLOBAL_RESET).InternalValue = 0;
            strobeMemory.WriteSingle(STR.GLOBAL_RESET);

            //generate negative voltage
            fpgaMemory.GetRegister(REG.NEG_DCDC_PWM).InternalValue = 50;
            fpgaMemory.WriteSingle(REG.NEG_DCDC_PWM);

            strobeMemory.GetRegister(STR.ENABLE_NEG_DCDC).InternalValue = 1;
            strobeMemory.WriteSingle(STR.ENABLE_NEG_DCDC);

            //romMemory.ReadSingle(ROM.FPGA_STATUS);
            //if (romMemory.RegisterByName(ROM.FPGA_STATUS).InternalValue != 3)
                //Logger.AddEntry(this, LogMessageType.ECoreError, "!!! DCMs not locked !!!");
        }

        override public void StopDevice()
        {
        }

        public void EnableCalib()
        {
            strobeMemory.GetRegister(STR.CHB_ENABLECALIB).InternalValue = 1;
            strobeMemory.WriteSingle(STR.CHB_ENABLECALIB);
        }

        public void DecreaseReadoutSpead()
        {
            fpgaMemory.GetRegister(REG.SAMPLECLOCKDIVIDER_B1).InternalValue = 1;
            fpgaMemory.WriteSingle(REG.SAMPLECLOCKDIVIDER_B1);
        }

        public void ChangeCalibVoltage()
        {
            int orig = fpgaMemory.GetRegister(REG.CALIB_VOLTAGE).InternalValue + 1;
            if (orig > 120) orig = 20;

            fpgaMemory.GetRegister(REG.CALIB_VOLTAGE).InternalValue = (byte)orig;
            fpgaMemory.WriteSingle(REG.CALIB_VOLTAGE);
        }

        public void ToggleFreeRunning()
        {           

            //fpgaMemory.RegisterByName(REG.SAMPLECLOCKDIV_B1).InternalValue = 1;
            //fpgaMemory.WriteSingle(REG.SAMPLECLOCKDIV_B1);

            if (strobeMemory.GetRegister(STR.FREE_RUNNING).InternalValue == 0)
                strobeMemory.GetRegister(STR.FREE_RUNNING).InternalValue = 1;
            else
                strobeMemory.GetRegister(STR.FREE_RUNNING).InternalValue = 0;
            strobeMemory.WriteSingle(STR.FREE_RUNNING);
        }

        
        public void FlashPIC2(StreamReader readerStream)
        {
            //PIC18LF14K50_Flasher picFlasher = new PIC18LF14K50_Flasher(eDevice, readerStream);
        }

        public enum PicFlashResult { Success, ReadFromRomFailure, TrialFailedWrongDataReceived, WriteToRomFailure, ErrorParsingHexFile, FailureDuringVerificationReadback }

        public PicFlashResult FlashPIC()
        {
            int i = 0;
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////
            // Convert HEX file into dictionary
            string fileName = "usb.hex";
            StreamReader reader = new StreamReader("usb.hex");

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

                    flashData.Add(upperAddress+lowerAddress, bytes);
                }
                else if (contentType == 04) //contains 2 bytes: the upper address
                {
                    upperAddress = Convert.ToUInt32(line.Substring(9, 4), 16) << 16;
                }
            }
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////

            byte[] sendBytesForUnlock = new byte[] { 123, 5 };
            byte[] sendBytesForFwVersion = new byte[] { 123, 1 };

            //////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //Fetch and print original FW version
            eDevice.HWInterface.WriteControlBytes(sendBytesForFwVersion);
            //System.Threading.Thread.Sleep(100);
            byte[] readFwVersion1 = eDevice.HWInterface.ReadControlBytes(16);
            Console.Write("Original FW version: ");
            for (i = 2; i < 5; i++)
                Console.Write(readFwVersion1[i].ToString() + ";");
            Console.WriteLine();
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////
            
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //Try to read from dummy location
            //read 8 bytes from location 0x1FC0
            byte[] sendBytesForRead = new byte[5];
            i = 0;
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

            
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //Try unlock-erase-write-read on dummy location
            //unlock            
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
            eDevice.HWInterface.WriteControlBytes(sendBytesForRead);
            byte[] readBuffer1 = eDevice.HWInterface.ReadControlBytes(16);
            byte[] sendBytesForRead2 = new byte[] { 123, 7, 31, 200, 8};
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
                if (readBuffer2[5 + i] != 8+i)
                    return PicFlashResult.TrialFailedWrongDataReceived;
            Console.WriteLine("Trial erase - write - read successful");
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////
            
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //Full upper memory erase
            //unlock
            eDevice.HWInterface.WriteControlBytes(sendBytesForUnlock);

            //full erase of upper block, done in blocks of 64B at once
            for (i = 0x2000; i < 0x3FFF; i=i+64)
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
            for (i = 0; i < 8; i++)
                if (readBuffer3[5 + i] != 0xFF)
                    return PicFlashResult.TrialFailedWrongDataReceived;
            Console.WriteLine("Upper memory area erased successfuly");
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////
            
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //Write full memory area with content read from file
            eDevice.HWInterface.WriteControlBytes(sendBytesForUnlock);
            //prepare packages
            byte[] writePackage1 = new byte[14];
            byte[] writePackage2 = new byte[14];

            foreach (KeyValuePair<uint, byte[]> kvp in flashData)
            {
                //only flash upper mem area
                if ((kvp.Key >= 0x2000) && (kvp.Key < 0x3FFF))
                {
                    byte[] byteArr = kvp.Value;

                    //fill first packet
                    i = 0;
                    writePackage1[i++] = 123;
                    writePackage1[i++] = 8;
                    writePackage1[i++] = (byte)(kvp.Key>>8);
                    writePackage1[i++] = (byte)(kvp.Key);
                    writePackage1[i++] = 8;
                    writePackage1[i++] = 1; //first data                    
                    for (i = 0; i < 8; i++)
                        if (byteArr.Length > i)
                            writePackage1[6+i] = byteArr[i];
                        else
                            writePackage1[6+i] = 0xEE;

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
                        if (byteArr.Length > 8+i)
                            writePackage2[6+i] = byteArr[8 + i];
                        else
                            writePackage2[6+i] = 0xFF;

                    //send first packet
                    eDevice.HWInterface.WriteControlBytes(writePackage1);
                    //send second packet, including the 16th byte, after which the write actually happens
                    eDevice.HWInterface.WriteControlBytes(writePackage2);
                }                
            }

            //don't lock here! need to verify memory first.
            //eDevice.HWInterface.WriteControlBytes(sendBytesForLock);

            Console.WriteLine("Writing of upper memory area finished");            
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////

            //////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //Verify by reading back from PIC memory and comparing to contents from file
            foreach (KeyValuePair<uint, byte[]> kvp in flashData)
            {
                //only flash upper mem area
                if ((kvp.Key >= 0x2000) && (kvp.Key < 0x3FFF))
                {
                    byte[] byteArr = kvp.Value;

                    //read 2 bytes at address
                    byte[] sendBytesForVerificationRead1 = new byte[] { 123, 7, (byte)(kvp.Key>>8), (byte)kvp.Key, 8 };
                    eDevice.HWInterface.WriteControlBytes(sendBytesForVerificationRead1);
                    byte[] readVerificationBytes1 = eDevice.HWInterface.ReadControlBytes(16);

                    uint addr = kvp.Key + 8; //need to do this, as there's a possiblity of overflowing
                    byte[] sendBytesForVerificationRead2 = new byte[] { 123, 7, (byte)(addr >> 8), (byte)addr, 8 };
                    eDevice.HWInterface.WriteControlBytes(sendBytesForVerificationRead2);
                    byte[] readVerificationBytes2 = eDevice.HWInterface.ReadControlBytes(16);

                    //compare
                    for (i = 0; i < 8; i++)
                        if (byteArr.Length > i)
                            if (readVerificationBytes1[5 + i] != byteArr[i])
                                return PicFlashResult.FailureDuringVerificationReadback;
                    for (i = 0; i < 8; i++)
                        if (byteArr.Length > 8+i)
                            if (readVerificationBytes2[5 + i] != byteArr[8+i])
                                return PicFlashResult.FailureDuringVerificationReadback;
                }
            }
            Console.WriteLine("Upper area memory validation passed succesfully!");
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////

            //////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //Lock again!
            eDevice.HWInterface.WriteControlBytes(sendBytesForLock);

            //and print FW version number to console
            eDevice.HWInterface.WriteControlBytes(sendBytesForFwVersion);
            byte[] readFwVersion2 = eDevice.HWInterface.ReadControlBytes(16);
            Console.Write("New FW version: ");
            for (i = 2; i < 5; i++)
                Console.Write(readFwVersion2[i].ToString() + ";");
            Console.WriteLine();            
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////

            return PicFlashResult.Success;
        }


//#if IPHONE
//#else
		public override void FlashHW()
		{
			string fileName = "FPGA_FW_v2.bin";





			List<byte> dataSent = new List<byte> ();
			byte [] extendedData = new byte[16] {
				255,
				255,
				255,
				255,
				255,
				255,
				255,
				255,
				255,
				255,
				255,
				255,
				255,
				255,
				255,
				255
			};

			int extendedPacketsToSend = 2048 / 8;

			Stream inStream = null;
			BinaryReader reader = null;
			try{
				#if ANDROID || IPHONE

				//show all embedded resources
				System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
				for (int assyIndex = 0; assyIndex < assemblies.Length; assyIndex++) {
					if (reader == null) //dirty patch! otherwise this loop will crash, as there are some assemblies at the end of the list that don't support the following operations and crash
					{
						System.Reflection.Assembly assy = assemblies[assyIndex];
						string[] assetList = assy.GetManifestResourceNames();
						for (int a=0; a<assetList.Length; a++) {
							Logger.AddEntry (this, LogMessageType.Persistent, "ER: " + assetList[a]);
							if (assetList[a].Contains("FPGA_FW_v2.bin"))
							{
								inStream = assy.GetManifestResourceStream(assetList[a]);
								reader = new BinaryReader(inStream);
								Logger.AddEntry (this, LogMessageType.Persistent, "Connected to FW Flash file");
							}
						}
					}	
				}

				//show all assets
				/*string[] assetList2 = Assets.List("");
				for (int a=0; a<assetList2.Length; a++) {
					Logger.AddEntry (this, LogMessageType.Persistent, "Asset: "+assetList2[a]);
				}
				//inStream = Assets.Open(fileName, Android.Content.Res.Access.Streaming);
				//reader = new BinaryReader(inStream);
*/

				#else
				inStream = new FileStream (fileName, FileMode.Open);
				reader = new BinaryReader (inStream);            
				#endif
			}
			catch (Exception e){
				Logger.AddEntry (this, LogMessageType.Persistent, "Opening FPGA FW file failed");
				Logger.AddEntry (this, LogMessageType.Persistent, e.Message);
				return;
			}

			//DemoStatusText = "Entered method";
			if (!eDevice.HWInterface.Connected) {
				DemoStatusText += " || returning";
				return;
			}

			if (reader == null)
				return;

			try{
				//Logger.AddEntry (this, LogMessageType.Persistent, reader.BaseStream.Length.ToString() + " bytes in file");
			}
			catch (Exception e){
				Logger.AddEntry (this, LogMessageType.Persistent, e.Message);
				return;
			}



			ushort fileLength = 0;
			ushort requiredFiller = 0;
			try{
			fileLength = (ushort) reader.BaseStream.Length;

			requiredFiller = (ushort) (16 - (fileLength % 16));
            
			//prep PIC for FPGA flashing
			ushort totalBytesToSend = (ushort) (fileLength + requiredFiller + extendedPacketsToSend * 16);
			byte [] toSend1 = new byte[4];
			int i = 0;
			toSend1 [i++] = 123; //message for PIC
			toSend1 [i++] = 12; //HOST_COMMAND_FLASH_FPGA
			toSend1 [i++] = (byte) (totalBytesToSend >> 8);
			toSend1 [i++] = (byte) (totalBytesToSend);
			
			eDevice.HWInterface.WriteControlBytes (toSend1);
			}catch{
				Logger.AddEntry (this, LogMessageType.Persistent, "Preparing PIC for FPGA flashing failed");
				return;
			}

			//sleep, allowing PIC to erase memory
			System.Threading.Thread.Sleep (10);

			//now send all data in chunks of 16bytes
			ushort bytesSent = 0;
			while ((fileLength - bytesSent) != (16-requiredFiller)) {
				byte [] intermediate = reader.ReadBytes (16);
				try{
				eDevice.HWInterface.WriteControlBytes (intermediate);
				}catch{
					Logger.AddEntry (this, LogMessageType.Persistent, "Writing core FPGA flash data failed");
					return;
				}

				bytesSent += 16;
				//pb.Value = (int)((float)(bytesSent)/(float)(fileLength)*100f);
				//pb.Update();

				for (int ii = 0; ii < 16; ii++)
					dataSent.Add (intermediate [ii]);

				DemoStatusText = "Programming FPGA " + bytesSent.ToString ();
			}

			//in case filelengt is not multiple of 16: fill with FF
			if (requiredFiller > 0) {
				byte [] lastData = new byte[16];
				for (int j = 0; j < 16-requiredFiller; j++)
					lastData [j] = reader.ReadByte ();
				for (int j = 0; j < requiredFiller; j++)
					lastData [16 - requiredFiller + j] = 255;	
				try{
				eDevice.HWInterface.WriteControlBytes (lastData);
				}catch{
					Logger.AddEntry (this, LogMessageType.Persistent, "Writing filler failed");
					return;
				}
                
				for (int ii = 0; ii < 16; ii++)
					dataSent.Add (lastData [ii]);

				DemoStatusText = "Sending filler " + requiredFiller.ToString ();
			}

			//now send 2048 more packets, allowing the FPGA to boot correctly            
			bytesSent = 0;
			for (int j = 0; j < extendedPacketsToSend; j++) {
				try{
				eDevice.HWInterface.WriteControlBytes (extendedData);
				}catch{
					Logger.AddEntry (this, LogMessageType.Persistent, "Sending extended FW flash data failed");
					return;
				}
				bytesSent += 16;
				//pb.Value = (int)((float)(bytesSent) / (float)(extendedPacketsToSend * 16) * 100f);
				//pb.Update();

				for (int ii = 0; ii < 16; ii++)
					dataSent.Add (extendedData [ii]);

				DemoStatusText = "Sending postamp " + j.ToString ();
			}

			//close down
			try{
			reader.Close ();
			inStream.Close ();
			}catch{
				Logger.AddEntry (this, LogMessageType.Persistent, "Closing FPGA FW file failed");
				return;
			}


			DemoStatusText = "";
		/*}
			catch{
				DemoStatusText = "Error during FPGA programming";
			}*/

        }
//#endif

        public override float[] GetDataAndConvertToVoltageValues()
        {
            int samplesToFetch = 4096;
            int bytesToFetch = samplesToFetch;
			byte[] rawData = eDevice.HWInterface.GetData(bytesToFetch);
            float[] voltageValues = new float[samplesToFetch];

            /*
            //this section converts twos complement to a physical voltage value
            for (int i = 0; i < rawData.Length; i++)
            {
                byte byteVal = (byte)rawData[i];
                float twosVal = (float)(sbyte)byteVal;
                float scaledVal = twosVal + 128;
                voltageValues[i] = scaledVal / 255f * 1.8f;
            }*/

            //this section converts twos complement to a physical voltage value
            for (int i = 0; i < rawData.Length; i++)
            {
                byte byteVal = (byte)rawData[i];

                bool calibration = false;
                if (calibration)
                    voltageValues[i] = (float)byteVal;
                else
                {
                    float yOffFPGA = (float)fpgaMemory.GetRegister(REG.CHB_YOFFSET_VOLTAGE).InternalValue;
                    float gainedOffset = yOffFPGA*calibrationCoefficients[1];
                    float yOffset = gainedOffset + calibrationCoefficients[2];
                    float gainedVal = (float)byteVal * calibrationCoefficients[0];
                    voltageValues[i] = (float)byteVal * calibrationCoefficients[0] + fpgaMemory.GetRegister(REG.CHB_YOFFSET_VOLTAGE).InternalValue * calibrationCoefficients[1] + calibrationCoefficients[2];
                }
            }

			if (true)
			{
	            if (fpgaMemory.GetRegister(REG.CALIB_VOLTAGE).InternalValue < 10)
	            {
	                fpgaMemory.GetRegister(REG.CALIB_VOLTAGE).InternalValue = 23;
	            }
	            else
	            {
	                fpgaMemory.GetRegister(REG.CALIB_VOLTAGE).InternalValue = 0;
	            }           
	            fpgaMemory.WriteSingle(REG.CALIB_VOLTAGE);
			}

            return voltageValues;            
        }

        public int FreqDivider 
        { 
            get 
            {
                fpgaMemory.ReadSingle(REG.SAMPLECLOCKDIVIDER_B1);
                fpgaMemory.ReadSingle(REG.SAMPLECLOCKDIVIDER_B0);
                return fpgaMemory.GetRegister(REG.SAMPLECLOCKDIVIDER_B1).InternalValue << 8 + fpgaMemory.GetRegister(REG.SAMPLECLOCKDIVIDER_B0).InternalValue+1;
            }
        }

        public int GetTriggerPos()
        {
            fpgaMemory.ReadSingle(REG.TRIGGERHOLDOFF_B1);
            fpgaMemory.ReadSingle(REG.TRIGGERHOLDOFF_B0);
            int msb = fpgaMemory.GetRegister(REG.TRIGGERHOLDOFF_B1).InternalValue;
            msb = msb << 8;
            int lsb = fpgaMemory.GetRegister(REG.TRIGGERHOLDOFF_B0).InternalValue;
            return msb + lsb + 1;
        }

        public void SetTriggerHorPos(int value)
        {
            value--;
            if (value < 0) value = 0;
            if (value > 2047) value = 2047;

            fpgaMemory.GetRegister(REG.TRIGGERHOLDOFF_B1).InternalValue = (byte)((value) >> 8);
            fpgaMemory.WriteSingle(REG.TRIGGERHOLDOFF_B1);
            fpgaMemory.GetRegister(REG.TRIGGERHOLDOFF_B0).InternalValue = (byte)((value));
            fpgaMemory.WriteSingle(REG.TRIGGERHOLDOFF_B0);
        }

		public void Temp()
		{
			temp = !temp;
		}

        //private nested classes, shielding this from the outside.
        //only Scop3v2 can instantiate this class!
        private class Scope3v2CalibrationVoltage: EInterfaces.ICalibrationVoltage
        {
            //private EFunctionality calibrationEnabled;
            private EFunctionality calibrationVoltage;

            //public EFunctionality CalibrationEnabled { get { return calibrationEnabled; } }
            public EFunctionality CalibrationVoltage { get { return calibrationVoltage; } }

            public Scope3v2CalibrationVoltage(Scop3v2 deviceImplementation)
            {
                this.calibrationVoltage = new EFCalibrationVoltage("Calibration voltage", "V", 0, deviceImplementation.fpgaMemory.GetRegister(REG.CALIB_VOLTAGE), 3.3f);
                //this.calibrationEnabled = new EFunctionality("Calibration enabled", "", deviceImplementation.strobeMemory, new string[] { STR.CHB_DIV1" }, F2H_CalibEnabled, H2F_CalibEnabled);
            }
        }

        private class Scope3v2TriggerPosition : EInterfaces.ITriggerPosition
        {
            private EFunctionality triggerPosition;
            public EFunctionality TriggerPosition { get { return triggerPosition; } }

            public Scope3v2TriggerPosition(Scop3v2 deviceImplementation)
            {
                this.triggerPosition = new EFTriggerPosition("Trigger position", "", 140, deviceImplementation.fpgaMemory.GetRegister(REG.TRIGGERLEVEL));
                //this.calibrationEnabled = new EFunctionality("Calibration enabled", "", deviceImplementation.strobeMemory, new string[] { STR.CHB_DIV1" }, F2H_CalibEnabled, H2F_CalibEnabled);
            }
        }

        private class Scope3v2ScopeChannelB : EInterfaces.IScopeChannel
        {
            private EFunctionality multiplicationFactor;
            private EFunctionality divisionFactor;
            private EFunctionality samplingFrequency;
            private EFOffset channelOffset;

            public EFunctionality MultiplicationFactor { get { return multiplicationFactor; } }
            public EFunctionality DivisionFactor { get { return divisionFactor; } }
            public EFunctionality SamplingFrequency { get { return samplingFrequency; } }
            public EFunctionality ChannelOffset { get { return channelOffset; } }

            public Scope3v2ScopeChannelB(Scop3v2 deviceImplementation)
            {
                EDeviceMemoryRegister[] multiplicationStrobes = new EDeviceMemoryRegister[3];
                multiplicationStrobes[0] = deviceImplementation.strobeMemory.GetRegister(STR.CHB_MULT1);
                multiplicationStrobes[1] = deviceImplementation.strobeMemory.GetRegister(STR.CHB_MULT2);
                multiplicationStrobes[2] = deviceImplementation.strobeMemory.GetRegister(STR.CHB_MULT3);

                int[] multiplyingResistors = new int[3] { 0, 1000, 6200 };

                this.channelOffset = new EFOffset("Channel offset", "V", 0);
                this.multiplicationFactor = new EFMultiplicationFactor("Multiplication factor", "", 1, multiplicationStrobes, 1000, multiplyingResistors, 24, channelOffset);
                this.divisionFactor = new EFDivisionFactor("Division factor", "", 1);
                this.samplingFrequency = new EFSamplingFrequency("Sampling Frequency", "Hz", 100000000);
            }

            public float MaxRange { get { return 255f; } }
            public float ScalingFactor { get { return multiplicationFactor.InternalValue / divisionFactor.InternalValue; } }
        }

        public void UploadToRAM(byte[] inData)
        {
            //raise global reset to reset RAM address counter, and to make sure the RAM switching is safe
            strobeMemory.GetRegister(STR.GLOBAL_RESET).InternalValue = 1;
            strobeMemory.WriteSingle(STR.GLOBAL_RESET);            

            //save previous ram config
            fpgaMemory.ReadSingle(REG.RAM_CONFIGURATION);
            byte previousRamConfiguration = fpgaMemory.GetRegister(REG.RAM_CONFIGURATION).InternalValue;

            //set ram config to I2C input
            fpgaMemory.GetRegister(REG.RAM_CONFIGURATION).InternalValue = 2; //sets RAM0 to I2C input
            fpgaMemory.WriteSingle(REG.RAM_CONFIGURATION);            

            //lower global reset
            strobeMemory.GetRegister(STR.GLOBAL_RESET).InternalValue = 0;
            strobeMemory.WriteSingle(STR.GLOBAL_RESET);

            //break data up into blocks of 8bytes
            int blockSize = 8;
            int fullLength = inData.Length;
            int blockCounter = 0;

            while (blockCounter * blockSize < fullLength) // as long as not all data has been sent
            {

                ///////////////////////////////////////////////////////////////////////////
                //////Start sending data
                byte[] toSend = new byte[5 + blockSize];

                //prep header
                int i = 0;
                toSend[i++] = 123; //message for FPGA
                toSend[i++] = 10; //I2C send
                toSend[i++] = (byte)(blockSize + 2); //data and 2 more bytes: the FPGA I2C address, and the register address inside the FPGA
                toSend[i++] = (byte)(7 << 1); //first I2C byte: FPGA i2c address for RAM writing(7) + '0' as LSB, indicating write operation
                toSend[i++] = (byte)0; //second I2C byte: dummy!

                //append data to be sent
                for (int c = 0; c < blockSize; c++)
                    toSend[i++] = inData[blockCounter * blockSize + c];

                eDevice.HWInterface.WriteControlBytes(toSend);

                blockCounter++;
            }

            //set ram config to original state
            fpgaMemory.GetRegister(REG.RAM_CONFIGURATION).InternalValue = previousRamConfiguration; //sets RAM0 to I2C input
            fpgaMemory.WriteSingle(REG.RAM_CONFIGURATION);

            //lower global reset
            strobeMemory.GetRegister(STR.GLOBAL_RESET).InternalValue = 0;
            strobeMemory.WriteSingle(STR.GLOBAL_RESET);
        }

        public void DemoLCTank()
        {
            fpgaMemory.GetRegister(REG.TRIGGERLEVEL).InternalValue = 78;
            fpgaMemory.WriteSingle(REG.TRIGGERLEVEL);

            fpgaMemory.GetRegister(REG.TRIGGERHOLDOFF_B0).InternalValue = 230;
            fpgaMemory.WriteSingle(REG.TRIGGERHOLDOFF_B0);

            fpgaMemory.GetRegister(REG.TRIGGERHOLDOFF_B1).InternalValue = 3;
            fpgaMemory.WriteSingle(REG.TRIGGERHOLDOFF_B1);

            fpgaMemory.GetRegister(REG.CHB_YOFFSET_VOLTAGE).InternalValue = 90;
            fpgaMemory.WriteSingle(REG.CHB_YOFFSET_VOLTAGE);

            strobeMemory.GetRegister(STR.FREE_RUNNING).InternalValue = 0;
            strobeMemory.WriteSingle(STR.FREE_RUNNING);

            strobeMemory.GetRegister(STR.CHB_DIV10).InternalValue = 1;
            strobeMemory.WriteSingle(STR.CHB_DIV10);

            strobeMemory.GetRegister(STR.CHB_MULT1).InternalValue = 0;
            strobeMemory.WriteSingle(STR.CHB_MULT1);

            fpgaMemory.GetRegister(REG.SAMPLECLOCKDIVIDER_B1).InternalValue = 0;
            fpgaMemory.WriteSingle(REG.SAMPLECLOCKDIVIDER_B1);
        }

		public void ChangeOffset(int channel, int amount)
		{
			if (channel == 0) {
				int newVal = (int)fpgaMemory.GetRegister (REG.CHA_YOFFSET_VOLTAGE).InternalValue + amount;
				fpgaMemory.GetRegister (REG.CHA_YOFFSET_VOLTAGE).InternalValue = (byte)newVal;
				fpgaMemory.WriteSingle(REG.CHA_YOFFSET_VOLTAGE);
			} else {
				int newVal = (int)fpgaMemory.GetRegister (REG.CHB_YOFFSET_VOLTAGE).InternalValue + amount;
				fpgaMemory.GetRegister (REG.CHB_YOFFSET_VOLTAGE).InternalValue = (byte)newVal;
				fpgaMemory.WriteSingle(REG.CHB_YOFFSET_VOLTAGE);
			}
		}

        public void DemoArduino()
        {
            fpgaMemory.GetRegister(REG.TRIGGERLEVEL).InternalValue = 119;
            fpgaMemory.WriteSingle(REG.TRIGGERLEVEL);

            fpgaMemory.GetRegister(REG.TRIGGERHOLDOFF_B0).InternalValue = 55;
            fpgaMemory.WriteSingle(REG.TRIGGERHOLDOFF_B0);

            fpgaMemory.GetRegister(REG.TRIGGERHOLDOFF_B1).InternalValue = 1;
            fpgaMemory.WriteSingle(REG.TRIGGERHOLDOFF_B1);

            fpgaMemory.GetRegister(REG.CHA_YOFFSET_VOLTAGE).InternalValue = 35;
			fpgaMemory.WriteSingle(REG.CHA_YOFFSET_VOLTAGE);

            fpgaMemory.GetRegister(REG.CHB_YOFFSET_VOLTAGE).InternalValue = 36;
			fpgaMemory.WriteSingle(REG.CHB_YOFFSET_VOLTAGE);

            fpgaMemory.GetRegister(REG.SAMPLECLOCKDIVIDER_B1).InternalValue = 100;
            fpgaMemory.WriteSingle(REG.SAMPLECLOCKDIVIDER_B1);

            strobeMemory.GetRegister(STR.FREE_RUNNING).InternalValue = 0;
            strobeMemory.WriteSingle(STR.FREE_RUNNING);


            strobeMemory.GetRegister(STR.CHB_DIV1).InternalValue = 0;
            strobeMemory.WriteSingle(STR.CHB_DIV1);

            strobeMemory.GetRegister(STR.CHB_DIV10).InternalValue = 1;
            strobeMemory.WriteSingle(STR.CHB_DIV10);

            strobeMemory.GetRegister(STR.CHB_MULT1).InternalValue = 1;
            strobeMemory.WriteSingle(STR.CHB_MULT1);

            strobeMemory.GetRegister(STR.CHB_MULT2).InternalValue = 1;
            strobeMemory.WriteSingle(STR.CHB_MULT2);

            strobeMemory.GetRegister(STR.CHA_DIV1).InternalValue = 1;
            strobeMemory.WriteSingle(STR.CHA_DIV1);

            strobeMemory.GetRegister(STR.CHA_DIV10).InternalValue = 0;
            strobeMemory.WriteSingle(STR.CHA_DIV10);

            strobeMemory.GetRegister(STR.CHA_MULT1).InternalValue = 0;
            strobeMemory.WriteSingle(STR.CHA_MULT1);
            
            strobeMemory.GetRegister(STR.CHA_MULT2).InternalValue = 0;
            strobeMemory.WriteSingle(STR.CHA_MULT2);
        }
    }
}
