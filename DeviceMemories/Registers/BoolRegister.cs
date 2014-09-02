using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.DeviceMemories
{
#if INTERNAL
    public
#else
    internal
#endif
    class BoolRegister : MemoryRegister
    {
        private bool internalValue
        {
            get { return (bool)_internalValue; }
            set { _internalValue = value; }
        }

        public BoolRegister(DeviceMemory memory, uint address, string name) : base(memory, address, name) 
        {
            this.internalValue = false;
        }

        public override MemoryRegister Set(object value)
        {
            this.internalValue = (bool)value;
            CallValueChangedCallbacks();
            return this;
        }

        public override object Get() { return this.internalValue; }

        public bool GetBool() { return this.internalValue; }

        public new BoolRegister Read() { return (BoolRegister)base.Read(); }
        
        public override int MaxValue { get { return 1; } }
    }
}
