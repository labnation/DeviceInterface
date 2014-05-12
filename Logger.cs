using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore
{    
    public struct LogEntry
    {
        public DateTime timestamp;
        public Type senderType;
        public LogLevel logLevel;
        public string message;  
    }

    public delegate void QueueChangedDelegate();

    public enum LogLevel { 
        Error   = 10, 
        Warning = 20, 
        Info    = 30, 
        Debug   = 40
    };

	#if IPHONE || ANDROID
	public static class Logger
	{
		public static List<string> Log = new List<string>();
		public static List<LogMessageType> MessageTypes = new List<LogMessageType>();

		static public void AddEntry(object sender, LogMessageType debugLevel, string message)
		{
			Log.Add (message);
			MessageTypes.Add (debugLevel);
		}
	}
#else
    //purpose: singleton keeping track of all logging information sent from anywhere in the project
    public static class Logger
    {
        static private Dictionary<Queue<LogEntry>, QueueChangedDelegate> logQueues = new Dictionary<Queue<LogEntry>, QueueChangedDelegate>();
        
        //allows any method anywhere in the project to send log information
        static public void AddEntry(object sender, LogLevel debugLevel, string message)
        {
            LogEntry newEntry = new LogEntry() {
                timestamp = DateTime.Now,
                senderType = sender != null ? sender.GetType() : typeof(object),
                logLevel = debugLevel,
                message = message
            };

            foreach (KeyValuePair<Queue<LogEntry>, QueueChangedDelegate> kvp in logQueues)
            {
                kvp.Key.Enqueue(newEntry);
                if (kvp.Value != null)
                    kvp.Value();
            }
        }
        static public void AddQueue(Queue<LogEntry> q, QueueChangedDelegate del)
        {
            logQueues.Add(q, del);
        }
        static public void AddQueue(Queue<LogEntry> q)
        {
            AddQueue(q, null);
        }
    }
#endif
}
