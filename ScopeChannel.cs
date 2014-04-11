using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore
{
    public static class ScopeChannels
    {
        public static ScopeChannel Undefined = null;

        public static DigitalChannel Digi0 = new DigitalChannel("Digital 0", 0);
        public static DigitalChannel Digi1 = new DigitalChannel("Digital 1", 1);
        public static DigitalChannel Digi2 = new DigitalChannel("Digital 2", 2);
        public static DigitalChannel Digi3 = new DigitalChannel("Digital 3", 3);
        public static DigitalChannel Digi4 = new DigitalChannel("Digital 4", 4);
        public static DigitalChannel Digi5 = new DigitalChannel("Digital 5", 5);
        public static DigitalChannel Digi6 = new DigitalChannel("Digital 6", 6);
        public static DigitalChannel Digi7 = new DigitalChannel("Digital 7", 7);

        public static AnalogChannel ChA  = new AnalogChannel("Channel A", 0);
        public static AnalogChannel ChB  = new AnalogChannel("Channel B", 1);
        public static AnalogChannel Math = new AnalogChannel("Math", 2);

        public static ProtocolChannel I2c = new ProtocolChannel("I2C", 0);
    }

    public class ScopeChannel
    {
        public string Name { get; protected set; }
        public int Value { get; protected set; }
        public static List<ScopeChannel> list = new List<ScopeChannel>();
        public ScopeChannel(string name, int value)
        {
            this.Name = name; 
            this.Value = value;
            list.Add(this);
        }
    }
    public class AnalogChannel : ScopeChannel 
    {
        new public static List<AnalogChannel> list = new List<AnalogChannel>();
        public AnalogChannel(string name, int value) : base(name, value) { list.Add(this); } 
    }
    public class DigitalChannel : ScopeChannel {
        new public static List<DigitalChannel> list = new List<DigitalChannel>();
        public DigitalChannel(string name, int value) : base(name, value) { list.Add(this); } 
    }
    public class ProtocolChannel : ScopeChannel {
        new public static List<ProtocolChannel> list = new List<ProtocolChannel>();
        public ProtocolChannel(string name, int value) : base(name, value) { list.Add(this);  } 
    }
}
