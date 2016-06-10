using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LabNation.DeviceInterface.Hardware
{
    public sealed class DummyInterface : IHardwareInterface
    { 
        public string Serial { get; private set; }
        private DummyInterface(string serial)
        {
            this.Serial = serial; 
        }

        public static DummyInterface Audio = new DummyInterface("Audio");
        public static DummyInterface Generator = new DummyInterface("Generator");
        public static DummyInterface File = new DummyInterface("File");
    }
}
