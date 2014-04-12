using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.DeviceMemories
{
    public class BoolRegister : MemoryRegister
    {
        private bool internalValue;

        public BoolRegister(int address, string name) : base(address, name) { }

        public override MemoryRegister Set(object value)
        {
            this.internalValue = (bool)value;
            CallValueChangedCallbacks();
            return this;
        }
        
        public override object Get() { return this.internalValue; }
        public bool GetBool() { return this.internalValue; }
        public override int MaxValue { get { return 1; } }
    }
}
