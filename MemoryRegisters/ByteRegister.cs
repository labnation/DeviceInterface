using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.MemoryRegisters
{    
    public class ByteRegister: EDeviceMemoryRegister
    {
        private byte internalValue;
        private string name;
        //private bool readOnly;
        
        public ByteRegister(string name, EDeviceMemory parentMemory)
        {
            //this.readOnly = readOnly;
            this.internalValue = 0;
            this.name = name;
            this.parentMemory = parentMemory;
        }

        public override int MaxValue { get { return 255; } }
        public override string Name { get { return name; } }
        public override event RegisterInternalValueChangedHandler OnInternalValueChanged;

        //converts incoming value to internal value
        public override byte InternalValue
        {
            get
            {
                return this.internalValue;
            }
            set
            {
                this.internalValue = value;

                //fire event, so linked values and GUIs can update
                //if (OnInternalValueChanged != null)
                //    OnInternalValueChanged(this, new EventArgs());
            }
        }
    }
}
