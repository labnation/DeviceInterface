using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using Zeroconf;
using System.Threading.Tasks;
using LabNation.DeviceInterface.Net;

namespace LabNation.DeviceInterface.Hardware
{
    //class that provides raw HW access to the device
    internal class InterfaceManagerZeroConf : InterfaceManager<InterfaceManagerZeroConf, SmartScopeInterfaceEthernet>
    {
        object pollLock = new object();
        bool pollThreadRunning;
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
                    this.ip.Equals(sl.ip) &&
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
            while (pollThreadRunning)
            {
                PollDevice(); //PollDevice contains the Thread.Sleep         
            }
        }

        private async Task<IReadOnlyList<IZeroconfHost>> FindZeroConf()
        {
            IReadOnlyList<IZeroconfHost> results = await
                ZeroconfResolver.ResolveAsync("_sss._tcp.local.");
            return results;
        }

        async Task<List<ServiceLocation>> EnumerateAllServicesFromAllHosts()
        {
            ILookup<string, string> domains = await ZeroconfResolver.BrowseDomainsAsync();
            var responses = await ZeroconfResolver.ResolveAsync(domains.Select(g => g.Key));
            List<ServiceLocation> l = new List<ServiceLocation>();
            foreach (var resp in responses)
                foreach (IService s in resp.Services.Values)
                    l.Add(new ServiceLocation(IPAddress.Parse(resp.IPAddress), s.Port, s.Name));
            return l;
        }

        Func<ServiceLocation, bool> nameFilter = new Func<ServiceLocation, bool>(x => x.name == Constants.SERVICE_TYPE + "." + Constants.REPLY_DOMAIN);
        public override void PollDevice()
        {
            Common.Logger.Info("Polling ZeroConf");

            Task<List<ServiceLocation>> hostsTask = EnumerateAllServicesFromAllHosts();

            hostsTask.Wait();
            List<ServiceLocation> detectedServices = hostsTask.Result.Where(nameFilter).ToList();

            foreach (var kvp in createdInterfaces)
            {
                if (!kvp.Value.connected)
                {
                    LabNation.Common.Logger.Info("An ethernet interface was removed");
                    onConnect(kvp.Value, false);
                    createdInterfaces.Remove(kvp.Key);
                }
            }

            //handle connects
            List<ServiceLocation> newInterfaces = detectedServices.Where(x => !createdInterfaces.ContainsKey(x)).ToList();
            foreach (ServiceLocation loc in detectedServices)
            {
                if (createdInterfaces.Where(x => x.Key.ip.Equals(loc.ip) && x.Key.port == loc.port).Count() == 0)
                {
                    // A new interface
                    LabNation.Common.Logger.Info("A new ethernet interface was found");
                    SmartScopeInterfaceEthernet ethif = new SmartScopeInterfaceEthernet(loc.ip, loc.port, OnInterfaceDisconnect);
                    createdInterfaces.Add(loc, ethif);
                    if (onConnect != null)
                        onConnect(ethif, true);
                }
            }
        }

        public void Destroy()
        {
            pollThreadRunning = false;
            pollThread.Join(1000);
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
