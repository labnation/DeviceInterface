using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using System.Text.RegularExpressions;

namespace DriverInstaller
{
    class Program
    {
        static int Main(string[] args)
        {
            int retCode = 0;
            bool running = true;
            string output, error;
            do
            {   
                //Install our new inf
                string dpinstExe;
                if (Environment.Is64BitOperatingSystem)
                    dpinstExe = "dpinst_64.exe";
                else
                    dpinstExe = "dpinst.exe";
                //string dpinstPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "pnputil.exe");
                string dpinstPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "driver", dpinstExe);
                string workPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "driver");
                string inf = Path.Combine(workPath, "SmartScope.inf");
                
                retCode = Common.Utils.RunProcess(dpinstPath, "/SW /C /SA /SH /F /LM", workPath, 0, out output, out error);
                //Silent mode - done here
                if (args.Contains("/S"))
                    return 0;

                byte status = (byte)(retCode >> 24);
                byte driversInstalledToDevice = (byte)retCode;
                byte driversInstalledToDriverStore = (byte)(retCode >> 8);
                if ((status & 0x80) != 0)
                {
                    var r = TopMostMessageBox.Show(String.Format("Failed to install driver (0x{0:X}). Try again?", retCode), "", MessageBoxButtons.YesNo, MessageBoxIcon.Error);
                    if (r == DialogResult.No)
                    {
                        TopMostMessageBox.Show("Find the driver in " + workPath, "", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        running = false;
                    }
                }
                else
                {
                    string msg = "";
                    if(driversInstalledToDevice == 1)
                        msg = "SmartScope driver installation went well";
                    else if(driversInstalledToDriverStore == 1)
                        msg = "SmartScope driver was installed, but no device was detected.";

                    if ((status & 0x40) != 0)
                        msg += "\n\nA restart is required to complete the installation";

                    if (msg == "")
                        msg = String.Format("It seems all went well, but I can't be sure. The exit code is (0x{0:X})", retCode);
                    TopMostMessageBox.Show(msg, "", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    running = false;
                }
            }
            while (running);
            return 0;
        }
    }
    static public class TopMostMessageBox
    {
        static public DialogResult Show(string message)
        {
            return Show(message, string.Empty, MessageBoxButtons.OK, MessageBoxIcon.None);
        }

        static public DialogResult Show(string message, string title)
        {
            return Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.None);
        }

        static public DialogResult Show(string message, string title,
            MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            // Create a host form that is a TopMost window which will be the 
            // parent of the MessageBox.
            Form topmostForm = new Form();
            // We do not want anyone to see this window so position it off the 
            // visible screen and make it as small as possible
            topmostForm.Size = new System.Drawing.Size(1, 1);
            topmostForm.StartPosition = FormStartPosition.Manual;
            System.Drawing.Rectangle rect = SystemInformation.VirtualScreen;
            topmostForm.Location = new System.Drawing.Point(rect.Bottom + 10,
                rect.Right + 10);
            topmostForm.Show();
            // Make this form the active form and make it TopMost
            topmostForm.Focus();
            topmostForm.BringToFront();
            topmostForm.TopMost = true;
            // Finally show the MessageBox with the form just created as its owner
            DialogResult result = MessageBox.Show(topmostForm, message, title,
                buttons, icon);
            topmostForm.Dispose(); // clean it up all the way

            return result;
        }
    }
}
