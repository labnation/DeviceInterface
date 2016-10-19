using System;
using System.Collections.Generic;

using Android.App;
using Android.Content.PM;
using Android.OS;

using LabNation.DeviceInterface;
using LabNation.DeviceInterface.Devices;
using LabNation.DeviceInterface.Hardware;

namespace MakerKit
{
	[Activity (
		Label = "MakerKitApp.Droid", 
		Theme="@android:style/Theme.Holo.Light", 
		Icon = "@drawable/icon", 
		MainLauncher = true, 
		ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation
	)]
	public class MainActivity : global::Xamarin.Forms.Platform.Android.FormsApplicationActivity
	{
		static DeviceManager hsManager;
		private App mainApp;

		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);

			global::Xamarin.Forms.Forms.Init (this, bundle);

			//load yaml file
			string regDefinitionPath = System.IO.Path.Combine(Android.OS.Environment.ExternalStorageDirectory.AbsolutePath, "LabNation");
			regDefinitionPath = System.IO.Path.Combine (regDefinitionPath, "DevApp");
			System.IO.Directory.CreateDirectory (regDefinitionPath);
			List<RegisterBankDefinition> registerDefinitions = MakerKit.YamlHelper.ReadYaml(System.IO.Path.Combine(regDefinitionPath, "regDefinition.yaml"));

			//start deviceManager, converting any incoming SmartScope into HackerSpecial instance
			hsManager = new DeviceManager(Application.Context, null, connectHandler, new Dictionary<Type, Type>() { { typeof(ISmartScopeInterface), typeof(HackerSpecial) } });
			hsManager.Start();

			mainApp = new App(registerDefinitions.ToArray ());
			LoadApplication (mainApp);

			mainApp.SetStatus("Loaded registerbank information from file");
		}

		private void connectHandler(IDevice dev, bool connected)
		{
			//Only accept devices of the IScope type (i.e. not IWaveGenerator)
			//and block out the fallback device (dummy scope)
			if (connected && dev is HackerSpecial && !(dev is DummyScope))
			{
				mainApp.SetStatus("SmartScope connected");

				mainApp.device = (HackerSpecial)dev;

				//Get FW contents
				string fwName = "SmartScopeCustom.bin";
				string regDefinitionPath = System.IO.Path.Combine(Android.OS.Environment.ExternalStorageDirectory.AbsolutePath, "LabNation");
				regDefinitionPath = System.IO.Path.Combine (regDefinitionPath, "DevApp");
				string fullFileName = System.IO.Path.Combine(regDefinitionPath, fwName);

				//load FW from file
				byte[] firmware = null;
				try
				{
					firmware = System.IO.File.ReadAllBytes(fullFileName);
					mainApp.SetStatus(fullFileName + " loaded");
				}
				catch
				{
					var files = System.IO.Directory.GetFiles (regDefinitionPath);
					mainApp.SetStatus(fullFileName + " not found!");
				}                    

				//Flash FW to FPGA
				if (firmware != null) {
					if (mainApp.device.FlashFPGA (firmware)) {
						mainApp.SetStatus("FPGA configured successfully");
					} else {
						mainApp.SetStatus("Firmware loaded, but configuring FPGA failed");
					}
				}
			}
			else
			{
				mainApp.device = null;
			}
		}
	}
}

