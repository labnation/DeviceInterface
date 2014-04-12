using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.DeviceMemories
{
    public class ByteRegister : MemoryRegister
    {
        private byte internalValue;

        public ByteRegister(int address, string name) : base(address, name) { }

        public override MemoryRegister Set(object value)
        {
            if (value.GetType().Equals(typeof(byte)))
                throw new Exception("Cannot set byte register with that kind of object (" + value.GetType().Name + ")");
            return this.Set((byte)value);
        }

        public ByteRegister Set(byte value) {
            this.internalValue = value;
            CallValueChangedCallbacks();
            return this;
        }
        
        public override object Get() { return this.internalValue; }
        public byte GetByte() { return this.internalValue; }
        public override int MaxValue { get { return 255; } }
    }
}
