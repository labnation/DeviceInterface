using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;
using System.Threading;
using System.Diagnostics;
using System.Reflection;

namespace LabNation.Common
{
    public enum LogLevel{
        ERROR = 0,
        WARN = 10,
        INFO = 20,
        DEBUG = 30,
    }
    public class LogMessage
    {
        public DateTime timestamp = DateTime.Now;
        public LogLevel level { get; private set; }
        public string message { get; private set; }
        public string end;
        public ConsoleColor? color = null;

        public LogMessage(LogLevel l, string msg, ConsoleColor? color = null) : this(l, msg, "", color) { }
		public LogMessage(LogLevel l, string msg, string end = "\n", ConsoleColor? color = null)
        {
            this.message = msg;
            this.level = l;
            this.end = end;
            this.color = color;
        }
    }
    public class Logger
    {
        public delegate void logUpdateCallback();
        static List<ConcurrentQueue<LogMessage>> logQueues = new List<ConcurrentQueue<LogMessage>>();

		/// <summary>
		/// Log the origin of the messages using reflection
		/// </summary>
		public static bool LogOrigin = false;

        public static void AddQueue(ConcurrentQueue<LogMessage> q)
        {
            logQueues.Add(q);
        }
        public static void LogC(LogLevel l, string msg, ConsoleColor? color = null)
        {
            Log(l, msg, "", color);
        }
        public static void Log(LogLevel l, string msg, string end = "\n", ConsoleColor? color = null )
        {
            foreach(string msg_part in msg.Split('\n'))
                foreach(var q in logQueues)
				    q.Enqueue(new LogMessage(l, msg_part, end, color));
        }
        public static void Info(string text) {  Log(LogLevel.INFO, text); }
		public static void Info(string format, params object[] args) { Info (String.Format (format, args)); }
        public static void Debug(string text) { Log(LogLevel.DEBUG, text); }
		public static void Debug(string format, params object[] args) { Debug (String.Format (format, args)); }
        public static void Warn(string text) { Log(LogLevel.WARN, text); }
		public static void Warn(string format, params object[] args) { Warn (String.Format (format, args)); }
        public static void Error(string text) { Log(LogLevel.ERROR, text); }
		public static void Error(string format, params object[] args) { Error (String.Format (format, args)); }
    }
}
