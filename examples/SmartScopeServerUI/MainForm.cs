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
using System.Threading;
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

        Dictionary<InterfaceServer, int> tableRows = new Dictionary<InterfaceServer, int>();
        ConcurrentQueue<LogMessage> logqueue = new ConcurrentQueue<LogMessage>();
        Thread logThread;
        LabNation.DeviceInterface.Net.Monitor interfaceMonitor;
        bool running = true;
        
        private void Form_Load(object sender, EventArgs e)
        {
            Logger.AddQueue(logqueue);
            logThread = new Thread(DequeueLog);
            logThread.Name = "Logbox";
            logThread.Start();
            this.FormClosing += Cleanup;
            Logger.Debug("App started");
            interfaceMonitor = new LabNation.DeviceInterface.Net.Monitor(false, OnServerChanged);
            tableLayoutPanel1.Controls.Add(new Label() { Text = "Serial" }, 0, 0);
            tableLayoutPanel1.Controls.Add(new Label() { Text = "Port" }, 1, 0);
            tableLayoutPanel1.Controls.Add(new Label() { Text = "Connected" }, 2, 0);
            tableLayoutPanel1.Controls.Add(new Label() { Text = "Btns" }, 3, 0);

            ToolStripMenuItem clearLog = new ToolStripMenuItem()
            {
                Text = "Clear log",
            };
            clearLog.Click += delegate { BeginInvoke((MethodInvoker)delegate { logbox.Clear(); }); };
            menu.Items.Add(clearLog);
        }

        private void Cleanup(object sender, FormClosingEventArgs e)
        {
            running = false;
            logThread.Join(1000);
            if (logThread.IsAlive)
                logThread.Abort();
            Logger.Info("Stopping interface monitor");
            interfaceMonitor.Stop();

            Logger.Info("Stopping console logger");
            
        }

        private void OnServerChanged(InterfaceServer s, bool connected)
        {
            if(connected)
            {
                BeginInvoke((MethodInvoker)delegate { AddServerToTable(s); });
            } else
            {
                BeginInvoke((MethodInvoker)delegate { RemoveServerFromTable(s); });
            }

        }

        private void AddServerToTable(InterfaceServer s)
        {
            if(tableRows.ContainsKey(s))
            {
                //Update
                int row = tableRows[s];
                Label labelPort = (Label)tableLayoutPanel1.GetControlFromPosition(1, row);
                Button startButton = (Button)tableLayoutPanel1.GetControlFromPosition(3, row);
                startButton.Text = "Stop";
                startButton.Click += StopServer;
                startButton.Enabled = true;
                labelPort.Text = s.Port.ToString();
            }
            else
            {
                Label labelSerial = new Label() { Text = s.hwInterface.Serial, Tag = s };
                Label labelPort = new Label() { Text = s.Port.ToString(), Tag = s };
                Button startButton = new Button()
                {
                    Text = "start",
                    Tag = s,
                    Dock = DockStyle.Fill,

                };
                startButton.Click += StartServer;

                tableLayoutPanel1.RowCount += 1;
                tableLayoutPanel1.RowStyles.Add(new RowStyle());
                int row = tableLayoutPanel1.RowCount - 2;
                tableLayoutPanel1.Controls.Add(labelSerial, 0, row);
                tableLayoutPanel1.Controls.Add(labelPort, 1, row);
                tableLayoutPanel1.Controls.Add(startButton, 3, row);
                tableRows[s] = row;
            }

        }

        private void StartServer(object s, EventArgs e) {
            Button b = (Button)s;
            b.Click -= StartServer;
            b.Enabled = false;
            ((InterfaceServer)b.Tag).Start();
        }
        private void StopServer(object s, EventArgs e) { ((InterfaceServer)((Control)s).Tag).Stop(); }
        private void RemoveServerFromTable(InterfaceServer s)
        {
            //Find row where this server lives
            int row = tableRows[s];
            tableRows.Remove(s);

            // delete all controls of row that we want to delete
            for (int i = 0; i < tableLayoutPanel1.ColumnCount; i++)
            {
                var control = tableLayoutPanel1.GetControlFromPosition(i, row);
                tableLayoutPanel1.Controls.Remove(control);
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

            // remove last row
            tableLayoutPanel1.RowStyles.RemoveAt(tableLayoutPanel1.RowCount - 1);
            tableLayoutPanel1.RowCount--;

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

        private void DequeueLog()
        {
            while(running)
            {
                Thread.Sleep(50);
                LogMessage m;
                while (logqueue.TryDequeue(out m))
                    AddLogMessage(m);
            }
        }
    }
}
