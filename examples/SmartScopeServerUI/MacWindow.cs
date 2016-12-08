
using System;
using System.Collections.Generic;
using System.Linq;
using Foundation;
using AppKit;

namespace LabNation.SmartScopeServerUI
{
	public partial class MacWindow : AppKit.NSWindow
	{
		#region Constructors

		// Called when created from unmanaged code
		public MacWindow (IntPtr handle) : base (handle)
		{
			Initialize ();
		}
		
		// Called when created directly from a XIB file
		[Export ("initWithCoder:")]
		public MacWindow (NSCoder coder) : base (coder)
		{
			Initialize ();
		}
		
		// Shared initialization code
		void Initialize ()
		{
		}

		#endregion
	}
}

