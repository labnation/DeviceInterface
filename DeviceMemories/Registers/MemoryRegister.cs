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
        
        private bool _dirty;
        public bool Dirty
        {
            get { return _dirty; }
            internal set
            {
                lock (_internalValueLock)
                {
                    _dirty = value;
                }
            }
        }
        private object __internalValue;
        protected object _internalValueLock = new object();
        protected object _internalValue { 
            get { return __internalValue;}
            set
            {
                lock (_internalValueLock)
                {
                    __internalValue = value;
                    this.Dirty = true;
                }
            }
        }

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
        void WriteImmediate()
        {
            this.Memory.Write(this.Address);
        }

#if INTERNAL
        public
#else
        internal
#endif
        void WriteImmediate(object value)
        {
            this.Set(value).WriteImmediate();
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
