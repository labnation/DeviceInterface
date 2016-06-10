using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using Zeroconf;
using System.Threading.Tasks;

namespace LabNation.DeviceInterface.Hardware
{
    //class that provides raw HW access to the device
    internal class InterfaceManagerZeroConf : InterfaceManager<InterfaceManagerZeroConf>
    {
        object pollLock = new object();
        bool pollThreadRunning;
        Thread pollThread;
        const int POLL_INTERVAL=5000;
        List<IPAddress> detectedServerAddresses = new List<IPAddress>();
        Dictionary<IPAddress, SmartScopeUsbInterfaceEthernet> createdInterfaces = new Dictionary<IPAddress, SmartScopeUsbInterfaceEthernet>();

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
                ZeroconfResolver.ResolveAsync("SmartScopeServer._sss._tcp.local.");
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
            
            //sleep for some time, allowing servers to be detected
            Thread.Sleep(POLL_INTERVAL);

            hostsTask.Wait();
            IReadOnlyList<IZeroconfHost> hostList = hostsTask.Result;
            detectedServerAddresses = hostList.Select(x => IPAddress.Parse(x.IPAddress)).ToList();

            //handle disconnects
            Dictionary<IPAddress, SmartScopeUsbInterfaceEthernet> disappearedInterfaces = createdInterfaces.Where(x => !detectedServerAddresses.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value);
            foreach (var r in disappearedInterfaces)
            {
                if (onConnect != null)
                    onConnect(r.Value, false);
                createdInterfaces.Remove(r.Key);
            }

            //handle connects
            List<IPAddress> newInterfaces = detectedServerAddresses.Where(x => !createdInterfaces.ContainsKey(x)).ToList();
            foreach (var n in newInterfaces)
            {
                createdInterfaces.Add(n, new SmartScopeUsbInterfaceEthernet(n, 25482));
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
