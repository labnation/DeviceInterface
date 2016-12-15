// WARNING
//
// This file has been generated automatically by Xamarin Studio to store outlets and
// actions made in the UI designer. If it is removed, they will be lost.
// Manual changes to this file may not be handled correctly.
//
using Foundation;
using System.CodeDom.Compiler;

namespace LabNation.SmartScopeServerUI
{
	[Register ("AppDelegate")]
	partial class AppDelegate
	{
		[Outlet]
		AppKit.NSMenuItem autostartitem { get; set; }

		[Action ("autostart:")]
		partial void autostart (Foundation.NSObject sender);

		[Action ("quit:")]
		partial void quit (Foundation.NSObject sender);
		
		void ReleaseDesignerOutlets ()
		{
			if (autostartitem != null) {
				autostartitem.Dispose ();
				autostartitem = null;
			}
		}
	}
}
