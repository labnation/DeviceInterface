using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using Mono.Zeroconf;
using System.Threading.Tasks;
using LabNation.DeviceInterface.Net;
using LabNation.Common;

namespace LabNation.DeviceInterface.Hardware
{
    //class that provides raw HW access to the device
    internal class InterfaceManagerZeroConf : InterfaceManager<InterfaceManagerZeroConf, SmartScopeInterfaceEthernet>
    {
#if MONOMAC || WINDOWS
		//Instance to ensure proper building
		Mono.Zeroconf.Providers.Bonjour.ZeroconfProvider p;
#elif LINUX
		Mono.Zeroconf.Providers.AvahiDBus.ZeroconfProvider p;
#endif

        object pollLock = new object();
        bool pollThreadRunning;
        int polling = 0;
        Thread pollThread;
        const int POLL_INTERVAL = 5000;
        List<ServiceLocation> detectedServices = new List<ServiceLocation>();

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

        protected override void Initialize()
        {
            startPollThread();
        }

        private void startPollThread()
        {
            pollThread = new Thread(new ThreadStart(pollThreadStart));
            pollThread.Name = "ZeroConf poll thread";
            pollThreadRunning = true;
            pollThread.Start();
        }

        private void pollThreadStart()
        {
            PollDevice();
        }

        List<String> serviceNames = new List<string>();
        object resolveLock = new object();

        public override void PollDevice()
        {
            //browser needs to be renewed each time, as it's being disposed after Browse
            ServiceBrowser browser = new ServiceBrowser();
            browser.ServiceAdded += delegate(object o, ServiceBrowseEventArgs args)
            {
                Console.WriteLine("Found Service: {0}", args.Service.Name);
                args.Service.Resolved += delegate(object o2, ServiceResolvedEventArgs args2)
                {
                    lock (resolveLock)
                    {
                        polling++;
                        IResolvableService s = (IResolvableService)args2.Service;

                        ServiceLocation loc = new ServiceLocation(s.HostEntry.AddressList[0], s.Port, s.FullName);
                        Logger.Info("A new ethernet interface was found");
                        SmartScopeInterfaceEthernet ethif = new SmartScopeInterfaceEthernet(loc.ip, loc.port, OnInterfaceDisconnect);
                        if (ethif.Connected)
                        {
                            createdInterfaces.Add(loc, ethif);
                            if (onConnect != null)
                                onConnect(ethif, true);
                        }
                        else
                        {
                            Logger.Info("... but could not connect to ethernet interface");
                        }
                        polling--;
                    }
                };
                args.Service.Resolve();
            };

            //go for it!
            Common.Logger.Info("Polling ZeroConf");
            browser.Browse(Net.Net.SERVICE_TYPE, Net.Net.REPLY_DOMAIN);
        }

        public void Destroy()
        {
            foreach (var hw in createdInterfaces)
                hw.Value.Destroy();

            pollThreadRunning = false;
            pollThread.Join(POLL_INTERVAL);
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
