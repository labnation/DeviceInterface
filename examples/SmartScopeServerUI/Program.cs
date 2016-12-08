using System;
#if MONOMAC
using CoreGraphics;
using Foundation;
using AppKit;
using ObjCRuntime;
#else
using System.Windows.Forms;
#endif

namespace LabNation.SmartScopeServerUI
{
    static class Program
    {
        #if MONOMAC
        static void Main (string[] args)
        {
          NSApplication.Init ();
          NSApplication.Main (args);
        }
        #else
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
        #endif
    }
}