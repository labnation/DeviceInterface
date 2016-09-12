using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LabNation.DeviceInterface.Memories
{
#if DEBUG
    public
#else
    internal
#endif
    delegate void RegisterValueChangedHandler(object o, EventArgs e);

#if DEBUG
    public
#else
    internal
#endif
    abstract class MemoryRegister
    {
        public DeviceMemory Memory { get; private set; }
        public string Name { get; internal set; }
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
            Dirty = false;
        }


        public abstract object Get();

        public abstract MemoryRegister Set(object value);

        public void WriteImmediate()
        {
            this.Memory.Write(this.Address);
        }

        public void WriteImmediate(object value)
        {
            this.Set(value).WriteImmediate();
        }

        public MemoryRegister Read()
        {
            this.Memory.Read(this.Address);
            return this;
        }

#if DEBUG
        public event RegisterValueChangedHandler OnInternalValueChanged;
#endif
        protected void CallValueChangedCallbacks()
        {
#if DEBUG
            if (OnInternalValueChanged != null)
                OnInternalValueChanged(this, new EventArgs());
#endif
        }


        public abstract int MaxValue { get; }
    }
}
