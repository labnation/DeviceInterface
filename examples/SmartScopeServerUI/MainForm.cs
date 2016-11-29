using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using LabNation.Common;
using LabNation.DeviceInterface.Net;
using System.Collections.Concurrent;
using LabNation.DeviceInterface.Hardware;
using LabNation.DeviceInterface.Devices;

namespace LabNation.SmartScopeServerUI
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        class ServerInfo
        {
            public int row;
            public int bytesTx;
            public int bytesRx;
        }

        Dictionary<InterfaceServer, ServerInfo> tableRows = new Dictionary<InterfaceServer, ServerInfo>();
        ConcurrentQueue<LogMessage> logqueue = new ConcurrentQueue<LogMessage>();
        Monitor interfaceMonitor;
        Timer logTimer;
        Timer bwTimer;
        
        private void Form_Load(object sender, EventArgs e)
        {
            this.FormClosing += Cleanup;
            Logger.Debug("App started");
            interfaceMonitor = new LabNation.DeviceInterface.Net.Monitor(false, OnServerChanged);
            tableLayoutPanel1.Controls.Add(new Label() { Text = "Serial" }, COL_SERIAL, 0);
            tableLayoutPanel1.Controls.Add(new Label() { Text = "Port" }, COL_PORT, 0);
            tableLayoutPanel1.Controls.Add(new Label() { Text = "Up" }, COL_BW_UP, 0);
            tableLayoutPanel1.Controls.Add(new Label() { Text = "Down" }, COL_BW_DN, 0);
            tableLayoutPanel1.Controls.Add(new Label() { Text = "Btns" }, COL_BUTT, 0);

            ToolStripMenuItem clearLog = new ToolStripMenuItem()
            {
                Text = "Clear log",
            };
            clearLog.Click += delegate { BeginInvoke((MethodInvoker)delegate { logbox.Clear(); }); };
            menu.Items.Add(clearLog);

            bwTimer = new Timer()
            {
                Enabled = true,
                Interval = 500,
            };
            bwTimer.Tick += BwTimer_Tick;

            Logger.AddQueue(logqueue);
            logTimer = new Timer()
            {
                Enabled = true,
                Interval = 100,
            };

            logTimer.Tick += LogTimer_Tick;

        }

        private void Cleanup(object sender, FormClosingEventArgs e)
        {

            bwTimer.Enabled = false;
            logTimer.Enabled = false;
            Logger.Info("Stopping interface monitor");
            interfaceMonitor.Stop();

            Logger.Info("Stopping console logger");
            
        }

        private void OnServerChanged(InterfaceServer s, bool connected)
        {
            if(connected)
            {
                BeginInvoke((MethodInvoker)delegate { UpdateServerTable(s, true); });
            } else
            {
                BeginInvoke((MethodInvoker)delegate { UpdateServerTable(s, false); });
            }

        }

        private object serverTableLock = new object();
        private const int COL_SERIAL = 0;
        private const int COL_PORT = 1;
        private const int COL_BW_UP = 2;
        private const int COL_BW_DN = 3;
        private const int COL_BUTT = 4;
        private const string STR_START = "start";
        private const string STR_STOP = "stop";

        private void UpdateServerTable(InterfaceServer s, bool present)
        {
            tableLayoutPanel1.SuspendLayout();

            lock (serverTableLock)
            {
                if (present)
                {
                    if (tableRows.ContainsKey(s))
                    {
                        //Update
                        int row = tableRows[s].row;
                        Label labelPort = (Label)tableLayoutPanel1.GetControlFromPosition(COL_PORT, row);
                        Button startButton = (Button)tableLayoutPanel1.GetControlFromPosition(COL_BUTT, row);
                        startButton.Text = s.State == ServerState.Started ? STR_STOP : STR_START;
                        startButton.Enabled = true;
                        labelPort.Text = s.Port.ToString();
                    }
                    else
                    {
                        Label labelSerial = new Label() { Text = s.hwInterface.Serial, Tag = s };
                        Label labelPort = new Label() { Text = s.Port.ToString(), Tag = s };
                        Button startButton = new Button()
                        {
                            Text = s.State == ServerState.Started ? STR_STOP : STR_START,
                            Tag = s,
                            Dock = DockStyle.Fill,

                        };
                        startButton.Click += StartStopServer;

                        tableLayoutPanel1.RowCount += 1;
                        tableLayoutPanel1.RowStyles.Add(new RowStyle());
                        //Use one but last row, last row is used for emptyness
                        int row = tableLayoutPanel1.RowCount - 2;
                        tableLayoutPanel1.Controls.Add(labelSerial, COL_SERIAL, row);
                        tableLayoutPanel1.Controls.Add(labelPort, COL_PORT, row);
                        tableLayoutPanel1.Controls.Add(new Label() { Text = "up" }, COL_BW_UP, row);
                        tableLayoutPanel1.Controls.Add(new Label() { Text = "dn" }, COL_BW_DN, row);
                        tableLayoutPanel1.Controls.Add(startButton, COL_BUTT, row);
                        tableRows.Add(s, new ServerInfo() { row = row });
                    }
                }
                else
                {
                    Logger.Debug("Removing Server for " + s.hwInterface.Serial);
                    //Find row where this server lives
                    int row = tableRows[s].row;
                    tableRows.Remove(s);

                    // delete all controls of row that we want to delete
                    for (int i = 0; i < tableLayoutPanel1.ColumnCount; i++)
                    {
                        var control = tableLayoutPanel1.GetControlFromPosition(i, row);
                        if(control != null)
                        {
                            tableLayoutPanel1.Controls.Remove(control);
                            Logger.Debug("Removing control @ col " + i + " " + control.GetType().ToString());
                            control.Dispose();
                        }
                            
                    }

                    // move up row controls that comes after row we want to remove
                    for (int i = row + 1; i < tableLayoutPanel1.RowCount; i++)
                    {
                        for (int j = 0; j < tableLayoutPanel1.ColumnCount; j++)
                        {
                            var control = tableLayoutPanel1.GetControlFromPosition(j, i);
                            if (control != null)
                            {
                                tableLayoutPanel1.SetRow(control, i - 1);
                            }
                        }
                    }

                    tableRows.Where(x => x.Value.row > row).ToList().ForEach(x => tableRows[x.Key].row = x.Value.row - 1);
                    tableLayoutPanel1.RowStyles.RemoveAt(row);
                    tableLayoutPanel1.RowCount--;
                }
            }
            tableLayoutPanel1.ResumeLayout();
            tableLayoutPanel1.PerformLayout();
        }


        DateTime lastBwUpdate = DateTime.Now;
        private void BwTimer_Tick(object sender, EventArgs e)
        {
            double timePassed = (DateTime.Now - lastBwUpdate).TotalMilliseconds / 1000;
            string format = "{0:0.00} kBps";
            lock (serverTableLock)
            {
                foreach(var kvp in tableRows)
                {
                    InterfaceServer s = kvp.Key;
                    int row = kvp.Value.row;
                    Label lup = (Label)tableLayoutPanel1.GetControlFromPosition(COL_BW_UP, row);
                    Label ldown = (Label)tableLayoutPanel1.GetControlFromPosition(COL_BW_DN, row);
                    if (lup == null || ldown == null)
                        continue;
                    double bwup = 0;
                    double bwdn = 0;
                    if (s.State == ServerState.Started)
                    {
                        bwdn = (s.BytesRx - tableRows[s].bytesRx) / timePassed / 1024;
                        bwup = (s.BytesTx - tableRows[s].bytesTx) / timePassed / 1024;
                    }
                    ldown.Text = String.Format(format, bwdn );
                    lup.Text = String.Format(format, bwup);

                    tableRows[s].bytesRx = s.BytesRx;
                    tableRows[s].bytesTx = s.BytesTx;
                }
            }
            lastBwUpdate = DateTime.Now;
        }

        private void StartStopServer(object s, EventArgs e) {
            Button b = (Button)s;
            b.Enabled = false;
            InterfaceServer server = (InterfaceServer)b.Tag;
            switch(server.State)
            {
                case ServerState.Started:
                    server.Stop();
                    break;
                case ServerState.Stopped:
                    server.Start();
                    break;
                case ServerState.Destroyed:
                    Logger.Warn("Received start/stop request on destroyed server - expect badness");
                    break;
            }
        }

        private void AddLogMessage(LogMessage m)
        {
            if (InvokeRequired) {
                this.Invoke((MethodInvoker)delegate { AddLogMessage(m); });
                return;
            }
            ConsoleColor c = m.color.HasValue ? m.color.Value : ConsoleColor.Green;
            string msg = String.Format("[{0}] {1} {2}", m.level.ToString().ToUpper(), m.message, Environment.NewLine);

            var box = logbox;
            box.SelectionStart = box.TextLength;
            box.SelectionLength = 0;

            box.SelectionColor = Color.FromName(c.ToString());
            box.AppendText(msg);
            box.SelectionColor = box.ForeColor;
            box.ScrollToCaret();
        }

        DateTime bwLastCheck = DateTime.MaxValue;
        private void LogTimer_Tick(object sender, EventArgs e)
        {
            LogMessage m;
            while (logqueue.TryDequeue(out m))
                AddLogMessage(m);
        }
    }
}
