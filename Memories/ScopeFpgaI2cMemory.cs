using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.Hardware;

namespace LabNation.DeviceInterface.Memories
{
    public class ScopeFpgaI2cMemory: ByteMemory
    {
        public ISmartScopeInterface hwInterface;
        private byte I2cAddress;
        private bool readOnly;

        protected Dictionary<uint, MemoryRegister> registers = new Dictionary<uint, MemoryRegister>();
        public override Dictionary<uint, MemoryRegister> Registers { get { return this.registers; } }

        public ScopeFpgaI2cMemory(ISmartScopeInterface hwInterface, byte I2cAddress, int size = 0, bool readOnly = false)
        {
            if (I2cAddress != (I2cAddress & 0x7f))
                throw new Exception(string.Format("I2c Address too large to be an I2C address: {0:X}", I2cAddress));
            
            for(uint i =0; i < size; i++)
                Registers.Add(i, new ByteRegister(this, i, "Reg[" + i.ToString() + "]"));
            this.hwInterface = hwInterface;
            this.I2cAddress = I2cAddress;
            this.readOnly = readOnly;
        }

        public override void Read(uint address)
        {
            byte[] data = null;
            hwInterface.GetControllerRegister(ScopeController.FPGA, ConvertAddress(address), 1, out data);
            Registers[address].Set(data[0]);
            Registers[address].Dirty = false;
        }

        public override void Write(uint address)
        {
            if (readOnly) return;

            byte[] data = new byte[] { this[address].GetByte() };
            hwInterface.SetControllerRegister(ScopeController.FPGA, ConvertAddress(address), data);
            Registers[address].Dirty = false;
        }

        private uint ConvertAddress(uint addr)
        {
            return (uint)((addr & 0xff) + (I2cAddress << 8));
        }
    }
}
