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
    partial class ScopeV2
    {
#if INTERNAL
        public
#else
        private
#endif
        struct Calibration
        {
            public AnalogChannel channel;
            public double divider;
            public double multiplier;
            public double[] coefficients;
        }

#if INTERNAL
        public
#else
        private
#endif 
        class Rom
        {
            //number of coefficients per calibration
            const int calibrationSize = 3;
            //Number of possible multiplier/divider combinations
            int modes = validMultipliers.Length * validDividers.Length;
            public UInt32 plugCount { get; private set; }
            public List<Calibration> calibration { get; private set; }
            ScopeUsbInterface hwInterface;
            public double[] computedMultipliers { get; private set; }
            public double[] computedDividers { get; private set; }

            internal Rom(ScopeUsbInterface hwInterface)
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

#if INTERNAL
            public void clearCalibration()
            {
                this.calibration.Clear();
            }

            public void setCalibration(Calibration c)
            {
                if (c.coefficients.Length != calibrationSize)
                    throw new Exception("Coefficients not of correct length!");

                this.calibration.Add(c);
            }
#endif

#if INTERNAL
            internal 
#else
            internal
#endif
            Calibration getCalibration(AnalogChannel ch, double divider, double multiplier)
            {
                return calibration.Where(x => x.channel == ch && x.divider == divider && x.multiplier == multiplier).First();
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
                public fixed float calibration[calibrationSize * 3 * 3 * 2]; //calibrationSize * nDivider * nMultiplier * nChannel
            }

#if INTERNAL
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

#if INTERNAL
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
                foreach (AnalogChannel ch in AnalogChannel.list)
                {
                    foreach (double divider in ScopeV2.validDividers)
                    {
                        foreach (double multiplier in ScopeV2.validMultipliers)
                        {
                            double[] coeff = this.calibration.Where(x => x.channel.Value == ch.Value && x.divider == divider && x.multiplier == multiplier).First().coefficients;
                            unsafe
                            {
                                for (int i = 0; i < coeff.Length; i++)
                                    m.calibration[offset + i] = (float)coeff[i];
                            }
                            offset += coeff.Length;
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
#if INTERNAL
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

                this.calibration = new List<Calibration>();
                int offset = 0;
                foreach (AnalogChannel ch in AnalogChannel.list)
                {
                    foreach (double divider in ScopeV2.validDividers)
                    {
                        foreach (double multiplier in ScopeV2.validMultipliers)
                        {
                            Calibration c = new Calibration()
                            {
                                channel = ch,
                                divider = divider,
                                multiplier = multiplier
                            };
                            double[] coeff = new double[calibrationSize];

                            unsafe
                            {
                                for (int i = 0; i < coeff.Length; i++)
                                    coeff[i] = (double)m.calibration[offset + i];
                            }
                            c.coefficients = coeff;
                            offset += coeff.Length;

                            this.calibration.Add(c);
                        }
                    }
                }
                computeDividersMultipliers();
            }

#if INTERNAL
            public static ScopeV2.Calibration ComputeCalibration(AnalogChannel channel, double div, double mul, double[] inputVoltage, double[] adcValue, double[] yOffset)
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
                return new ScopeV2.Calibration()
                {
                    channel = channel,
                    divider = div,
                    multiplier = mul,
                    coefficients = C.ToColumnWiseArray()
                };
            }
        }
#endif

    }
}
