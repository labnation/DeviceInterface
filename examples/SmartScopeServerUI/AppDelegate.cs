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
            ConsoleLogger c = new ConsoleLogger(LogLevel.DEBUG);

            mainWindowController = new MacWindowController ();

			// This is where we setup our visual tree. These could be setup in MainWindow.xib, but
			// this example is showing programmatic creation.

			// We create a tab control to insert both examples into, and set it to take the entire window and resize
			CGRect frame = mainWindowController.Window.ContentView.Frame;

            ServerTable stv = new ServerTable(frame);
            NSScrollView scrollView = new NSScrollView(frame)
            {
                AutoresizingMask = NSViewResizingMask.HeightSizable | NSViewResizingMask.WidthSizable
            };
            scrollView.DocumentView = stv.Table;


            mainWindowController.Window.ContentView.AddSubview (scrollView);
			mainWindowController.Window.MakeKeyAndOrderFront (this);

            m = new Monitor(false, stv.ServerChanged);
		}

        public override void WillTerminate(Foundation.NSNotification notification)
        {
            FileLogger.StopAll();   
        }
	}
}

