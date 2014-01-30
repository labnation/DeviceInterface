using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore
{    
    public delegate void RegisterInternalValueChangedHandler(object o, EventArgs e);

    //abstract class representing one registers.
    //concrete siblings can be 8bit register, a string, etc

    //simple class, mainly used for converting incoming data to native data, and checking whether the resulting value is not out of range
    abstract public class EDeviceMemoryRegister
    {
        protected EDeviceMemory parentMemory;

        abstract public event RegisterInternalValueChangedHandler OnInternalValueChanged;

        //abstract public bool ReadOnly { get; }
        abstract public byte InternalValue { get; set; }
        abstract public int MaxValue { get; }
        abstract public string Name { get; }

        public EDeviceMemory ParentMemory { get { return parentMemory; } }
    }
}
