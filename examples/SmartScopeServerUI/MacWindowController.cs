
using System;
using System.Collections.Generic;
using System.Linq;
using Foundation;
using AppKit;

namespace LabNation.SmartScopeServerUI
{
	public partial class MacWindowController : AppKit.NSWindowController
	{
		#region Constructors

		// Called when created from unmanaged code
		public MacWindowController (IntPtr handle) : base (handle)
		{
			Initialize ();
		}
		
		// Called when created directly from a XIB file
		[Export ("initWithCoder:")]
		public MacWindowController (NSCoder coder) : base (coder)
		{
			Initialize ();
		}
		
		// Call to load from the XIB/NIB file
		public MacWindowController () : base ("MacWindow")
		{
			Initialize ();
		}
		
		// Shared initialization code
		void Initialize ()
		{
		}

		#endregion

		//strongly typed window accessor
		public new MacWindow Window {
			get {
				return (MacWindow)base.Window;
			}
		}
	}
}

