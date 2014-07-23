using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.DeviceMemories
{    
    public delegate void RegisterValueChangedHandler(object o, EventArgs e);

    public abstract class MemoryRegister
    {
        public DeviceMemory Memory { get; private set; }
        public string Name { get; private set; }
        public uint Address { get; private set; }
        public bool Dirty { get; private set; }

        internal MemoryRegister(DeviceMemory memory, uint address, string name)
        {
            Address = address;
            Name = name;
            Memory = memory;
            Dirty = true;
        }


#if INTERNAL
        public
#else
        internal
#endif
        abstract object Get();

#if INTERNAL
        public
#else
        internal
#endif
        abstract MemoryRegister Set(object value);

#if INTERNAL
        public
#else
        internal
#endif
        void Write()
        {
            this.Memory.Write(this.Address);
        }

#if INTERNAL
        public
#else
        internal
#endif
        void Write(object value)
        {
            this.Set(value).Write();
        }

#if INTERNAL
        public
#else
        internal
#endif
        MemoryRegister Read()
        {
            this.Memory.Read(this.Address);
            return this;
        }

#if INTERNAL
        public event RegisterValueChangedHandler OnInternalValueChanged;
#endif
        protected void CallValueChangedCallbacks()
        {
#if INTERNAL
            if (OnInternalValueChanged != null)
                OnInternalValueChanged(this, new EventArgs());
#endif
        }


        public abstract int MaxValue { get; }
    }
}
