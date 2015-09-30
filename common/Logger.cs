using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;

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
        public LogMessage(LogLevel l, string msg)
        {
            this.message = msg;
            this.level = l;
        }
    }
    public class Logger
    {
        public delegate void logUpdateCallback();
        static List<ConcurrentQueue<LogMessage>> logQueues = new List<ConcurrentQueue<LogMessage>>();
        static List<logUpdateCallback> logUpdateCallbacks = new List<logUpdateCallback>();
        public static void AddQueue(ConcurrentQueue<LogMessage> q, logUpdateCallback cb = null)
        {
            logQueues.Add(q);
            if(cb != null)
                logUpdateCallbacks.Add(cb);
        }
        private static void Log(LogLevel l, string msg)
        {
            foreach(var q in logQueues)
                q.Enqueue(new LogMessage(l, msg));
            foreach (var cb in logUpdateCallbacks)
                cb();
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
