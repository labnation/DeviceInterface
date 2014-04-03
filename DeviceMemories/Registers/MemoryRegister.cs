using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.DeviceMemories
{    
    public delegate void RegisterInternalValueChangedHandler(object o, EventArgs e);

    //abstract class representing one registers.
    //concrete siblings can be 8bit register, a string, etc

    //simple class, mainly used for converting incoming data to native data, and checking whether the resulting value is not out of range
    public class MemoryRegister<T>
    {
        protected DeviceMemory<MemoryRegister<T>> parentMemory;
        protected string name;
        protected int address;
        protected T internalValue;

        public MemoryRegister(int address, string name)
        {
            //this.readOnly = readOnly;
            //this.internalValue = 0;
            this.address = address;
            this.name = name;
            //this.parentMemory = parentMemory;
        }
        public DeviceMemory<MemoryRegister<T>> ParentMemory { get { return this.parentMemory; } }
        public event RegisterInternalValueChangedHandler OnInternalValueChanged;

        //abstract public bool ReadOnly { get; }
        public T InternalValue { get { return this.internalValue; } set { this.internalValue = value; } }
        public string Name { get { return this.name; } }
        public int Address { get { return this.address; } }

        public T Get() { return this.internalValue; }

        public MemoryRegister<T> Set(T value)
        {
            this.internalValue = value;

            //fire event, so linked values and GUIs can update
            if (OnInternalValueChanged != null)
                OnInternalValueChanged(this, new EventArgs());
            return this;
        }
        public static int MaxValue { get { 
            //FIXME: make generic
            if (typeof(T) == typeof(byte)) return 255;
            else if (typeof(T) == typeof(bool)) return 1;
            else return -1;
        } }
    }
}
