using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;

namespace LabNation.DeviceInterface.Net
{
    internal class BandwidthMonitor
    {
        int tempBytes = 0;
        DateTime lastUpdate = DateTime.Now;
        TimeSpan updateInterval;
        LinkedList<KeyValuePair<DateTime, int>> linkedList = new LinkedList<KeyValuePair<DateTime, int>>();
        TimeSpan oneSecond = new TimeSpan(0, 0, 0, 1, 0);
        string lastCalc = "0";

        internal BandwidthMonitor(TimeSpan updateInterval)
        {
            this.updateInterval = updateInterval;
        }

        internal bool Update(int bytes, out string value)
        {
            tempBytes += bytes;

            DateTime now = DateTime.Now;
            if (now.Subtract(lastUpdate) > updateInterval)
            {
                linkedList.AddLast(new KeyValuePair<DateTime, int>(now, tempBytes));

                //drop elements older than 1sec
                LinkedListNode<KeyValuePair<DateTime, int>> item = linkedList.First;
                while (item != null && now.Subtract(item.Value.Key) > oneSecond)
                {
                    item = item.Next;
                    linkedList.RemoveFirst();
                }
                //if (bytesPerInterval.Count > 10)
                //{
                //TimeSpan t = now.Subtract(bytesPerInterval.ElementAt(bytesPerInterval.Count - needToRemove - 1).Key);
                //}
                //for (int i = 0; i < needToRemove; i++)
                //bytesPerInterval.Remove(bytesPerInterval.Keys.ElementAt(0));

                int bandwidth = 0;
                item = linkedList.First;
                while (item != null)
                {
                    bandwidth += item.Value.Value;
                    item = item.Next;
                }
                //foreach (var kvp in bytesPerInterval)
                //  bandwidth += kvp.Value;
                float KBps = (float)(bandwidth)/1000.0f;
                lastCalc = KBps.ToString("0.00");
                value = lastCalc;

                tempBytes = 0;
                lastUpdate = now;

                return true;
            }
            else
            {
                value = lastCalc;
                return false; // no update
            }
        }
    }
}
