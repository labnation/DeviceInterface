using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.Devices;
using LabNation.Common;
using LabNation.DeviceInterface.DataSources;

namespace LabNation.DeviceInterface
{
    public struct AnalogWaveProperties
    {
        public float minValue;
        public float maxValue;
        public double frequency;

        public AnalogWaveProperties(float minValue, float maxValue, double frequency)
        {
            this.minValue = minValue;
            this.maxValue = maxValue;
            this.frequency = frequency;
        }
    }                           
                                 
    public static class Tools
    {
        public static DataPackageScope FetchLastFrame(SmartScope scope)
        {
            DateTime oldFetchTime = DateTime.Now;
            DataPackageScope oldPackage = null;
            
            while (oldPackage == null)
                oldPackage = scope.GetScopeData();

            DataPackageScope p = null;
            do
            {
                scope.ForceTrigger();
                p = scope.GetScopeData();
                if (p == null) p = oldPackage;
            } while ((p.Identifier == oldPackage.Identifier) && (DateTime.Now.Subtract(oldFetchTime).TotalMilliseconds < 3000));
            return p;
        }

        public static Dictionary<AnalogChannel, AnalogWaveProperties> MeasureAutoArrangeSettings(IScope scope, AnalogChannel aciveChannel)
        {
            SmartScope s = scope as SmartScope;

            //check whether scope is ready/available
            if (s == null || !s.Ready)
            {
                Logger.Error("No scope found to perform AutoArrange");
                return null;
            }

            //stop scope streaming
            s.DataSourceScope.Stop();

            //Prepare scope for test
            s.SetDisableVoltageConversion(false);

            //set to wide timerange, but slightly off so smallest chance of aliasing
            const float initialTimeRange = 0.495f;
            s.AcquisitionMode = AcquisitionMode.AUTO;
            s.AcquisitionLength = initialTimeRange;
            //s.AcquisitionDepth = 4096;
            s.TriggerHoldOff = 0;
            s.SendOverviewBuffer = false;
            
            AnalogTriggerValue atv = new AnalogTriggerValue();
            atv.channel = AnalogChannel.ChA;
            atv.direction = TriggerDirection.RISING;
            atv.level = 5000;
            s.TriggerAnalog = atv;

            foreach (AnalogChannel ch in AnalogChannel.List)
                s.SetCoupling(ch, Coupling.DC);

            s.CommitSettings();

            ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            // VERTICAL

            //set to largest input range
            float maxRange = 1.2f / 1f * 36f;
            foreach (AnalogChannel ch in AnalogChannel.List)
                s.SetVerticalRange(ch, -maxRange / 2f, maxRange / 2f);

            float[] minValues = new float[] { float.MaxValue, float.MaxValue };
            float[] maxValues = new float[] { float.MinValue, float.MinValue };
            //measure min and max voltage over 3 full ranges
            for (int i = -1; i < 2; i++)
            {
                foreach (AnalogChannel ch in AnalogChannel.List)
                    s.SetYOffset(ch, (float)i * maxRange);
                s.CommitSettings();

                System.Threading.Thread.Sleep(100);
                s.ForceTrigger();

                //fetch data
                DataPackageScope p = FetchLastFrame(s);
                p = FetchLastFrame(s); //needs this second fetch as well to get voltage conversion on ChanB right?!?

                if (p == null)
                {
                    Logger.Error("Didn't receive data from scope, aborting");
                    return null;
                }

                //check if min or max need to be updated (only in case this measurement was not saturated)
                float[] dataA = (float[])p.GetData(DataSourceType.Viewport, AnalogChannel.ChA).array;
                float[] dataB = (float[])p.GetData(DataSourceType.Viewport, AnalogChannel.ChB).array;
                float minA = dataA.Min();
                float maxA = dataA.Max();
                float minB = dataB.Min();
                float maxB = dataB.Max();

                if (minA != p.SaturationLowValue[AnalogChannel.ChA] && minA != p.SaturationHighValue[AnalogChannel.ChA] && minValues[0] > minA) minValues[0] = minA;
                if (minB != p.SaturationLowValue[AnalogChannel.ChB] && minB != p.SaturationHighValue[AnalogChannel.ChB] && minValues[1] > minB) minValues[1] = minB;
                if (maxA != p.SaturationLowValue[AnalogChannel.ChA] && maxA != p.SaturationHighValue[AnalogChannel.ChA] && maxValues[0] < maxA) maxValues[0] = maxA;
                if (maxB != p.SaturationLowValue[AnalogChannel.ChB] && maxB != p.SaturationHighValue[AnalogChannel.ChB] && maxValues[1] < maxB) maxValues[1] = maxB;          
            }

            //calc ideal voltage range and offset
            float sizer = 3; //meaning 3 waves would fill entire view
            float[] amplitudes = new float[2];
            amplitudes[0] = maxValues[0] - minValues[0];
            amplitudes[1] = maxValues[1] - minValues[1];
            float[] desiredOffsets = new float[2];
            desiredOffsets[0] = (maxValues[0] + minValues[0]) / 2f;
            desiredOffsets[1] = (maxValues[1] + minValues[1]) / 2f;
            float[] desiredRanges = new float[2];
            desiredRanges[0] = amplitudes[0] * sizer;
            desiredRanges[1] = amplitudes[1] * sizer;

            //intervene in case the offset is out of range for this range
            if (desiredRanges[0] < Math.Abs(desiredOffsets[0]))
                desiredRanges[0] = Math.Abs(desiredOffsets[0]);
            if (desiredRanges[1] < Math.Abs(desiredOffsets[1]))
                desiredRanges[1] = Math.Abs(desiredOffsets[1]);

            //set fine voltage range and offset
            s.SetVerticalRange(AnalogChannel.ChA, -desiredRanges[0] / 2f, desiredRanges[0] / 2f);
            s.SetYOffset(AnalogChannel.ChA, -desiredOffsets[0]);
            s.SetVerticalRange(AnalogChannel.ChB, -desiredRanges[1] / 2f, desiredRanges[1] / 2f);
            s.SetYOffset(AnalogChannel.ChB, -desiredOffsets[1]);
            s.CommitSettings();

            //now get data in order to find accurate lowHigh levels (as in coarse mode this was not accurate)
            DataPackageScope pFine = FetchLastFrame(s);
            pFine = FetchLastFrame(s); //needs this second fetch as well to get voltage conversion on ChanB right?!?
            float[] dataAfine = (float[])pFine.GetData(DataSourceType.Viewport, AnalogChannel.ChA).array;
            float[] dataBfine = (float[])pFine.GetData(DataSourceType.Viewport, AnalogChannel.ChB).array;
            amplitudes[0] = dataAfine.Max() - dataAfine.Min();
            amplitudes[1] = dataBfine.Max() - dataBfine.Min();
            minValues[0] = dataAfine.Min();
            minValues[1] = dataBfine.Min();
            maxValues[0] = dataAfine.Max();
            maxValues[1] = dataBfine.Max();

            //set trigger in middle of active wave
            float[] activeData;
            float activeMinValue, activeMaxValue;
            if (aciveChannel == AnalogChannel.ChA)
            {
                activeData = dataAfine;
                activeMinValue = dataAfine.Min() + amplitudes[0]*0.1f;
                activeMaxValue = dataAfine.Max() - amplitudes[0] * 0.1f;
            }
            else
            {
                activeData = dataBfine;
                activeMinValue = dataBfine.Min() + amplitudes[1] * 0.1f;
                activeMaxValue = dataBfine.Max() - amplitudes[1] * 0.1f;
            }
            float activeAmplitude = Math.Abs(activeData.Min() + activeData.Max());
            float triggerLevel = (activeData.Min() + activeData.Max()) / 2f;
            AnalogTriggerValue trig = new AnalogTriggerValue();
            trig.channel = aciveChannel;
            trig.direction = TriggerDirection.RISING;
            trig.level = triggerLevel;
            scope.TriggerAnalog = trig;

            ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            // HORIZONTAL
            const float minTimeRange = 500f * 0.00000001f;//500 samples over full hor span
            const float maxTimeRange = 0.2f;            

            double frequency, frequencyError, dutyCycle, dutyCycleError;
            double finalFrequency = double.MinValue;
            int iterationCounter = 0;
            for (float currTimeRange = maxTimeRange; currTimeRange > minTimeRange; currTimeRange/=100f)
            {
                iterationCounter++;

                s.AcquisitionLength = currTimeRange;
                s.SetViewPort(0, s.AcquisitionLength);
                s.CommitSettings();

                DataPackageScope pHor = FetchLastFrame(s);
                pHor = FetchLastFrame(s);
                activeData = (float[])pFine.GetData(DataSourceType.Viewport, aciveChannel).array;

                float currMinVal = activeData.Min();
                float currMaxVal = activeData.Max();
                if (currMinVal > activeMinValue)
                    break;

                if (currMaxVal < activeMaxValue)
                    break;

                ComputeFrequencyDutyCycle(pHor.GetData(DataSourceType.Viewport, aciveChannel), out frequency, out frequencyError, out dutyCycle, out dutyCycleError);
                if (frequency > finalFrequency)
                    finalFrequency = frequency;
            }

            //in case of flatline or very low freq, initial value will not have changed
            if (finalFrequency == double.MinValue)
                finalFrequency = 0;

            Dictionary<AnalogChannel, AnalogWaveProperties> waveProperties = new Dictionary<AnalogChannel, AnalogWaveProperties>();
            waveProperties.Add(AnalogChannel.ChA, new AnalogWaveProperties(minValues[0], maxValues[0], finalFrequency));
            waveProperties.Add(AnalogChannel.ChB, new AnalogWaveProperties(minValues[1], maxValues[1], finalFrequency));

            return waveProperties;
        }

        public static void ComputeFrequencyDutyCycle(ChannelData data, out double frequency, out double frequencyError, out double dutyCycle, out double dutyCycleError)
        {
            frequency = double.NaN;
            frequencyError = double.NaN;    
            dutyCycle = double.NaN;
            dutyCycleError = double.NaN;

            bool[] digitized = data.array.GetType().GetElementType() == typeof(bool) ? (bool[])data.array : LabNation.Common.Utils.Schmitt((float[])data.array);

            List<double> edgePeriod = new List<double>();
            List<double> highPeriod = new List<double>();
            List<double> lowPeriod = new List<double>();

            int lastRisingIndex = 0;
            int lastFallingIndex = 0;
            double samplePeriod = data.samplePeriod;
            for (int i = 1; i < digitized.Length; i++)
            {
                //Edge detection by XOR-ing sample with previous sample
                bool edge = digitized[i] ^ digitized[i - 1];
                if (edge)
                {
                    //If we're high now, it's a rising edge
                    if (digitized[i])
                    {
                        if (lastRisingIndex > 0)
                            edgePeriod.Add((i - lastRisingIndex) * samplePeriod);
                        if (lastFallingIndex > 0)
                            lowPeriod.Add((i - lastFallingIndex) * samplePeriod);

                        lastRisingIndex = i;
                    }
                    else
                    {
                        if (lastFallingIndex > 0)
                            edgePeriod.Add((i - lastFallingIndex) * samplePeriod);
                        if (lastRisingIndex > 0)
                            highPeriod.Add((i - lastRisingIndex) * samplePeriod);
                        lastFallingIndex = i;
                    }
                }
            }

            if (edgePeriod.Count < 1)
                return;

            double average = edgePeriod.Average();

            if (highPeriod.Count > 0 && lowPeriod.Count > 0)
            {
                dutyCycle = highPeriod.Average() / average;
                dutyCycle += 1.0 - lowPeriod.Average() / average;
                dutyCycle *= 50;
            }


            double f = 1 / average;
            double fError = edgePeriod.Select(x => Math.Abs(1 / x - f)).Max();
            if (fError > f * 0.6)
                return;
            frequency = f;
            frequencyError = fError;
        }
    }
}
