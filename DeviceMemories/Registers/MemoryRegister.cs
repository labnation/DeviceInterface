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

        public MemoryRegister(DeviceMemory memory, uint address, string name)
        {
            Address = address;
            Name = name;
            Memory = memory;
        }
        public event RegisterValueChangedHandler OnInternalValueChanged;

        public abstract object Get();
        public abstract MemoryRegister Set(object value);

        public void Write()
        {
            this.Memory.WriteSingle(this.Address);
        }

        public MemoryRegister Read()
        {
            this.Memory.ReadSingle(this.Address);
            return this;
        }

        protected void CallValueChangedCallbacks()
        {
            if (OnInternalValueChanged != null)
                OnInternalValueChanged(this, new EventArgs());
        }
        public abstract int MaxValue { get; }
    }
}
