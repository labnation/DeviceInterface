using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Net;
using LabNation.DeviceInterface.Net;
using LabNation.Common;
using Foundation;
using CoreFoundation;
using System.Runtime.InteropServices;

namespace LabNation.DeviceInterface.Hardware
{

    //class that provides raw HW access to the device
    internal class InterfaceManagerIOS : InterfaceManager<InterfaceManagerIOS, SmartScopeInterfaceEthernet>
    {
		const int AF_INET = 2;
		const int AF_INET6 = 30;

		private List<NSNetService> servicesFound = new List<NSNetService>();
		// The original C struct is in `/usr/include/sys/socket.h`
		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		struct sockaddr
		{
			public byte sa_len;
			public byte sa_family;
		}

		// The original C struct is in `/usr/include/netinet/in.h`
		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		struct sockaddr_in
		{
			public byte sin_len;
			public byte sin_family;
			public ushort sin_port;
			public uint sin_addr;
		}

		class ServiceLocation
		{
			public IPAddress ip;
			public int port;
			public string name;
			public ServiceLocation(IPAddress ip, int port, string name)
			{
				this.ip = ip;
				this.port = port;
				this.name = name;
			}
			public override bool Equals(object s)
			{
				if (!(s is ServiceLocation))
					return false;
				ServiceLocation sl = (ServiceLocation)s;
				return
					this.port == sl.port &&
					this.name == sl.name;
			}
		}


		Dictionary<ServiceLocation, SmartScopeInterfaceEthernet> createdInterfaces = new Dictionary<ServiceLocation, SmartScopeInterfaceEthernet>();
		NSNetServiceBrowser browser;

        protected override void Initialize()
        {
			browser = new NSNetServiceBrowser();
			Resume();
        }

		public override void PollDevice()
		{
			Logger.Debug("Polling not supported on Android service discovery");
		}

        public void Destroy()
        {
            foreach (var hw in createdInterfaces)
                hw.Value.Destroy();
        }

		public void Pause()
		{
			browser.FoundService -= ServiceResolved;
			browser.Stop();
		}
		public void Resume()
		{
			browser.FoundService += ServiceResolved;
			browser.SearchForServices(Net.Net.SERVICE_TYPE, Net.Net.REPLY_DOMAIN);
		}

		public void ServiceResolved(object sender, NSNetServiceEventArgs info)
		{
			servicesFound.Add(info.Service);
			info.Service.AddressResolved += AdressResolved;
			info.Service.ResolveFailure += AddressResolveFailure;
			info.Service.Resolve(5.0);
		}

		public void AddressResolveFailure(object sender, NSNetServiceErrorEventArgs e)
		{
			servicesFound.Remove((NSNetService)sender);
			Logger.Error("Failed to resolve : {0} - {1}", sender, e);
		}

		public void AdressResolved(object sender, EventArgs info)
		{
			NSNetService ns = (NSNetService)sender;
			servicesFound.Remove(ns);

			ServiceLocation sl = new ServiceLocation(IPAddress.None, 0, "");
			foreach (var addr in ns.Addresses)
			{
				sockaddr socket_address = (sockaddr)Marshal.PtrToStructure(addr.Bytes, typeof(sockaddr));
				if (socket_address.sa_family != AF_INET)
				{
					Logger.Info("Ignoring service since it's socket type is not AF_INET but {0:d}", socket_address.sa_family);
					continue;
				}
				sockaddr_in IP4 = (sockaddr_in)Marshal.PtrToStructure(addr.Bytes, typeof(sockaddr_in));

				IPAddress address = new IPAddress(IP4.sin_addr);
				sl = new ServiceLocation(address, (int)ns.Port, ns.Name);
				Logger.Debug("Got IP " + address.ToString());
				if (createdInterfaces.Keys.Contains(sl))
				{
					Logger.Info("Skipping registration of service at {0}:{1} since already registerd", address, ns.Port);
					return;
				}
			}

			Logger.Info("A new ethernet interface was found at {0}:{1}", sl.ip, sl.port);
			SmartScopeInterfaceEthernet ethif = new SmartScopeInterfaceEthernet(
				sl.ip, sl.port, OnInterfaceDisconnect);
			if (ethif.Connected)
			{
				createdInterfaces.Add(sl, ethif);
				if (onConnect != null)
					onConnect(ethif, true);
			}
			else
			{
				LabNation.Common.Logger.Info("... but could not connect to ethernet interface");
			}
		}

        private void OnInterfaceDisconnect(SmartScopeInterfaceEthernet hardwareInterface)
        {
            //remove from list
            if (createdInterfaces.ContainsValue(hardwareInterface))
                createdInterfaces.Remove(createdInterfaces.Single(x => x.Value == hardwareInterface).Key);

            //propage upwards (to DeviceManager)
            onConnect(hardwareInterface, false);

            //send DISCONNECT command to server
            hardwareInterface.Destroy();
        }
    }
}
