using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Net;
using LabNation.DeviceInterface.Net;
using Android.Net.Nsd;
using Android.Content;
using Android.App;
using LabNation.Common;

namespace LabNation.DeviceInterface.Hardware
{

	class DiscoveryListener : Java.Lang.Object, NsdManager.IDiscoveryListener
	{
		NsdManager mNsdManager;

		public DiscoveryListener(NsdManager nsdManager)
		{
			mNsdManager = nsdManager;
		}

		public void OnDiscoveryStarted(string serviceType)
		{
			Logger.Info("Service discovery started");
		}

		public void OnDiscoveryStopped(string serviceType)
		{
			Logger.Info("Discovery stopped: " + serviceType);
		}

		public void OnServiceFound(Android.Net.Nsd.NsdServiceInfo service)
		{
			Logger.Info("Service discovery success" + service);
			mNsdManager.ResolveService(service, new ResolveListener());
		}

		public void OnServiceLost(Android.Net.Nsd.NsdServiceInfo service)
		{
		}

		public void OnStartDiscoveryFailed(string serviceType, Android.Net.Nsd.NsdFailure errorCode)
		{
			Logger.Error("Discovery failed: Error code:" + errorCode);
			mNsdManager.StopServiceDiscovery(this);
		}

		public void OnStopDiscoveryFailed(string serviceType, Android.Net.Nsd.NsdFailure errorCode)
		{
			Logger.Error("Discovery failed: Error code:" + errorCode);
			mNsdManager.StopServiceDiscovery(this);
		}
	}

	class ResolveListener : Java.Lang.Object, NsdManager.IResolveListener
	{
		string mServiceName;
		public void OnServiceResolved(NsdServiceInfo info)
		{
			mServiceName = info.ServiceName;
			Logger.Debug("Service registered: " + mServiceName);
			InterfaceManagerServiceDiscovery.Instance.ServiceResolved(info);
		}

		public void OnResolveFailed(NsdServiceInfo arg0, NsdFailure fail)
		{
			Logger.Info("Service registration failed: " + fail);
		}
	}

    //class that provides raw HW access to the device
    internal class InterfaceManagerServiceDiscovery : InterfaceManager<InterfaceManagerServiceDiscovery, SmartScopeInterfaceEthernet>
    {
		public static Context context;

		Dictionary<NsdServiceInfo, SmartScopeInterfaceEthernet> createdInterfaces = new Dictionary<NsdServiceInfo, SmartScopeInterfaceEthernet>();

		DiscoveryListener discoveryListener;
		NsdManager nsdManager;

        protected override void Initialize()
        {
			nsdManager = (NsdManager)context.GetSystemService(Context.NsdService);
			discoveryListener = new DiscoveryListener(nsdManager);
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
			nsdManager.StopServiceDiscovery(discoveryListener);
		}
		public void Resume()
		{
			nsdManager.DiscoverServices(Net.Net.SERVICE_TYPE, NsdProtocol.DnsSd, discoveryListener);
		}

		public void ServiceResolved(NsdServiceInfo info)
		{
			if (createdInterfaces.Keys.Contains(info))
			{
				Logger.Info("Skipping registration of service at {0}:{1} since already registerd", info.Host, info.Port);
				return;
			}
			Logger.Info("A new ethernet interface was found at {0}:{1}", info.Host, info.Port);
			SmartScopeInterfaceEthernet ethif = new SmartScopeInterfaceEthernet(
				new System.Net.IPAddress(info.Host.GetAddress()), info.Port, OnInterfaceDisconnect);
			if (ethif.Connected)
			{
				createdInterfaces.Add(info, ethif);
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
