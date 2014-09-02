using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.Devices
{
    public abstract class Channel
    {
        public static int CompareByOrder(Channel a, Channel b)
        {
            return a.Order - b.Order;
        }
        private static int order = 0;
        public string Name { get; protected set; }
        public int Value { get; protected set; }
        public static explicit operator int(Channel ch) { return ch.Value; }

        public int Order { get; protected set; }
        private static HashSet<Channel> list = new HashSet<Channel>();
        public static IList<Channel> List { get { return list.ToList().AsReadOnly(); } }
        public Channel(string name, int value)
        {
            this.Name = name; 
            this.Value = value;
            this.Order = order;
            order++;
            list.Add(this);
        }

        public override string ToString()
        {
            return this.Name;
        }
    }
    public sealed class AnalogChannel : Channel 
    {
        private static HashSet<AnalogChannel> list = new HashSet<AnalogChannel>();
        new public static IList<AnalogChannel> List { get { return list.ToList().AsReadOnly(); } }
        private AnalogChannel(string name, int value) : base(name, value) { 
            list.Add(this);
        }

        public static readonly AnalogChannel ChA = new AnalogChannel("A", 0);
        public static readonly AnalogChannel ChB = new AnalogChannel("B", 1);
    }
    public sealed class DigitalChannel : Channel {
        private static HashSet<DigitalChannel> list = new HashSet<DigitalChannel>();
        new public static IList<DigitalChannel> List { get { return list.ToList().AsReadOnly(); } }
        private DigitalChannel(string name, int value) : base(name, value) { list.Add(this); }

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
        private LogicAnalyserChannel(string name, int value) : base(name, value) { list.Add(this); }

        public static readonly LogicAnalyserChannel LogicAnalyser = new LogicAnalyserChannel("LA", 0);
    }

}
