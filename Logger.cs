using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
#if IPHONE || ANDROID
#else
using System.Windows.Forms;
#endif

namespace ECore
{    
    public struct LogEntry
    {
        public DateTime timestamp;
        public Type senderType;
        public LogMessageType debugLevel;
        public string message;  
    }

    public enum LogMessageType { GUIError, GUIInfo, ECoreError, ECoreWarning, CommandToDevice, ReplyFromDevice, ECoreInfo, Persistent};

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
        static private List<LogEntry> logEntries = new List<LogEntry>();
        static private ListBox listBox;

        //allows any method anywhere in the project to send log information
        static public void AddEntry(object sender, LogMessageType debugLevel, string message)
        {
            LogEntry newEntry = new LogEntry();
            newEntry.timestamp = DateTime.Now;
            newEntry.senderType = sender.GetType();
            newEntry.debugLevel = debugLevel;
            newEntry.message = message;

            logEntries.Add(newEntry);

            //if a linkbox is attached: add line and scroll down
            if (listBox != null)
            {
                //check whether or not we're on the same thread as the listbox -- otherwise its methods need to be invoked
                if (listBox.InvokeRequired)
                {
                    listBox.Invoke((MethodInvoker)delegate { AddEntryToDisplay(newEntry); });
                }
                else
                {
                    AddEntryToDisplay(newEntry);
                }
            }
        }

        static public void AddEntryToDisplay(LogEntry newEntry)
        {
            lock(listBox)
            {
            listBox.Items.Add(newEntry.timestamp.ToString().PadRight(22) + newEntry.debugLevel.ToString().PadRight(15) + newEntry.message);
            listBox.SelectedIndex = listBox.Items.Count - 1;
            listBox.SetSelected(listBox.Items.Count - 1, false);
            }
        }

        static public void AttachListbox(ListBox listbox)
        {
            listBox = listbox;
        }
    }
#endif
}
