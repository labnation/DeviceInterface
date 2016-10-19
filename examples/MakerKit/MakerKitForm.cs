using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using LabNation.DeviceInterface.Devices;
using LabNation.DeviceInterface.Hardware;

namespace MakerKit
{
    public partial class MakerKitForm : Form
    {
        static DeviceManager hsManager;
        static HackerSpecial device;

        public MakerKitForm()
        {
            //load yaml file            
            List<RegisterBankDefinition> registerDefinitions = YamlHelper.ReadYaml("regDefinition.yaml");

            InitializeComponent(registerDefinitions);
			this.pictureBox1.Image = Image.FromStream(new MemoryStream(Resources.Load("labnation-logo.png")));
            statusLabel.Text = "Loaded registerbank information from file";

            //starts DeviceManaging, converting any incoming SmartScope interfacei into a HackerSpecial instance. See connectHandler on what's going on next.
            hsManager = new DeviceManager(null, connectHandler, new Dictionary<Type, Type>() { { typeof(ISmartScopeInterface), typeof(HackerSpecial) } });
            hsManager.Start();

		}

		protected override void OnFormClosing(System.Windows.Forms.FormClosingEventArgs e)
		{
			hsManager.Stop();
			base.OnFormClosing(e);
		}

        static void connectHandler(IDevice dev, bool connected)
        {
            //Only accept devices of the IScope type (i.e. not IWaveGenerator)
            //and block out the fallback device (dummy scope)
            if (connected && dev is HackerSpecial && !(dev is DummyScope))
            {
                statusLabel.Text = "SmartScope connected";

                device = (HackerSpecial)dev;

                //Get FW contents
                string fwName = "SmartScopeCustom.bin";

                //load FW from file
                byte[] firmware = null;
                try
                {
                    firmware = System.IO.File.ReadAllBytes(fwName);
                    statusLabel.Text = fwName + " file loaded";
                }
                catch
                {
                    statusLabel.Text = fwName + " file not found!";
                }

                //Flash FW to FPGA
                if (firmware != null)
                {
                    if (device.FlashFPGA(firmware))
                    {
                        statusLabel.Text = "FPGA configured successfully";
                    }
                    else
                    {
                        statusLabel.Text = "Firmware loaded, but configuring FPGA failed";
                    }
                }
            }
            else
            {
                device = null;
            }
        }

        //whenever the value in a textbox is updated, the new value is sent to the FPGA immediately
        void textbox_TextChanged(object sender, System.EventArgs e)
        {
            TextBox textbox = (TextBox)sender;
            int registerIndex = (int)textbox.Tag;
            
            uint entryValue;
			if (!uint.TryParse (textbox.Text, out entryValue))
				entryValue = 0;
			else if (entryValue < 0)
				entryValue = 0;
			else if (entryValue > 255)
				entryValue = 255;
            textbox.Text = entryValue.ToString();

            if (device != null)
                device.FpgaUserMemory[(uint)registerIndex].WriteImmediate((byte)entryValue);
        }
    }
}
