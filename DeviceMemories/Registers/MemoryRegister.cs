using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.DeviceMemories
{    
    public delegate void RegisterInternalValueChangedHandler(object o, EventArgs e);

    //simple class, mainly used for converting incoming data to native data, and checking whether the resulting value is not out of range
    public abstract class MemoryRegister
    {
        protected string name;
        protected int address;

        public MemoryRegister(int address, string name)
        {
            this.address = address;
            this.name = name;
        }
        public event RegisterInternalValueChangedHandler OnInternalValueChanged;

        //abstract public bool ReadOnly { get; }
        //public object InternalValue { get { return this.internalValue; } set { this.Set(value); } }
        public string Name { get { return this.name; } }
        public int Address { get { return this.address; } }

        public abstract object Get();
        public abstract MemoryRegister Set(object value);

        protected void CallValueChangedCallbacks()
        {
            //fire event, so linked values and GUIs can update
            if (OnInternalValueChanged != null)
                OnInternalValueChanged(this, new EventArgs());
        }
        public abstract int MaxValue { get; }
    }
}
