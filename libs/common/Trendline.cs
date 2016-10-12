using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LabNation.Common
{
    public class Trendline
    {
        private readonly IList<double> xAxisValues;
        private readonly IList<double> yAxisValues;
        private int count;
        private double xAxisValuesSum;
        private double xxSum;
        private double xySum;
        private double yAxisValuesSum;

        public Trendline(IList<double> yAxisValues, IList<double> xAxisValues)
        {
            this.yAxisValues = yAxisValues;
            this.xAxisValues = xAxisValues;

            this.Initialize();
        }

        public double Slope { get; private set; }
        public double Intercept { get; private set; }
        public double Start { get; private set; }
        public double End { get; private set; }

        private void Initialize()
        {
            this.count = this.yAxisValues.Count;
            this.yAxisValuesSum = this.yAxisValues.Sum();
            this.xAxisValuesSum = this.xAxisValues.Sum();
            this.xxSum = 0;
            this.xySum = 0;

            for (int i = 0; i < this.count; i++)
            {
                this.xySum += (this.xAxisValues[i] * this.yAxisValues[i]);
                this.xxSum += (this.xAxisValues[i] * this.xAxisValues[i]);
            }

            this.Slope = this.CalculateSlope();
            this.Intercept = this.CalculateIntercept();
            this.Start = this.CalculateStart();
            this.End = this.CalculateEnd();
        }

        private double CalculateSlope()
        {
            try
            {
                return ((this.count * this.xySum) - (this.xAxisValuesSum * this.yAxisValuesSum)) / ((this.count * this.xxSum) - (this.xAxisValuesSum * this.xAxisValuesSum));
            }
            catch (DivideByZeroException)
            {
                return 0;
            }
        }

        private double CalculateIntercept()
        {
            return (this.yAxisValuesSum - (this.Slope * this.xAxisValuesSum)) / this.count;
        }

        private double CalculateStart()
        {
            return (this.Slope * this.xAxisValues.First()) + this.Intercept;
        }

        private double CalculateEnd()
        {
            return (this.Slope * this.xAxisValues.Last()) + this.Intercept;
        }
    }
}
