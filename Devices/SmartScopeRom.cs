using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.HardwareInterfaces;
using System.Runtime.InteropServices;
using Common;
using MathNet.Numerics.LinearAlgebra.Double;

namespace ECore.Devices
{
    partial class SmartScope
    {
#if DEBUG
        public
#else
        private
#endif
        struct GainCalibration
        {
            public AnalogChannel channel;
            public double divider;
            public double multiplier;
            public double[] coefficients;
        }
        public struct FrequencyResponse
        {
            public AnalogChannel channel;
            public double multiplier;
            public Dictionary<int, float> magnitudes;
            public Dictionary<int, float> phases;
        }

        public List<FrequencyResponse> FrequencyResponses { get { return rom.frequencyResponse; } }

#if DEBUG
        public
#else
        private
#endif 
        class Rom
        {
            //number of coefficients per calibration
            const int gainCalibrationCoefficients = 3;
            const int frequencyResponseMagnitudes = 16;
            const int frequencyResponsePhases = 10;
            //Number of possible multiplier/divider combinations
            int modes = validMultipliers.Length * validDividers.Length;
            public UInt32 plugCount { get; private set; }
            public List<GainCalibration> gainCalibration { get; private set; }
            public List<FrequencyResponse> frequencyResponse { get; private set; }
            ISmartScopeUsbInterface hwInterface;
            public double[] computedMultipliers { get; private set; }
            public double[] computedDividers { get; private set; }

            internal Rom(ISmartScopeUsbInterface hwInterface)
            {
                this.hwInterface = hwInterface;
                Download();
            }

            private void computeDividersMultipliers()
            {
                computedMultipliers = new double[validMultipliers.Length];
                computedMultipliers[0] = 1;
                double[] referenceCalibration = getCalibration(AnalogChannel.ChA, validDividers[0], validMultipliers[0]).coefficients;

                for(int i = 1; i < validMultipliers.Length;i++)
                    computedMultipliers[i] = referenceCalibration[0] / getCalibration(AnalogChannel.ChA, validDividers[0], validMultipliers[i]).coefficients[0];

                computedDividers = new double[validDividers.Length];
                computedDividers[0] = 1;
                for (int i = 1; i < validDividers.Length; i++)
                    computedDividers[i] = getCalibration(AnalogChannel.ChA, validDividers[i], validMultipliers[0]).coefficients[0] / referenceCalibration[0];

            }

#if DEBUG
            public void clearCalibration()
            {
                this.gainCalibration.Clear();
            }

            public void setCalibration(GainCalibration c)
            {
                if (c.coefficients.Length != gainCalibrationCoefficients)
                    throw new Exception("Coefficients not of correct length!");

                this.gainCalibration.Add(c);
            }

            public void clearFrequencyResponse()
            {
                this.frequencyResponse.Clear();
            }

            public void setFrequencyResponse(FrequencyResponse f)
            {
                if (f.magnitudes.Count != frequencyResponseMagnitudes)
                    throw new Exception("Frequency response magnitudes not of correct length!");

                if (f.phases.Count != frequencyResponsePhases)
                    throw new Exception("Frequency response phases not of correct length!");

                this.frequencyResponse.Add(f);
            }
#endif

            public GainCalibration getCalibration(AnalogChannel ch, double divider, double multiplier)
            {
                return gainCalibration.Where(x => x.channel == ch && x.divider == divider && x.multiplier == multiplier).First();
            }

            private byte[] MapToBytes(Map m)
            {
                int size = Marshal.SizeOf(m);
                byte[] output = new byte[size];
                IntPtr p = Marshal.AllocHGlobal(size);

                Marshal.StructureToPtr(m, p, true);
                Marshal.Copy(p, output, 0, size);
                Marshal.FreeHGlobal(p);
                return output;
            }

            private Map BytesToMap(byte[] b)
            {
                Map m = new Map();
                int size = Marshal.SizeOf(m);
                IntPtr ptr = Marshal.AllocHGlobal(size);
                Marshal.Copy(b, 0, ptr, size);

                m = (Map)Marshal.PtrToStructure(ptr, m.GetType());
                Marshal.FreeHGlobal(ptr);

                return m;
            }

            [StructLayout(LayoutKind.Sequential)]
            unsafe struct Map
            {
                public UInt32 plugCount;
                public fixed float gainCalibration[gainCalibrationCoefficients * 3 * 3 * 2]; //calibrationSize * nDivider * nMultiplier * nChannel
                public fixed float magnitudes[frequencyResponseMagnitudes * 3 * 2]; //nFrequencyResponseMagnitudes * nMultiplier * nChannel
                public fixed ushort magnitudesIndices[frequencyResponseMagnitudes * 3 * 2]; //nFrequencyResponseMagnitudes * nMultiplier * nChannel
                public fixed float phases[frequencyResponsePhases * 3 * 2]; //nfrequencyResponsePhases * nMultiplier * nChannel
                public fixed ushort phasesIndices[frequencyResponsePhases * 3 * 2]; //nfrequencyResponsePhases * nMultiplier * nChannel
            }

#if DEBUG
            public bool Test(out long failAddress, out string message)
            {
                failAddress = -1;
                int addressSpace = 0x100;

                //Write
                byte[] data;
                int writeLength = 27; //32 - 5
                for (uint i = 0; i < addressSpace; i++)
                {
                    data = new byte[writeLength];
                    for(int j = 0; j < writeLength; j++) 
                        data[j] = (byte)(~(i + j));
                    try
                    {
                        hwInterface.SetControllerRegister(ScopeController.FLASH, i, data);
                    }
                    catch (Exception e)
                    {
                        message = "ROM test failed in write phase: " + e.Message;
                        failAddress = i;
                        return false;
                    }

                }
                //Read back
                uint readLength = 11; //16 - 5
                for (uint i = 0; i < addressSpace; i++)
                {
                    try
                    {
                        hwInterface.GetControllerRegister(ScopeController.FLASH, i, readLength, out data);
                    }
                    catch (Exception e)
                    {
                        message = "ROM test failed in readback phase: " + e.Message;
                        failAddress = i;
                        return false;
                    }

                    for (int j = 0; j < readLength; j++)
                    {
                        if (data[j] != (byte)(~(i + j)))
                        {
                            message = String.Format("Mismatch at address 0x{0:X4} - read {1}, expected {2}", i + j, data[j], (byte)(~(i + j)));
                            failAddress = i + j;
                            return false;
                        }
                    }
                }

                //Erase with xFF
                data = new byte[writeLength];
                for (int j = 0; j < writeLength; j++)
                    data[j] = 0xFF;
                for (uint i = 0; i < addressSpace; i++)
                {
                    try
                    {
                        hwInterface.SetControllerRegister(ScopeController.FLASH, i, data);
                    }
                    catch (Exception e)
                    {
                        message = "ROM test failed in erase phase: " + e.Message;
                        failAddress = i;
                        return false;
                    }
                }

                message = "All OK";
                return true;
            }
#endif

#if DEBUG
            public
#else
            private 
#endif
            void Upload()
            {
                //Fill ROM map structure
                Map m = new Map();
                m.plugCount = plugCount;
                int offset = 0;

                //FIXME: this code can be cleaner and shorter I suppose
                foreach (AnalogChannel ch in AnalogChannel.List)
                {
                    foreach (double divider in SmartScope.validDividers)
                    {
                        foreach (double multiplier in SmartScope.validMultipliers)
                        {
                            double[] coeff = this.gainCalibration.Where(x => x.channel.Value == ch.Value && x.divider == divider && x.multiplier == multiplier).First().coefficients;
                            unsafe
                            {
                                for (int i = 0; i < coeff.Length; i++)
                                    m.gainCalibration[offset + i] = (float)coeff[i];
                            }
                            offset += coeff.Length;
                        }
                    }
                }
                
                int magnitudesOffset = 0;
                int phasesOffset = 0;
                foreach (AnalogChannel ch in AnalogChannel.List)
                {
                    foreach (double multiplier in SmartScope.validMultipliers)
                    {
                        try
                        {
                            FrequencyResponse f = this.frequencyResponse.Where(x => x.multiplier == multiplier && x.channel == ch).First();
                            unsafe
                            {
                                foreach (var kvp in f.magnitudes)
                                {
                                    m.magnitudesIndices[magnitudesOffset] = (ushort)kvp.Key;
                                    m.magnitudes[magnitudesOffset] = kvp.Value;
                                    magnitudesOffset++;
                                }
                                foreach (var kvp in f.phases)
                                {
                                    m.phasesIndices[phasesOffset] = (ushort)kvp.Key;
                                    m.phases[phasesOffset] = kvp.Value;
                                    phasesOffset++;
                                }
                            }
                        }
                        catch (InvalidOperationException)
                        {
                            Logger.Warn(String.Format("Failed to upload frequency response to ROM for channel {0:G} and multiplier {1}", ch, multiplier));
                            continue;
                        }
                    }
                }

                byte[] b = MapToBytes(m);

                uint writeOffset = (uint)Marshal.SizeOf(m.plugCount);
                while (writeOffset < b.Length)
                {
                    uint writeLength=Math.Min(11, (uint)(b.Length - writeOffset));
                    byte[] tmp = new byte[writeLength];
                    Array.Copy(b, writeOffset, tmp, 0, writeLength);
                    hwInterface.SetControllerRegister(ScopeController.FLASH, writeOffset, tmp);
                    writeOffset += writeLength;
                }
            }
#if DEBUG
            public
#else
            private 
#endif
            void Download()
            {
                int size = Marshal.SizeOf(typeof(Map));
                byte[] romContents = new byte[size];
                uint maxReadLength = 11;
                for (uint byteOffset = 0; byteOffset < size; )
                {
                    uint readLength = (uint)Math.Min(maxReadLength, size - byteOffset);
                    byte[] tmp;
                    hwInterface.GetControllerRegister(ScopeController.FLASH, byteOffset, readLength, out tmp);
                    Array.Copy(tmp, 0, romContents, byteOffset, readLength);
                    byteOffset += readLength;
                }
                Map m = BytesToMap(romContents);
                this.plugCount = m.plugCount;

                this.gainCalibration = new List<GainCalibration>();
                int offset = 0;
                foreach (AnalogChannel ch in AnalogChannel.List)
                {
                    foreach (double divider in SmartScope.validDividers)
                    {
                        foreach (double multiplier in SmartScope.validMultipliers)
                        {
                            GainCalibration c = new GainCalibration()
                            {
                                channel = ch,
                                divider = divider,
                                multiplier = multiplier
                            };
                            double[] coeff = new double[gainCalibrationCoefficients];

                            unsafe
                            {
                                for (int i = 0; i < coeff.Length; i++)
                                    coeff[i] = (double)m.gainCalibration[offset + i];
                            }
                            c.coefficients = coeff;
                            offset += coeff.Length;

                            this.gainCalibration.Add(c);
                        }
                    }
                }
                computeDividersMultipliers();

                this.frequencyResponse = new List<FrequencyResponse>();
                int magnitudesOffset = 0;
                int phasesOffset = 0;
                foreach (AnalogChannel ch in AnalogChannel.List)
                {
                    foreach (double multiplier in SmartScope.validMultipliers)
                    {
                        try
                        {
                            FrequencyResponse f = new FrequencyResponse()
                            {
                                channel = ch,
                                multiplier = multiplier,
                                phases = new Dictionary<int, float>(),
                                magnitudes = new Dictionary<int, float>()
                            };
                            unsafe
                            {
                                for (int i = 0; i < frequencyResponsePhases; i++)
                                    f.phases.Add(m.phasesIndices[phasesOffset + i], m.phases[phasesOffset + i]);
                                phasesOffset += frequencyResponsePhases;
                                for (int i = 0; i < frequencyResponseMagnitudes; i++)
                                    f.magnitudes.Add(m.magnitudesIndices[magnitudesOffset + i], m.magnitudes[magnitudesOffset + i]);
                                magnitudesOffset += frequencyResponseMagnitudes;
                            }
                            this.frequencyResponse.Add(f);
                        }
                        catch (ArgumentException e)
                        {
                            Logger.Warn(String.Format("Failed to load frequency response from ROM for channel {0:G} and multiplier {1} [{2}]", ch, multiplier, e.Message));
                        }
                    }
                }
            }

#if DEBUG
            public static SmartScope.GainCalibration ComputeCalibration(AnalogChannel channel, double div, double mul, double[] inputVoltage, double[] adcValue, double[] yOffset)
            {
                int rows = adcValue.Length;
                int cols = 3;

                double[] matrixData = adcValue.Concat(
                                        yOffset.Concat(
                                        Enumerable.Repeat(1.0, rows)
                                        )).ToArray();

                var A = new DenseMatrix(rows, cols, matrixData);
                var B = new DenseMatrix(rows, 1, inputVoltage);
                var C = A.QR().Solve(B);
                return new SmartScope.GainCalibration()
                {
                    channel = channel,
                    divider = div,
                    multiplier = mul,
                    coefficients = C.ToColumnWiseArray()
                };
            }
#endif
        }
    }
}
