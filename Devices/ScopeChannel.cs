using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LabNation.DeviceInterface.Devices
{    
    public abstract class Channel
    {
        public static int CompareByOrder(Channel a, Channel b)
        {
            return a.Order - b.Order;
        }
        private static int order = 0;
        public virtual bool Destructable { get { return false; } }
        public string Name { get; protected set; }
        public int Value { get; protected set; }
        public Type DataType { get; protected set; }
        public static explicit operator int(Channel ch) { return ch.Value; }

        public int Order { get; protected set; }
        private static HashSet<Channel> list = new HashSet<Channel>();
        public static IList<Channel> List { get { return list.ToList().AsReadOnly(); } }
        public Channel(string name, int value, Type dataType)
        {
            this.Name = name; 
            this.Value = value;
            this.Order = order;
            this.DataType = dataType;
            order++;
            list.Add(this);
        }
        public static void Destroy(Channel ch)
        {
            if (ch.Destructable)
                list.Remove(ch);
        }

        static public implicit operator string(Channel ch)
        {
            return ch == null ? null : ch.GetType().Name + "-" + ch.Name;
        }
    }
    public sealed class AnalogChannel : Channel 
    {
        private static HashSet<AnalogChannel> list = new HashSet<AnalogChannel>();
        new public static IList<AnalogChannel> List { get { return list.ToList().AsReadOnly(); } }
        private AnalogChannel(string name, int value) : base(name, value, typeof(float)) { 
            list.Add(this);
        }
        static public implicit operator AnalogChannel(string chName)
        {
            return list.Where(x => x == chName).First();
        }
        static public explicit operator AnalogChannelRaw(AnalogChannel ch)
        {
            return AnalogChannelRaw.List.Single(x => x.Value == ch.Value);
        }

        public static readonly AnalogChannel ChA = new AnalogChannel("A", 0);
        public static readonly AnalogChannel ChB = new AnalogChannel("B", 1);
    }

    public class AnalogChannelRaw : Channel
    {
        private static HashSet<AnalogChannelRaw> list;
        new public static IList<AnalogChannelRaw> List
        {
            get
            {
                if (list == null)
                {
                    list = new HashSet<AnalogChannelRaw>();
                    foreach (AnalogChannel ch in AnalogChannel.List)
                    {
                        list.Add(new AnalogChannelRaw(ch));
                    }
                }
                return list.ToList().AsReadOnly();
            }
        }
        private AnalogChannelRaw(AnalogChannel ch) : base(ch.Name, ch.Value, typeof(byte)) { }
    }

    public static class ChannelHelpers
    {
        public static AnalogChannelRaw Raw(this AnalogChannel ch)
        {
            return (AnalogChannelRaw)ch;
        }
    }

    public sealed class DigitalChannel : Channel {
        private static HashSet<DigitalChannel> list = new HashSet<DigitalChannel>();
        new public static IList<DigitalChannel> List { get { return list.ToList().AsReadOnly(); } }
        private DigitalChannel(string name, int value) : base(name, value, typeof(byte)) { 
            list.Add(this); 
        }
        static public implicit operator DigitalChannel(string chName)
        {
            return list.Where(x => x == chName).First();
        }

        public static readonly DigitalChannel Digi0 = new DigitalChannel("0", 0);
        public static readonly DigitalChannel Digi1 = new DigitalChannel("1", 1);
        public static readonly DigitalChannel Digi2 = new DigitalChannel("2", 2);
        public static readonly DigitalChannel Digi3 = new DigitalChannel("3", 3);
        public static readonly DigitalChannel Digi4 = new DigitalChannel("4", 4);
        public static readonly DigitalChannel Digi5 = new DigitalChannel("5", 5);
        public static readonly DigitalChannel Digi6 = new DigitalChannel("6", 6);
        public static readonly DigitalChannel Digi7 = new DigitalChannel("7", 7);
    }
    public sealed class LogicAnalyserChannel : Channel
    {
        private static HashSet<LogicAnalyserChannel> list = new HashSet<LogicAnalyserChannel>();
        new public static IList<LogicAnalyserChannel> List { get { return list.ToList().AsReadOnly(); } }
        private LogicAnalyserChannel(string name, int value) : base(name, value, typeof(byte)) { list.Add(this); }

        public static readonly LogicAnalyserChannel LA = new LogicAnalyserChannel("LA", 0);
    }

}
