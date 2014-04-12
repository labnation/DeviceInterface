using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.DeviceMemories
{    
    public delegate void RegisterValueChangedHandler(object o, EventArgs e);

    public abstract class MemoryRegister
    {
        public string Name { get; private set; }
        public int Address { get; private set; }

        public MemoryRegister(int address, string name)
        {
            Address = address;
            Name = name;
        }
        public event RegisterValueChangedHandler OnInternalValueChanged;

        public abstract object Get();
        public abstract MemoryRegister Set(object value);

        protected void CallValueChangedCallbacks()
        {
            if (OnInternalValueChanged != null)
                OnInternalValueChanged(this, new EventArgs());
        }
        public abstract int MaxValue { get; }
    }
}
