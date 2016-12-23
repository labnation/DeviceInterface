using System;
using System.Collections.Generic;
using System.Linq;
using AppKit;
using CoreGraphics;
using Foundation;
using LabNation.Common;
using LabNation.DeviceInterface.Net;

namespace LabNation.SmartScopeServerUI
{
    class ServerTable : NSObject
    {
        enum COLS { 
            Serial, 
            Port, 
            BwUp, 
            BwDn, 
            Buttons 
        };
        static Dictionary<COLS, string> coldefs = new Dictionary<COLS, string>
        {
            { COLS.Serial, "Serial"},
            { COLS.Port, "Port"},
            { COLS.BwUp, "Up"},
            { COLS.BwDn, "Down"},
            { COLS.Buttons, ""},

        };

        static string bwFormat = "{0:0.00} kBps";
        private const string STR_START = "start";
        private const string STR_STOP = "stop";

        public NSTableView Table { get; private set; }

        public ServerTable(CGRect frame)
        {
            Table = new NSTableView()
            {
                Frame = frame
            };

            foreach (var kvp in coldefs)
            {
                NSTableColumn c = new NSTableColumn(kvp.Value);
                c.HeaderCell.StringValue = kvp.Value;
                Table.AddColumn(c);
            }

            Table.DataSource = new TableDataSource(this);
            Table.Delegate = new TableDelegate(this);

            item = NSStatusBar.SystemStatusBar.CreateStatusItem(NSStatusItemLength.Square);
            item.Title = "S";
            item.Menu = new NSMenu();
            serverTitle = new NSMenuItem("Servers:");
            serverTitle.Enabled = false;
            item.Menu.AddItem(serverTitle);

            UpdateBandwidth(null);
        }

        NSStatusItem item;
        NSMenuItem serverTitle;

        DateTime lastBwUpdate = DateTime.Now;
        void UpdateBandwidth(NSTimer timer)
        {
            double timePassed = (DateTime.Now - lastBwUpdate).TotalMilliseconds / 1000;
            foreach (ServerInfo info in tableRows.Values)
            {
                InterfaceServer s = info.server;
                if (s.State == ServerState.Started)
                {
                    info.bwRx = (s.BytesRx - info.bytesRx) / timePassed / 1024;
                    info.bwTx = (s.BytesTx - info.bytesTx) / timePassed / 1024;
                }
                else {
                    info.bwTx = 0;
                    info.bwRx = 0;
                }

                info.bytesRx = s.BytesRx;
                info.bytesTx = s.BytesTx;

                UpdateMenuItem(info);
            }
            lastBwUpdate = DateTime.Now;
            NSTimer.CreateScheduledTimer(TimeSpan.FromMilliseconds(1000), UpdateBandwidth);
            Table.ReloadData();
        }


        class ServerInfo
        {
            public InterfaceServer server;
            public NSMenuItem menuItem;
            public int bytesTx;
            public int bytesRx;
            public double bwTx;
            public double bwRx;
        }

        Dictionary<int, ServerInfo> tableRows = new Dictionary<int, ServerInfo>();

        private void UpdateMenuItem(ServerInfo info)
        {
            info.menuItem.Title = String.Format("{0} : Up {1} kBps - Down {2} kBps",
                                                info.server.hwInterface.Serial,
                                                info.bwTx, info.bwRx);
            info.menuItem.State = info.server.State == ServerState.Started ? NSCellStateValue.On : NSCellStateValue.Off;
        }

        public void ServerChanged(InterfaceServer s, bool present)
        {
            InvokeOnMainThread(() =>
            {
                if (present)
                {
                    if (tableRows.Where(x => x.Value.server == s).Count() == 0)
                    {
                        ServerInfo info = new ServerInfo() { server = s, menuItem = new NSMenuItem() };
                        UpdateMenuItem(info);
                        item.Menu.AddItem(info.menuItem);
                        tableRows.Add(tableRows.Count, info);
                    }
                }
                else 
                {
                    if (tableRows.Where(x => x.Value.server == s).Count() != 0)
                    {
                        var info = tableRows.Single(x => x.Value.server == s);
                        item.Menu.RemoveItem(info.Value.menuItem);
                        tableRows.Remove(info.Key);

                        List<ServerInfo> ordered = tableRows.OrderBy(x => x.Key).Select(x => x.Value).ToList();
                        for (int i = 0; i < tableRows.Count; i++)
                        {
                            if (ordered.Count > i)
                                tableRows[i] = ordered[i];
                            else
                                tableRows.Remove(i);
                        }
                    }
                }
                Table.ReloadData();
            });
        }

        void ReorderTableRows()
        {
            
        }

        class TableDelegate : NSTableViewDelegate
        {
            ServerTable stv;
            public TableDelegate(ServerTable stv)
            {
                this.stv = stv;
            }

            public override NSView GetViewForItem(NSTableView tableView, NSTableColumn tableColumn, nint row)
            {
                string identifier = row.ToString() + "_" + tableColumn.Identifier;
                ServerInfo info = null;
                stv.tableRows.TryGetValue((int)row, out info);

                if (tableColumn.Identifier == coldefs[COLS.Buttons])
                {
                    //button
                    NSButton button = (NSButton)tableView.MakeView(identifier, this);
                    if (button == null)
                    {
                        button = new NSButton();
                        button.Identifier = identifier;
                        button.Activated += stv.StartStopServer;
                        button.BezelStyle = NSBezelStyle.Inline;
                    }
                    button.Title = info.server.State == ServerState.Started ? STR_STOP : STR_START;
                    button.Enabled = true;

                    return button;
                }
                else 
                {
                    NSTextField textfield = (NSTextField)tableView.MakeView(identifier, this);
                    if (textfield == null)
                    {
                        textfield = new NSTextField();
                        textfield.Identifier = identifier;
                        textfield.Bordered = false;
                        textfield.Selectable = false;
                        textfield.Editable = false;
                    }
                    if (info != null)
                    {
                        if (tableColumn.Identifier == coldefs[COLS.Serial])
                            textfield.StringValue = info.server.hwInterface.Serial;
                        else if (tableColumn.Identifier == coldefs[COLS.BwUp])
                            textfield.StringValue = String.Format(bwFormat, info.bwTx);
                        else if (tableColumn.Identifier == coldefs[COLS.BwDn])
                            textfield.StringValue = String.Format(bwFormat, info.bwRx);
                        else if (tableColumn.Identifier == coldefs[COLS.Port])
                            textfield.StringValue = info.server.Port.ToString();
                    }
                    return textfield;
                }
            }

            public override bool ShouldSelectRow(NSTableView tableView, nint row)
            {
                return false;
            }
        }

        private void StartStopServer(object s, EventArgs e)
        {
            NSButton b = (NSButton)s;
            b.Enabled = false;
            int row = (int)Table.RowForView(b);
            InterfaceServer server = tableRows[row].server;
            switch (server.State)
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
                default:
                    Logger.Debug("Not doing anything to server, it is busy {0:G}", server.State);
                    break;
            }
        }

        class TableDataSource : NSTableViewDataSource
        {
            ServerTable stv;
            public TableDataSource(ServerTable stv)
            {
                this.stv = stv;
            }
            public override nint GetRowCount(NSTableView tableView)
            {
                return stv.tableRows.Count;
            }
        }
    }
}
