using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.MemoryRegisters
{    
    public class ByteRegister: EDeviceMemoryRegister
    {
        private byte internalValue;
        private int address;
        private string name;
        //private bool readOnly;
        
        public ByteRegister(int address, string name, EDeviceMemory parentMemory)
        {
            //this.readOnly = readOnly;
            this.internalValue = 0;
            this.address = address;
            this.name = name;
            this.parentMemory = parentMemory;
        }

        public override int MaxValue { get { return 255; } }
        public override int Address { get { return address; } }
        public override string Name { get { return name; } }
        public override event RegisterInternalValueChangedHandler OnInternalValueChanged;

        public override EDeviceMemoryRegister set(object value)
        {
            if(!(value is byte))
                throw new Exception("Cannot convert " + value.GetType() + " to byte");
            this.internalValue = (byte)value;

            //fire event, so linked values and GUIs can update
            if (OnInternalValueChanged != null)
                OnInternalValueChanged(this, new EventArgs());
            return this;
        }

        //converts incoming value to internal value
        public override byte InternalValue
        {
            get
            {
                return this.internalValue;
            }
            set
            {
                this.set(value);
            }
        }
    }
}
