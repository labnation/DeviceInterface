using System;
using System.Collections.Generic;
using System.Reflection;
using Xamarin.Forms;
using LabNation.DeviceInterface;
using LabNation.DeviceInterface.Devices;
using LabNation.DeviceInterface.Hardware;

namespace MakerKit
{
	public class App : Application
	{
		public HackerSpecial device;
		private List<Entry> entries = new List<Entry>();		//contains 1 Entry per register. listIndex == regAddress.
		private List<Slider> sliders = new List<Slider>();		//contains 1 Slider per register. listIndex == regAddress.
		private Label statusLabel;
		private RegisterBankDefinition bankDef;

		public App (RegisterBankDefinition[] allBanks)
		{			
			this.bankDef = allBanks[0];
				
			//image
			Image embeddedImage = new Image { 
				Aspect = Aspect.AspectFit,
				HeightRequest = 50,
				HorizontalOptions = LayoutOptions.Fill,
				Source = ImageSource.FromResource("MakerKit.Resources.labnation-logo.png")
			};

			//title
			var label = new Label { Text="SmartScope Maker Kit", FontSize = 24 };
			var labelStacker = new StackLayout {
				HorizontalOptions = LayoutOptions.Center,
				Orientation = StackOrientation.Horizontal,
				Padding = new Thickness(0,0,0,30),
				Children = { label }
			};

			//bottom status bar
			statusLabel = new Label { Text="Status", FontSize = 12 };
			var statusLabelStacker = new StackLayout {
				HorizontalOptions = LayoutOptions.Center,
				VerticalOptions = LayoutOptions.End,
				Orientation = StackOrientation.Horizontal,
				Padding = new Thickness(0,0,0,30),
				Children = { statusLabel }
			};

			ContentPage textboxPage = new ContentPage {
				Content = new ScrollView {
					Content = new StackLayout
					{						
						Spacing = 10,
						Padding = 10,
						VerticalOptions = LayoutOptions.Start,
						Orientation = StackOrientation.Vertical,
						HorizontalOptions = LayoutOptions.FillAndExpand,
						Children = { embeddedImage, labelStacker, DefineEntryGrid(allBanks[0]), statusLabelStacker}
					}
				}
			}; 
			textboxPage.Title = "Values";

			ContentPage sliderPage = new ContentPage {
				Content = new ScrollView {
					Content = new StackLayout
					{

					Spacing = 10,
					Padding = 10,
					VerticalOptions = LayoutOptions.Start,
					Orientation = StackOrientation.Vertical,
					HorizontalOptions = LayoutOptions.FillAndExpand,
						Children = { embeddedImage, labelStacker, DefineSliderGrid(allBanks[0]), statusLabelStacker}
					}
				}
			}; 
			sliderPage.Title = "Sliders";

			MainPage = new TabbedPage {
				Children = { textboxPage, sliderPage }
			};
		}

		public void SetStatus(string status)
		{
			this.statusLabel.Text = status;
		}

		private Grid DefineSliderGrid(RegisterBankDefinition bankDef)
		{
			Grid grid = new Grid
			{
				HorizontalOptions = LayoutOptions.FillAndExpand,
				VerticalOptions = LayoutOptions.FillAndExpand,
				RowSpacing = 35,
				ColumnDefinitions = 
				{
					new ColumnDefinition { Width = new GridLength(40, GridUnitType.Absolute) },
					new ColumnDefinition { Width = GridLength.Auto },
					new ColumnDefinition { Width = new GridLength(100, GridUnitType.Star) }
				}
				};

			RowDefinitionCollection rdc = new RowDefinitionCollection ();
			foreach (var item in bankDef.Registers)
				rdc.Add (new RowDefinition { Height = GridLength.Auto });

			for (int i = 0; i < bankDef.Registers.Length; i++) {
				grid.Children.Add(new Label
					{
						Text = i.ToString("000"),
						FontSize = Device.GetNamedSize (NamedSize.Small, typeof(Label)),
						HorizontalOptions = LayoutOptions.Start,
						VerticalOptions = LayoutOptions.Center
					}, 0, i);

				grid.Children.Add(new Label
					{
						Text = bankDef.Registers[i],
						FontSize = Device.GetNamedSize (NamedSize.Large, typeof(Label)),
						HorizontalOptions = LayoutOptions.Start,
						VerticalOptions = LayoutOptions.Center
					}, 1, i);

				Slider slider = new Slider (0,255,0);
				slider.HorizontalOptions = LayoutOptions.FillAndExpand;
				slider.VerticalOptions = LayoutOptions.Center;
				slider.ValueChanged += OnRegisterValueChanged;
				sliders.Add (slider);
				grid.Children.Add(slider, 2, i);
			}

			return grid;
		}

		private Grid DefineEntryGrid(RegisterBankDefinition bankDef)
		{
			Grid grid = new Grid
			{
				HorizontalOptions = LayoutOptions.FillAndExpand,
				VerticalOptions = LayoutOptions.FillAndExpand,
				RowSpacing = 35,
				ColumnDefinitions = 
				{
					new ColumnDefinition { Width = new GridLength(40, GridUnitType.Absolute) },
					new ColumnDefinition { Width = GridLength.Auto },
					new ColumnDefinition { Width = new GridLength(100, GridUnitType.Star) }
				}
				};

			RowDefinitionCollection rdc = new RowDefinitionCollection ();
			foreach (var item in bankDef.Registers)
				rdc.Add (new RowDefinition { Height = GridLength.Auto });

			for (int i = 0; i < bankDef.Registers.Length; i++) {
				grid.Children.Add(new Label
					{
						Text = i.ToString("000"),
						FontSize = Device.GetNamedSize (NamedSize.Small, typeof(Label)),
						HorizontalOptions = LayoutOptions.Start,
						VerticalOptions = LayoutOptions.Center
					}, 0, i);
				
				grid.Children.Add(new Label
					{
						Text = bankDef.Registers[i],
						FontSize = Device.GetNamedSize (NamedSize.Large, typeof(Label)),
						HorizontalOptions = LayoutOptions.Start,
						VerticalOptions = LayoutOptions.Center
					}, 1, i);

				Entry entry = new Entry {
					HorizontalOptions = LayoutOptions.FillAndExpand,
					VerticalOptions = LayoutOptions.Center,
					Text = "0",
					Keyboard = Keyboard.Numeric
				};
				entry.Completed += OnRegisterValueChanged;
				entries.Add (entry);
				grid.Children.Add(entry, 2, i);
			}

			return grid;
		}

		private void OnRegisterValueChanged(object sender, EventArgs e)
		{
			int index = -1;
			int newValue = -1;

			if (sender is Entry) {				
				Entry entry = (Entry)sender;
				index = entries.IndexOf (entry);
				if (!int.TryParse (entry.Text, out newValue))
					newValue = 0;
				else if (newValue < 0)
					newValue = 0;
				else if (newValue > 255)
					newValue = 255;				
				sliders [index].Value = newValue;
			} else if (sender is Slider) {
				Slider slider = (Slider)sender;
				newValue = (int)slider.Value;
				index = sliders.IndexOf (slider);
				entries[index].Text = newValue.ToString ();
			}

			uint uregNr = (uint)index;
			byte byteVal = (byte)newValue;
			if (device != null)
				device.FpgaUserMemory[uregNr].WriteImmediate(byteVal);

			statusLabel.Text = "Set register [" + index.ToString () + "] " + bankDef.Registers [index] + " to " + newValue.ToString ();
		}

		private bool fetchTreadRunning = false;
		private void FetchThread()
		{
			fetchTreadRunning = true;
			while (fetchTreadRunning) {
				for (int i = 0; i < bankDef.Registers.Length; i++) {					
					if (device != null) {
						double origValue = sliders[i].Value;
						int fpgaValue = (int)device.FpgaUserMemory [(byte)i].Read ().GetByte();

						//if user hasn't updated in the meantime
						if (sliders [i].Value == origValue) {
							//if value was updated by FPGA
							if (fpgaValue != (int)origValue) {
								entries[i].Text = fpgaValue.ToString ();
								sliders [i].Value = fpgaValue;
								statusLabel.Text = "Register [" + i.ToString () + "] " + bankDef.Registers [i] + " was updated by FPGA to " + fpgaValue.ToString ();
							}
						}
					}

					System.Threading.Thread.Sleep (20);
				}
			}
		}

		protected override void OnStart ()
		{
			// Handle when your app starts
		}

		protected override void OnSleep ()
		{
			// Handle when your app sleeps
		}

		protected override void OnResume ()
		{
			// Handle when your app resumes
		}
	}
}

