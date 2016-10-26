# console-demo
To get started, check out the repository. From a command line, run ```./bootstrap.h <platform>```, i.e.

```./bootstrap.h MacOS```

Then open the generated ```DeviceInterface.<Platform>.sln``` file with Xamarin/MonoDevelop/VisualStudio.

When running the app, the smartscope is configured to make 20us acquisitions in AUTO trigger mode (i.e. timeout when no trigger) with an input range of -3V to 3V on both channels. They are configured in DC coupling for a x10 probe and a 0V vertical offset.

## Console in linux
MonoDevelop doesn't seem to run the app in an external console by default which leads to poor console support. You can change that though in the **project options of SmartScopeConsole**
```
Run > General > Run on external console
```

# app output
The app shows the average voltage measured on each channel like below:

```
9/30/2015 6:08:26 PM  INFO  LabNation SmartScope Console Demo
9/30/2015 6:08:26 PM  INFO  ---------------------------------
9/30/2015 6:08:29 PM  INFO  Device connected of type SmartScope with serial 00513005A14
9/30/2015 6:08:29 PM  INFO  Configuring scope
---------------------------------------------
-              SCOPE SETTINGS               -
---------------------------------------------
Scope serial        : 00513005A14
Acquisition depth   : 2.00 kSa
Acquisition length  : 20.5 µs
Sample rate         : 100 MHz
Viewport offset     : 0.00 s
Viewport timespan   : 20.5 µs
Trigger holdoff     : 0.00 s
Acquisition mode    : AUTO
Rolling             : No
Partial             : No
Logic Analyser      : No
======= Channel A =======
Channel A - Vertical range : [-3.37 V:3.59 V]
Channel A - Vertical offset: -13.0 mV
Channel A - Coupling       : DC
Channel A - Probe division : X10
======= Channel B =======
Channel B - Vertical range : [-3.37 V:3.59 V]
Channel B - Vertical offset: -26.0 mV
Channel B - Coupling       : DC
Channel B - Probe division : X10
---------------------------------------------
112[ Ch A ][-----------------------*-------------26.0 mV----][ Ch B ][-----1.36 V----------------------*--------------]
```

# Understanding what's happening

## The Log
First off, we use the ```LabNation.Common.Logger``` to easily output information to the console and see where things go wrong in ```LabNation.DeviceInterface``` which also uses this Logger. We choose not to show debug messages
```c#
FileLogger consoleLog = new FileLogger (new StreamWriter (Console.OpenStandardOutput ()), LogLevel.INFO);
```

Then we Log a little to the Log
```c#
Logger.Info ("LabNation SmartScope Console Demo");
Logger.Info ("---------------------------------");
```

## The DeviceManager
First, an instance of ```DeviceManager``` is created by passing a device connection callback to it. Then this ```DeviceManager``` is started. This basically sets up the device detection thread, ensuring your callback is called when a device shows up or disappears.

```c#
deviceManager = new DeviceManager (connectHandler);
deviceManager.Start ();
```

The connecthandler has the following prototype
```c#
public delegate void DeviceConnectHandler(IDevice device, bool connected);
```

Note that the DeviceManager is built so that even when no SmartScope is connected, it falls back to a ```fallbackDevice``` which is the Dummy Scope. This means you'll always have an ```IScope``` device to work with, if desired. In this app though, we chose to ignore the ```fallbackDevice``` and have our ```IScope``` field ```NULL``` when no actual device is connected.

The body of the connect handler can look like this
```c#
if (connected && dev is IScope && dev != deviceManager.fallbackDevice) {
	Logger.Info ("Device connected of type " + dev.GetType ().Name + " with serial " + dev.Serial);
	scope = (IScope)dev;
	ConfigureScope ();
} else {
	scope = null;
}
```

## Connecting a device
When a device is connected, the connectHandler is first called for the disconnection of the ```fallbackDevice``` and then for the connection of the new device, i.e. with ```connected``` true in the latter case. Our callback immediately continues to configuring the scope, taking the following steps:

* Stop a possibly running acquisition. This puts the controller inside the SmartScope to rest.

```c#
scope.Running = false;
scope.CommitSettings ();
```

* Register for incoming data: the IScope interface allows to synchronously poll for data using ```IScope.GetScopeData()```, but you can also register a callback on ```IScope.DataSourceScope```. Once this callback is registered, the ```IScope.DataSourceScope.Start()``` is called which starts a thread that'll poll for data and call your callback when new data is available.

```c#
scope.DataSourceScope.OnNewDataAvailable += PrintVoltageBars;
scope.DataSourceScope.Start ();
```

* Configure the ```IScope``` itself using a bunch of properties and setters methods (where properties were not possible, i.e. for channel specific settings).

```c#
scope.LogicAnalyserEnabled = false;
scope.Rolling = false;
scope.SendOverviewBuffer = false;
scope.AcquisitionLength = scope.AcquisitionLengthMin; 
scope.TriggerHoldOff = 0; 
scope.AcquisitionMode = AcquisitionMode.AUTO; 
scope.PreferPartial = false;
scope.SetViewPort (0, scope.AcquisitionLength);

foreach (AnalogChannel ch in AnalogChannel.List) {
	scope.SetVerticalRange (ch, -3, 3);
	scope.SetYOffset (ch, 0);
	scope.SetCoupling (ch, Coupling.DC);
	scope.SetProbeDivision (ch, ProbeDivision.X10);
}

scope.TriggerAnalog = new AnalogTriggerValue () {
	channel = AnalogChannel.ChA,
	direction = TriggerDirection.RISING,
	level = 1.0f
};
```

* **Synchronise the new settings** above to the SmartScope using ```IScope.CommitSettings```. This ensures that the next acquisition is either with the settings entered before the previous CommitSettings call, or with the new settings. *DON'T FORGET THIS STEP!*

```c#
scope.CommitSettings ();
```
* Print the scope configuration for your pleasure using ```PrintScopeConfiguration()``` and a bunch of ```String.Format()``` calls
* Set the *acquisition running*

```c#
scope.Running = true;
scope.CommitSettings ();
```

## OnNewDataAvailable - A dual voltmeter
This is where we do stuff with the measure data. It's all quite simple
* The ```PrintVoltageBars``` method we registered with ```IScope.DataSourceScope``` looks as follows

```c#
static void PrintVoltageBars (DataPackageScope p, EventArgs e)
{
	//Do something nice with the DataPackageScope
}
```
* From the data package coming from the scope, we get the viewport data for each channel and compute the average for each channel. Then we display it using crazy console print.
```c#
foreach (AnalogChannel ch in AnalogChannel.List) {
	ChannelData d = p.GetData (DataSourceType.Viewport, ch);
	float average = ((float[])d.array).Average ();
	//Crazy console print code below
	//...
}
```

## Viewport, Acquisition Buffer, What?!
Because we have this massive 4 megasample memory and a mere USB 1.0 controller in the SmartScope, and because we don't want to load your host system with handling 2 times 4 megasamples at 100Hz or so, and also because you probably don't care about all those 4 million dots all the time, we separated things:
* **The Acquisition**: this is defined by the number of samples you would like to be able to search once you hit the stop button. It's minimally 2048 samples and goes up to 4 megasamples. Though currently we have a software limit at 512kSa which you can lift in [SmartScope.cs:80](/labnation/DeviceInterface/blob/master/Devices/SmartScope.cs#L80)
* **The Viewport**: this is a window within the acquisition which is effectively streamed to the host. The acquisition is subsampled so that the viewport spans at most 2048 samples. The viewport is defined by a time offset and time span.

### The stop button
When acquiring, you can set ```IScope.running``` to false (and call ```CommitSettings()```). Once this is done, one more trigger condition is expected and the acquisition will stop, resulting in free time on the USB to transfer the entire acquisition buffer to the host. You can monitor the progress of this transfer using the ```DataPackageScope.FullAcquisitionFetchProgress``` field, which equals ```1f``` after completion. Once this is the case, you can call ```DataPackageScope.GetData(DataSourceType.Acquisition, AnalogChannel.ChA)``` to use Channel A's acquisition buffer. Note that the ```ChannelData.Partial``` flag for ChannelData of type ```DataSourceType.Acquisition``` is always true due to array copy efficiency considerations, so **do not use it instead of ```DataPackageScope.FullAcquisitionFetchProgress```**. The ```Partial``` flag has its function on viewport data for slow acquisitions such as when rolling, allowing you to decide whether to process/display the data or not in your app.
