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
        const int POLL_INTERVAL=5000;
        List<ServiceLocation> detectedServices = new List<ServiceLocation>();

        class ServiceLocation {
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

        private void pollThreadStart ()
		{
			while (pollThreadRunning) {
                PollDevice(); //PollDevice contains the Thread.Sleep         
            }
        }

        private async Task<IReadOnlyList<IZeroconfHost>> FindZeroConf()
        {
            IReadOnlyList<IZeroconfHost> results = await
                ZeroconfResolver.ResolveAsync("_sss._tcp.local.");
            return results;
        }

        public async Task EnumerateAllServicesFromAllHosts()
        {
            ILookup<string, string> domains = await ZeroconfResolver.BrowseDomainsAsync();
            var responses = await ZeroconfResolver.ResolveAsync(domains.Select(g => g.Key));
            foreach (var resp in responses)
                Console.WriteLine(resp);
        }

        public override void PollDevice()
        {
            Common.Logger.Warn("Polling ZeroConf");

            Task<IReadOnlyList<IZeroconfHost>> hostsTask = FindZeroConf();

            hostsTask.Wait();
            IReadOnlyList<IZeroconfHost> hostList = hostsTask.Result;
            detectedServices = new List<ServiceLocation>();
            foreach (IZeroconfHost h in hostList)
            {
                IPAddress ip = IPAddress.Parse(h.IPAddress);
                foreach (IService s in h.Services.Values)
                {
                    detectedServices.Add(new ServiceLocation(ip, s.Port, s.Name));
                }
            }

            //handle disconnects
            Dictionary<ServiceLocation, SmartScopeInterfaceEthernet> disappearedInterfaces = 
                createdInterfaces.Where(x => !detectedServices.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value);

            foreach (var r in disappearedInterfaces)
            {
                if (onConnect != null)
                    onConnect(r.Value, false);
                createdInterfaces.Remove(r.Key);
            }

            //handle connects
            List<ServiceLocation> newInterfaces = detectedServices.Where(x => !createdInterfaces.ContainsKey(x)).ToList();
            foreach (var n in newInterfaces)
            {
                createdInterfaces.Add(n, new SmartScopeInterfaceEthernet(n.ip, n.port));
                if (onConnect != null)
                    onConnect(createdInterfaces[n], true);
            }       
        }

        public void Destroy()
        {
            pollThreadRunning = false;
            pollThread.Join(1000);
        }
    }
}
