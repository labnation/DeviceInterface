using System;
using LabNation.DeviceInterface.Devices;
using LabNation.Common;
using LabNation.DeviceInterface.DataSources;
using System.Threading;
using System.IO;
using System.Linq;
#if WINDOWS
using System.Windows.Forms;
#endif

namespace SmartScopeConsole
{
	class MainClass
	{
		/// <summary>
		/// The DeviceManager detects device connections
		/// </summary>
		static DeviceManager deviceManager;
		/// <summary>
		/// The scope used in here
		/// </summary>
		static IScope scope;
		static bool running = true;

        [STAThread]
		static void Main (string[] args)
		{
			//Open logger on console window
			FileLogger consoleLog = new FileLogger (new StreamWriter (Console.OpenStandardOutput ()), LogLevel.INFO);

			Logger.Info ("LabNation SmartScope Console Demo");
			Logger.Info ("---------------------------------");

			//Set up device manager with a device connection handler (see below)
			deviceManager = new DeviceManager (connectHandler);
			deviceManager.Start ();

			ConsoleKeyInfo cki = new ConsoleKeyInfo();
			while (running) {
#if WINDOWS
                Application.DoEvents();
#endif
				Thread.Sleep (100);

				if (Console.KeyAvailable) {
					cki = Console.ReadKey (true);
					HandleKey (cki);
				}
			}
            Logger.Info("Stopping device manager");
			deviceManager.Stop ();
            Logger.Info("Stopping Logger");
			consoleLog.Stop ();
		}

		static void connectHandler (IDevice dev, bool connected)
		{
			//Only accept devices of the IScope type (i.e. not IWaveGenerator)
			//and block out the fallback device (dummy scope)
			if (connected && dev is IScope && !(dev is DummyScope)) {
				Logger.Info ("Device connected of type " + dev.GetType ().Name + " with serial " + dev.Serial);
				scope = (IScope)dev;
				ConfigureScope ();
			} else {
				scope = null;
			}
		}

		static void HandleKey(ConsoleKeyInfo k)
		{
			switch (k.Key) {
				case ConsoleKey.Q:
				case ConsoleKey.X:
                case ConsoleKey.Escape:
					running = false;
					break;

			}
		}

		static void ConfigureScope ()
		{
			Logger.Info ("Configuring scope");

			//Stop the scope acquisition (commit setting to device)
			scope.Running = false;
			scope.CommitSettings ();

			//Set handler for new incoming data
			scope.DataSourceScope.OnNewDataAvailable += PrintVoltageBars;
			//Start datasource
			scope.DataSourceScope.Start ();

			//Configure acquisition

			/******************************/
			/* Horizontal / time settings */
			/******************************/
			//Disable logic analyser
			scope.ChannelSacrificedForLogicAnalyser = null;
			//Don't use rolling mode
			scope.Rolling = false;
			//Don't fetch overview buffer for faster transfers
			scope.SendOverviewBuffer = false;
			//Set sample depth to the minimum for a max datarate
			scope.AcquisitionLength = scope.AcquisitionLengthMin; 
			//trigger holdoff in seconds
			scope.TriggerHoldOff = 0; 
			//Acquisition mode to automatic so we get data even when there's no trigger
			scope.AcquisitionMode = AcquisitionMode.AUTO; 
			//Don't accept partial packages
			scope.PreferPartial = false;
			//Set viewport to match acquisition
			scope.SetViewPort (0, scope.AcquisitionLength);

			/*******************************/
			/* Vertical / voltage settings */
			/*******************************/
			foreach (AnalogChannel ch in AnalogChannel.List) {
				//FIRST set vertical range
				scope.SetVerticalRange (ch, -3, 3);
				//THEN set vertical offset (dicated by range)
				scope.SetYOffset (ch, 0);
				//use DC coupling
				scope.SetCoupling (ch, Coupling.DC);
				//and x10 probes
				scope.SetProbeDivision (ch, ProbeDivision.X10);
			}

			// Set trigger to channel A
			scope.TriggerValue = new TriggerValue () {
				channel = AnalogChannel.ChA,
				edge = TriggerEdge.RISING,
				level = 1.0f
			};

			//Update the scope with the current settings
			scope.CommitSettings ();

			//Show user what he did
			PrintScopeConfiguration ();

			//Set scope runnign;
			scope.Running = true;
			scope.CommitSettings ();
		}

		/// <summary>
		/// Print 2 bars representing the average voltage of each smartscope channel
		/// </summary>
		static void PrintVoltageBars (DataPackageScope p, DataSource s)
		{
			int consoleWidth = Console.BufferWidth;
			if(consoleWidth < 50)
				consoleWidth = 50;	
			int voltageBarWidth = consoleWidth / 2 - 12;
			string voltageBar = new string ('-', voltageBarWidth);

			//Print the acquisition identifier. For the smartscope, this
			//is an incrementing number from 0 to 255
			string line = String.Format ("{0,3:d}", p.Identifier);

			foreach (AnalogChannel ch in AnalogChannel.List) {
				//Fetch the data from the DataPackageScope
				//DataPackageScope is a rather big class containing
				//pretty much everything there is to know about
				//the acquired data.
                ChannelData d = p.GetData(ChannelDataSourceScope.Viewport, ch);
				//Average out the voltages
				float average = ((float[])d.array).Average ();

				//Scale it between 0 and 1 to project it onto the bar
				float averageRelative = (average - p.SaturationLowValue [ch]) / (p.SaturationHighValue [ch] - p.SaturationLowValue [ch]);

				//Pretty printing time for [ Ch A ][---15.0 mV----*-------]

				line += String.Format ("[ Ch {0,1} ]", ch.Name);
				line += "[";

				//Add the '*' mark to the voltage bar
				string b = voltageBar.Overwrite ((int)(averageRelative * voltageBar.Length), "*");

				//Pretty print the voltage into a SI formatted string like 341.2 mV
				string val = printVolt (average);

				//Compute where to place the voltage measurement so as not to overlap with
				//the '*' mark

				int valuePosition = voltageBarWidth * (averageRelative > 0.51f ? 1 : 5) / 6  - val.Length / 2;
				b = b.Overwrite (valuePosition, val);
				line += b;
				line += "]";
			}
			line += "\r";

			Console.Write (line);
		}

		static string printVolt (float v)
		{
			return Utils.siPrint (v, 0.013, 3, "V");
		}
			
		static void PrintScopeConfiguration ()
		{
			string c = "";
			string f = "{0,-20}: {1:s}\n";
			string fCh = "Channel {0:s} - {1,-15:s}: {2:s}\n";
			c += "---------------------------------------------\n";
			c += "-              SCOPE SETTINGS               -\n";
			c += "---------------------------------------------\n";
			c += String.Format (f, "Scope serial", scope.Serial);
			c += String.Format (f, "Acquisition depth", Utils.siPrint (scope.AcquisitionDepth, 1, 3, "Sa", 1024));
			c += String.Format (f, "Acquisition length", Utils.siPrint (scope.AcquisitionLength, 1e-9, 3, "s"));
			c += String.Format (f, "Sample rate", Utils.siPrint (1.0 / scope.SamplePeriod, 1, 3, "Hz"));
			c += String.Format (f, "Viewport offset", Utils.siPrint (scope.ViewPortOffset, 1e-9, 3, "s"));
			c += String.Format (f, "Viewport timespan", Utils.siPrint (scope.ViewPortTimeSpan, 1e-9, 3, "s"));	
			c += String.Format (f, "Trigger holdoff", Utils.siPrint (scope.TriggerHoldOff, 1e-9, 3, "s"));
			c += String.Format (f, "Acquisition mode", scope.AcquisitionMode.ToString ("G"));
			c += String.Format (f, "Rolling", scope.Rolling.YesNo ());
			c += String.Format (f, "Partial", scope.PreferPartial.YesNo ());
			c += String.Format (f, "Logic Analyser", scope.LogicAnalyserEnabled.YesNo ());


			foreach (AnalogChannel ch in AnalogChannel.List) {
				string chName = ch.Name;
				c += String.Format ("======= Channel {0:s} =======\n", chName);
				c += String.Format (fCh, chName, "Vertical offset", printVolt (scope.GetYOffset (ch)));
				c += String.Format (fCh, chName, "Coupling", scope.GetCoupling (ch).ToString ("G"));
				c += String.Format (fCh, chName, "Probe division", scope.GetProbeDivision (ch).ToString ());
			}
			c += "---------------------------------------------\n";
			Console.Write (c);				
		}
	}
}