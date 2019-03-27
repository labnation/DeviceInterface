using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Collections.Concurrent;

namespace LabNation.Common
{

#if !ANDROID
    public class ConsoleLogger : FileLogger
    {
        //NOTE: the streamwrite is in fact not used by the parent class,
        // It writes immediately to the console (to use color)
        public ConsoleLogger(LogLevel level)
            : base(new StreamWriter(Console.OpenStandardOutput()), level)
        {
            this.useConsoleColor = true;
        }
    }
#endif
    public class FileLogger
    {
        static List<FileLogger> loggers = new List<FileLogger>();
        StreamWriter writer;
        Thread dumpThread;
        ConcurrentQueue<LogMessage> logQueue;
        bool running;
        LogLevel logLevel;
#if !ANDROID
        protected bool useConsoleColor = false;
        ConsoleColor oldColor = Console.ForegroundColor;
#endif
        public FileLogger(StreamWriter writer, LogLevel level)
        {
            this.logLevel = level;
            this.writer = writer;
            logQueue = new ConcurrentQueue<LogMessage>();
            Logger.AddQueue(logQueue);

            dumpThread = new Thread(dumpThreadStart) { Name = "log dump thread" };
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
            LogMessage previousEntry = new LogMessage(LogLevel.DEBUG, null, "\n");
            while (running || logQueue.Count > 0)
            {
                while (logQueue.Count > 0)
                {
                    LogMessage entry;
                    if (logQueue.TryDequeue(out entry))
                    {
                        if (entry.level > logLevel) continue;
                        string message = "";
                        //Don't print timestamp/origin if last message wasn't ended with newline
                        if (previousEntry.end == "\n")
                            message += entry.timestamp.ToString().PadRight(22) + entry.level.ToString().PadRight(6);
                        message += entry.message + entry.end;

                        previousEntry = entry;
#if !IOS && !ANDROID
                        if (useConsoleColor && entry.color.HasValue)
                        {
                            oldColor = Console.ForegroundColor;
                            Console.ForegroundColor = entry.color.Value;
                            Console.Write(message);
                            Console.ForegroundColor = oldColor;
                        }
                        else
#endif
                            writer.Write(message);
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
