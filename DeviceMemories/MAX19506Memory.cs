using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.DeviceMemories
{
    //this class defines which type of registers it contain, how much of them, and how to access them
    //actual filling of these registers must be defined by the specific HWImplementation, through the constructor of this class
    public class MAX19506Memory: EDeviceMemory
    {
        private EDeviceMemory fpgaMemory;
        private EDeviceMemory strobeMemory;
        private EDeviceMemory romMemory;

        //this method defines which type of registers are stored in the memory
        public MAX19506Memory(EDevice eDevice, EDeviceMemory fpgaMemory, EDeviceMemory strobeMemory, EDeviceMemory romMemory)
        {
            this.eDevice = eDevice;
            this.fpgaMemory = fpgaMemory;
            this.strobeMemory = strobeMemory;
            this.romMemory = romMemory;

            //fetch list of register names from predefined list
            registerIndices = PredefinedStrobeNames();
                        
            //look up how many registers are required
            int largestIndex = 0;
            foreach (KeyValuePair<string, int> kvp in registerIndices)
                if (kvp.Value > largestIndex) 
                    largestIndex = kvp.Value;

            //instantiate registerList
            registers = new List<EDeviceMemoryRegister>();
            for (int i = 0; i < largestIndex+1; i++)
            {
                //find name of this register
                string regName = "<none>";
                foreach (KeyValuePair<string, int> kvp in registerIndices)
                    if (kvp.Value == i)
                        regName = kvp.Key;

                registers.Add(new MemoryRegisters.ByteRegister(regName, this));
            }

        }

        private Dictionary<string, int> PredefinedStrobeNames()
        {
            Dictionary<string, int> strobeNames = new Dictionary<string, int>();
            strobeNames.Add("MAX_POWER_MANAGEMENT", 0);
            strobeNames.Add("MAX_OUTPUT_FORMAT", 1);
            strobeNames.Add("MAX_OUTPUT_PWR_MNGMNT", 2);
            strobeNames.Add("MAX_DATA/CLK_TIMING", 3);
            strobeNames.Add("MAX_CHA_TERMINATION", 4);
            strobeNames.Add("MAX_CHB_TERMINATION", 5);
            strobeNames.Add("MAX_FORMAT/PATTERN", 6);
            strobeNames.Add("MAX_COMMON_MODE", 8);
            strobeNames.Add("MAX_SOFT_RESET", 10);

            return strobeNames;
        }

        public override void ReadRange(int startAddress, int burstSize)
        {
            for (int i = 0; i < burstSize; i++)
            {
                //first send correct address to FPGA
                int address = startAddress + i;
                fpgaMemory.RegisterByName("REG_SPI_ADDRESS").InternalValue = (byte)(address+128); //for a read, MSB must be 1
                fpgaMemory.WriteSingle("REG_SPI_ADDRESS");

                //next, trigger rising edge to initiate SPI comm
                strobeMemory.RegisterByName("STR_INIT_SPI_TRANSFER").InternalValue = 0;
                strobeMemory.WriteSingle("STR_INIT_SPI_TRANSFER");
                strobeMemory.RegisterByName("STR_INIT_SPI_TRANSFER").InternalValue = 1;
                strobeMemory.WriteSingle("STR_INIT_SPI_TRANSFER");

                //finally read acquired value
                romMemory.ReadSingle("ROM_SPI_RECEIVED_VALUE");
                int acquiredVal = romMemory.RegisterByName("ROM_SPI_RECEIVED_VALUE").InternalValue;
                Registers[address].InternalValue = (byte)acquiredVal;
            }            
            
        }

        public override void WriteRange(int startAddress, int burstSize)
        {
            for (int i = 0; i < burstSize; i++)
            {
                //first send correct address to FPGA
                int address = startAddress + i; //for a write, MSB must be 0
                fpgaMemory.RegisterByName("REG_SPI_ADDRESS").InternalValue = (byte)address;
                fpgaMemory.WriteSingle("REG_SPI_ADDRESS");

                //next, send the write value to FPGA
                int valToWrite = Registers[address].InternalValue;
                fpgaMemory.RegisterByName("REG_SPI_WRITE_VALUE").InternalValue = (byte)valToWrite;
                fpgaMemory.WriteSingle("REG_SPI_WRITE_VALUE");

                //finally, trigger rising edge
                strobeMemory.RegisterByName("STR_INIT_SPI_TRANSFER").InternalValue = 0;
                strobeMemory.WriteSingle("STR_INIT_SPI_TRANSFER");
                strobeMemory.RegisterByName("STR_INIT_SPI_TRANSFER").InternalValue = 1;
                strobeMemory.WriteSingle("STR_INIT_SPI_TRANSFER");
            }
        }
    }
}
