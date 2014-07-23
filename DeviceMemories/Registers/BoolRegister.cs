using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.DeviceMemories
{
    public class BoolRegister : MemoryRegister
    {
        private bool internalValue
        {
            get { return (bool)_internalValue; }
            set { _internalValue = value; }
        }

        internal BoolRegister(DeviceMemory memory, uint address, string name) : base(memory, address, name) 
        {
            this.internalValue = false;
        }

#if INTERNAL
        public
#else
        internal
#endif
        override MemoryRegister Set(object value)
        {
            this.internalValue = (bool)value;
            CallValueChangedCallbacks();
            return this;
        }

#if INTERNAL
        public
#else
        internal
#endif
        override object Get() { return this.internalValue; }
#if INTERNAL
        public
#else
        internal
#endif
        bool GetBool() { return this.internalValue; }

#if INTERNAL
        public
#else
        internal
#endif
        new BoolRegister Read() { return (BoolRegister)base.Read(); }
        
        public override int MaxValue { get { return 1; } }
    }
}
