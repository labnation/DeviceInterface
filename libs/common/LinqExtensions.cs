using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LabNation.Common
{
    public static class Extensions
    {
        public static double StdDev(this IEnumerable<double> values)
        {
            // ref: http://warrenseen.com/blog/2006/03/13/how-to-calculate-standard-deviation/
            double mean = 0.0;
            double sum = 0.0;
            double stdDev = 0.0;
            int n = 0;
            foreach (double val in values)
            {
                n++;
                double delta = val - mean;
                mean += delta / n;
                sum += delta * (val - mean);
            }
            if (1 < n)
                stdDev = Math.Sqrt(sum / (n - 1));

            return stdDev;
        }

        public static float Median(this IEnumerable<float> source)
        {
            int decimals = source.Count();

            int midpoint = (decimals - 1) / 2;
            IEnumerable<float> sorted = source.OrderBy(n => n);
            
            float median = sorted.ElementAt(midpoint);
            if (decimals % 2 == 0)
                median = (median + sorted.ElementAt(midpoint + 1)) / 2;

            return median;
        }
    }
}