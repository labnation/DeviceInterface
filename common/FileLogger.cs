using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Collections.Concurrent;

namespace LabNation.Common
{
    public class FileLogger
    {
        static List<FileLogger> loggers = new List<FileLogger>();
        StreamWriter writer;
        Thread dumpThread;
        ConcurrentQueue<LogMessage> logQueue;
        bool running;
        LogLevel logLevel;
		private static string originInsert = "\n" + new string (' ', 28);

        public FileLogger(StreamWriter writer, LogLevel level)
        {
            this.logLevel = level;
            this.writer = writer;
            logQueue = new ConcurrentQueue<LogMessage>();
            Logger.AddQueue(logQueue);

            dumpThread = new Thread(dumpThreadStart);
            running = true;
            dumpThread.Start();
            loggers.Add(this);
        }



        public FileLogger(string filename, LogLevel level) : this(
            new StreamWriter(new FileStream(Path.Combine(LabNation.Common.Utils.StoragePath, filename), FileMode.Append)),
            level
            )
        {
	        Logger.Info("Started file logger in " + ((FileStream)this.writer.BaseStream).Name);
        }

        public static void StopAll()
        {
            foreach (FileLogger l in loggers)
                l.Stop();
        }

        public void Stop()
        {
            this.running = false;
            dumpThread.Join();
        }

        private void dumpThreadStart()
        {
            while (running || logQueue.Count > 0)
            {
                while (logQueue.Count > 0)
                {
                    LogMessage entry;
                    if (logQueue.TryDequeue(out entry))
                    {
                        if (entry.level > logLevel) continue;							
						string message = entry.timestamp.ToString().PadRight(22) + entry.level.ToString().PadRight(6) + (entry.origin == null ? "" : entry.origin.PadRight(20) + originInsert) + entry.message;
                        writer.WriteLine(message);
                    }
                }
                writer.Flush();
                Thread.Sleep(50);
            }
            writer.Flush();
            writer.Close();
        }
    }
}
