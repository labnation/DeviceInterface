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
        public float amplitude;
        public float offset;
        public bool isFlatline;
        public double frequency;

        public AnalogWaveProperties(float minValue, float maxValue, float amplitude, float offset, bool isFlatline, double frequency)
        {
            this.minValue   = minValue;
            this.maxValue   = maxValue; 
            this.amplitude  = amplitude; 
            this.offset     = offset;
            this.isFlatline = isFlatline;
            this.frequency = frequency;
        }
    }                           
                                 
    public static class Tools
    {
        public static DataPackageScope FetchLastFrame(IScope scope)
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

        public static Dictionary<AnalogChannel, AnalogWaveProperties> MeasureAutoArrangeSettings(IScope scope, AnalogChannel aciveChannel, Action<float> progressReport)
        {
            float progress = 0f;
            progressReport(progress);

            //stop scope streaming
            scope.DataSourceScope.Stop();

            //Prepare scope for test
            if (scope is SmartScope)
                (scope as SmartScope).SetDisableVoltageConversion(false);

            //set to timerange wide enough to capture 50Hz, but slightly off so smallest chance of aliasing
            const float initialTimeRange = 0.0277f;
            scope.AcquisitionMode = AcquisitionMode.AUTO;
            scope.AcquisitionLength = initialTimeRange;
            scope.SetViewPort(0, scope.AcquisitionLength);
            //s.AcquisitionDepth = 4096;
            scope.TriggerHoldOff = 0;
            scope.SendOverviewBuffer = false;

            TriggerValue trigger = new TriggerValue()
            {
                mode = TriggerMode.Edge,
                source = TriggerSource.Channel,
                channel = AnalogChannel.ChA,
                edge = TriggerEdge.RISING,
                level = 5000,
            };
            scope.TriggerValue = trigger;

            foreach (AnalogChannel ch in AnalogChannel.List)
                scope.SetCoupling(ch, Coupling.DC);

            scope.CommitSettings();
            progress += .1f;
            progressReport(progress);
            ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            // VERTICAL == VOLTAGE

            //set to largest input range
            float maxRange = 1.2f / 1f * 36f;
            foreach (AnalogChannel ch in AnalogChannel.List)
                scope.SetVerticalRange(ch, -maxRange / 2f, maxRange / 2f);

            float[] minValues = new float[] { float.MaxValue, float.MaxValue };
            float[] maxValues = new float[] { float.MinValue, float.MinValue };
            //measure min and max voltage over 3 full ranges
            for (int i = -1; i < 2; i++)
            {
                progress += .1f;
                progressReport(progress);

                foreach (AnalogChannel ch in AnalogChannel.List)
                    scope.SetYOffset(ch, (float)i * maxRange);
                scope.CommitSettings();

                System.Threading.Thread.Sleep(100);
                scope.ForceTrigger();

                //fetch data
                DataPackageScope p = FetchLastFrame(scope);

                if (p == null)
                {
                    Logger.Error("Didn't receive data from scope, aborting");
                    return null;
                }

                //check if min or max need to be updated (only in case this measurement was not saturated)
                float[] dataA = (float[])p.GetData(ChannelDataSourceScope.Viewport, AnalogChannel.ChA).array;
                float[] dataB = (float[])p.GetData(ChannelDataSourceScope.Viewport, AnalogChannel.ChB).array;
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
            float[] coarseAmplitudes = new float[2];
            coarseAmplitudes[0] = maxValues[0] - minValues[0];
            coarseAmplitudes[1] = maxValues[1] - minValues[1];
            float[] desiredOffsets = new float[2];
            desiredOffsets[0] = (maxValues[0] + minValues[0]) / 2f;
            desiredOffsets[1] = (maxValues[1] + minValues[1]) / 2f;
            float[] desiredRanges = new float[2];
            desiredRanges[0] = coarseAmplitudes[0] * sizer;
            desiredRanges[1] = coarseAmplitudes[1] * sizer;

            //intervene in case the offset is out of range for this range
            if (desiredRanges[0] < Math.Abs(desiredOffsets[0]))
                desiredRanges[0] = Math.Abs(desiredOffsets[0]);
            if (desiredRanges[1] < Math.Abs(desiredOffsets[1]))
                desiredRanges[1] = Math.Abs(desiredOffsets[1]);

            //set fine voltage range and offset
            scope.SetVerticalRange(AnalogChannel.ChA, -desiredRanges[0] / 2f, desiredRanges[0] / 2f);
            scope.SetYOffset(AnalogChannel.ChA, -desiredOffsets[0]);
            scope.SetVerticalRange(AnalogChannel.ChB, -desiredRanges[1] / 2f, desiredRanges[1] / 2f);
            scope.SetYOffset(AnalogChannel.ChB, -desiredOffsets[1]);
            scope.CommitSettings();

            System.Threading.Thread.Sleep(100);
            scope.ForceTrigger();

            //now get data in order to find accurate lowHigh levels (as in coarse mode this was not accurate)
            DataPackageScope pFine = FetchLastFrame(scope);
            
            Dictionary<AnalogChannel, float[]> dataFine = new Dictionary<AnalogChannel, float[]>();
            dataFine.Add(AnalogChannel.ChA, (float[])pFine.GetData(ChannelDataSourceScope.Viewport, AnalogChannel.ChA).array);
            dataFine.Add(AnalogChannel.ChB, (float[])pFine.GetData(ChannelDataSourceScope.Viewport, AnalogChannel.ChB).array);
            
            Dictionary<AnalogChannel, float> minimumValues = new Dictionary<AnalogChannel, float>();
            Dictionary<AnalogChannel, float> maximumValues = new Dictionary<AnalogChannel, float>();
            Dictionary<AnalogChannel, float> amplitudes = new Dictionary<AnalogChannel, float>();
            Dictionary<AnalogChannel, float> offsets = new Dictionary<AnalogChannel, float>();
            Dictionary<AnalogChannel, bool> isFlatline = new Dictionary<AnalogChannel, bool>();
            foreach (var kvp in dataFine)
            {
                minimumValues.Add(kvp.Key, kvp.Value.Min());
                maximumValues.Add(kvp.Key, kvp.Value.Max());
                amplitudes.Add(kvp.Key, kvp.Value.Max() - kvp.Value.Min());
                offsets.Add(kvp.Key, (kvp.Value.Max() + kvp.Value.Min())/2f);
                isFlatline.Add(kvp.Key, amplitudes[kvp.Key] < 0.01f);
            }

            ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            // HORIZONTAL == FREQUENCY
            
            const float minTimeRange = 500f * 0.00000001f;//500 samples over full hor span
            const float maxTimeRange = 1f;            

            double frequency, frequencyError, dutyCycle, dutyCycleError;
            Dictionary<AnalogChannel, double> finalFrequencies = new Dictionary<AnalogChannel, double>();
            finalFrequencies.Add(AnalogChannel.ChA, double.MaxValue);
            finalFrequencies.Add(AnalogChannel.ChB, double.MaxValue);
            int iterationCounter = 0;   //only for performance testing
            float currTimeRange = minTimeRange;
            bool continueLooping = true;
            if (isFlatline.Where(x => x.Value).ToList().Count == isFlatline.Count) //no need to find frequency in case of 2 DC signals
                continueLooping = false;
            int remeasureCounter = 0; // in case of spikes, we try to remeasure until captured. But slow sines can cause this to go wild
            while (continueLooping)
            {
                progress += .04f;
                progressReport(progress);

                iterationCounter++;     //only for performance testing

                scope.AcquisitionLength = currTimeRange;
                scope.SetViewPort(0, scope.AcquisitionLength);
                scope.CommitSettings();

                System.Threading.Thread.Sleep(100);
                scope.ForceTrigger();

                DataPackageScope pHor = FetchLastFrame(scope);
                Dictionary<AnalogChannel, float[]> timeData = new Dictionary<AnalogChannel, float[]>();
                timeData.Add(AnalogChannel.ChA, (float[])pHor.GetData(ChannelDataSourceScope.Viewport, AnalogChannel.ChA).array);
                timeData.Add(AnalogChannel.ChB, (float[])pHor.GetData(ChannelDataSourceScope.Viewport, AnalogChannel.ChB).array);

                bool notCapturedFullAmplitude = false;
                foreach (var kvp in timeData)
                {
                    //make sure entire amplitude is in view
                    float currMinVal = kvp.Value.Min();
                    float currMaxVal = kvp.Value.Max();
                    float lowMarginValue = minimumValues[kvp.Key] + amplitudes[kvp.Key] * 0.1f;
                    float highMarginValue = maximumValues[kvp.Key] - amplitudes[kvp.Key] * 0.1f;
                    if (currMinVal > lowMarginValue || currMaxVal < highMarginValue)
                    {
                        notCapturedFullAmplitude = true;
                        continue;
                    }

                    Dictionary<int, bool> risingNFallingEdges;
                    ComputeFrequencyDutyCycle(pHor.GetData(ChannelDataSourceScope.Viewport, kvp.Key), out frequency, out frequencyError, out dutyCycle, out dutyCycleError, out risingNFallingEdges, currMinVal, currMaxVal);
                    if (!double.IsNaN(frequency) && (finalFrequencies[kvp.Key] == double.MaxValue))
                        finalFrequencies[kvp.Key] = frequency;
                }

                if (notCapturedFullAmplitude && remeasureCounter++ < 5)
                {
                    //need to re-measure, so don't increment
                }
                else
                {
                    remeasureCounter = 0;
                    //update and check whether we've found what we were looking for
                    currTimeRange *= 100f;
                    bool freqFoundForAllActiveWaves = true;
                    foreach (var kvp in timeData)
                        if (!isFlatline[kvp.Key] && finalFrequencies[kvp.Key] == double.MaxValue)
                            freqFoundForAllActiveWaves = false;
                    continueLooping = !freqFoundForAllActiveWaves;
                    if (currTimeRange > maxTimeRange)
                        continueLooping = false;
                }                
            } 

            //in case of flatline or very low freq, initial value will not have changed
            foreach (AnalogChannel ch in finalFrequencies.Keys.ToList())
                if (finalFrequencies[ch] == double.MaxValue)
                    finalFrequencies[ch] = 0;

            Dictionary<AnalogChannel, AnalogWaveProperties> waveProperties = new Dictionary<AnalogChannel, AnalogWaveProperties>();
            foreach (var kvp in isFlatline)
                waveProperties.Add(kvp.Key, new AnalogWaveProperties(minimumValues[kvp.Key], maximumValues[kvp.Key], amplitudes[kvp.Key], offsets[kvp.Key], isFlatline[kvp.Key], finalFrequencies[kvp.Key]));

            return waveProperties;
        }

        public static void ComputeRiseFallTimes(float[] voltages, float minVoltage, float maxVoltage, double samplePeriod, float pct, Dictionary<int, bool> risingNFallingEdges, out double riseTime, out double fallTime)
        {
            riseTime = double.NaN;
            fallTime = double.NaN;
            List<double> riseTimes = new List<double>();
            List<double> fallTimes = new List<double>();

            float amp = maxVoltage - minVoltage;
            float lowVBoundary = minVoltage + amp*(1f-pct)/2f;
            float highVBoundary = maxVoltage - amp*(1f-pct)/2f;

            //copy for fast access -- ElementAt is terribly slow!!
            int[] risingNFallingEdgesIndices = risingNFallingEdges.Keys.ToArray();
            bool[] risingNFallingEdgesBools = risingNFallingEdges.Values.ToArray();

            for (int i = 1; i < risingNFallingEdges.Count-1; i++)
            {
                bool risingEdge = risingNFallingEdgesBools[i];
                int prevCrossIndex = risingNFallingEdgesIndices[i - 1];
                int currCrossIndex = risingNFallingEdgesIndices[i];
                int nextCrossIndex = risingNFallingEdgesIndices[i + 1];
                
                if (risingEdge) //rising edge
                {
                    //walk backwards                    
                    int beginIndex = currCrossIndex;
                    while (beginIndex > prevCrossIndex && voltages[beginIndex] > lowVBoundary)
                        beginIndex--;                    

                    //walk forward
                    int endIndex = currCrossIndex;
                    while (endIndex < nextCrossIndex && voltages[endIndex] < highVBoundary)
                        endIndex++;
                    
                    //only add in case both boundary voltages were reached
                    if (beginIndex > prevCrossIndex && endIndex < nextCrossIndex)
                        if (endIndex - beginIndex - 1 > 1)
                            riseTimes.Add((endIndex - beginIndex - 1)*samplePeriod);
                }
                else //falling edge
                {
                    //walk backwards                    
                    int beginIndex = currCrossIndex;
                    while (beginIndex > prevCrossIndex && voltages[beginIndex] < highVBoundary)
                        beginIndex--;

                    //walk forward
                    int endIndex = currCrossIndex;
                    while (endIndex < nextCrossIndex && voltages[endIndex] > lowVBoundary)
                        endIndex++;

                    //only add in case both boundary voltages were reached
                    if (beginIndex > prevCrossIndex && endIndex < nextCrossIndex)
                        if (endIndex - beginIndex - 1 > 1)
                            fallTimes.Add((endIndex - beginIndex - 1) * samplePeriod);
                }
            }

            if (riseTimes.Count == 0)
                riseTime = double.NaN;
            else
                riseTime = riseTimes.Average();

            if (fallTimes.Count == 0)
                fallTime = double.NaN;
            else
                fallTime = fallTimes.Average();
        }

        public static void ComputeFrequencyDutyCycle(ChannelData data, out double frequency, out double frequencyError, out double dutyCycle, out double dutyCycleError, out Dictionary<int, bool> risingNFallingEdges, float minVoltage, float maxVoltage)
        {
            frequency = double.NaN;
            frequencyError = double.NaN;    
            dutyCycle = double.NaN;
            dutyCycleError = double.NaN;
            risingNFallingEdges = new Dictionary<int, bool>();

            bool[] digitized = data.array.GetType().GetElementType() == typeof(bool) ? (bool[])data.array : LabNation.Common.Utils.Schmitt((float[])data.array, minVoltage, maxVoltage);

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
                        risingNFallingEdges.Add(i, true);
                        if (lastRisingIndex > 0)
                            edgePeriod.Add((i - lastRisingIndex) * samplePeriod);
                        if (lastFallingIndex > 0)
                            lowPeriod.Add((i - lastFallingIndex) * samplePeriod);

                        lastRisingIndex = i;
                    }
                    else
                    {
                        risingNFallingEdges.Add(i, false);
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

            //if freq is way too high to be measured
            if (risingNFallingEdges.Count >= digitized.Length/2)
            {
                //invalidate all results and return
                frequency = double.NaN;
                frequencyError = double.NaN;
                dutyCycle = double.NaN;
                dutyCycleError = double.NaN;
                risingNFallingEdges = new Dictionary<int, bool>();
                return;
            }

            //take median
            edgePeriod.Sort();
            double median = edgePeriod.ElementAt((int)edgePeriod.Count / 2);

            if (highPeriod.Count > 0 && lowPeriod.Count > 0)
            {
                dutyCycle = highPeriod.Average() / median;
                dutyCycle += 1.0 - lowPeriod.Average() / median;
                dutyCycle *= 50;
            }
            if (dutyCycle < 0)
                dutyCycle = 0;
            if (dutyCycle > 100)
                dutyCycle = 100;

            //reject if more than 5% of are more than 50% off
            float rejectRatio = 0.05f;
            float errorRatio = 0.5f;
            double f = 1 / median;
            double fError = edgePeriod.Select(x => Math.Abs(1 / x - f)).Max();
            List<double> outliers = edgePeriod.Where(x => Math.Abs(x - median) > median * errorRatio).ToList();
            if (fError > f * 0.6 && outliers.Count > edgePeriod.Count * rejectRatio)
                return;
            frequency = f;
            frequencyError = fError;
        }
    }
}
