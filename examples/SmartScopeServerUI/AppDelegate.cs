using System;
using CoreGraphics;
using Foundation;
using AppKit;
using ObjCRuntime;
using System.Collections.Generic;
using LabNation.DeviceInterface.Net;
using LabNation.Common;

namespace LabNation.SmartScopeServerUI
{
	public partial class AppDelegate : NSApplicationDelegate
	{
		MacWindowController mainWindowController;

		public override bool ApplicationShouldTerminateAfterLastWindowClosed (NSApplication sender)
		{
			return true;
		}

        Monitor m;

		public override void DidFinishLaunching (NSNotification notification)
		{
            new ConsoleLogger(LogLevel.DEBUG);

            mainWindowController = new MacWindowController ();

			// This is where we setup our visual tree. These could be setup in MainWindow.xib, but
			// this example is showing programmatic creation.

			// We create a tab control to insert both examples into, and set it to take the entire window and resize
			CGRect frame = mainWindowController.Window.ContentView.Frame;

            ServerTable stv = new ServerTable(frame);
            NSScrollView scrollView = new NSScrollView(frame)
            {
                AutoresizingMask = NSViewResizingMask.HeightSizable | NSViewResizingMask.WidthSizable,
                HasHorizontalScroller = true,
                HasVerticalScroller = true,
            };
            scrollView.DocumentView = stv.Table;


            mainWindowController.Window.ContentView.AddSubview (scrollView);
			mainWindowController.Window.MakeKeyAndOrderFront (this);

            autostartitem.State = NSCellStateValue.On;
            m = new Monitor(autostartitem.State == NSCellStateValue.On, stv.ServerChanged);
		}

        partial void autostart(Foundation.NSObject sender)
        {

            NSMenuItem startitem = (NSMenuItem)sender;
            startitem.State = startitem.State == NSCellStateValue.On ? NSCellStateValue.Off : NSCellStateValue.On;
            m.Autostart = autostartitem.State == NSCellStateValue.On;
        }

        partial void quit(Foundation.NSObject sender)
        {
            mainWindowController.Close();
        }
        public override void WillTerminate(Foundation.NSNotification notification)
        {
            FileLogger.StopAll();   
        }
	}
}

