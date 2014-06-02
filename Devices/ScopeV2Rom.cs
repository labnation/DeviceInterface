using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.HardwareInterfaces;
using System.Runtime.InteropServices;

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
            public ulong plugCount { get; private set; }
            public List<Calibration> calibration { get; private set; }
            ScopeUsbInterface hwInterface;

            internal Rom(ScopeUsbInterface hwInterface)
            {
                this.hwInterface = hwInterface;
                Download();
                plugCount++;
                Upload();
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
            public 
#else
            private
#endif
            double[] getCalibration(AnalogChannel ch, double divider, double multiplier)
            {
                return calibration.Where(x => x.channel == ch && x.divider == divider && x.multiplier == multiplier).First().coefficients;
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
                public ulong plugCount;
                public fixed float calibration[calibrationSize * 3 * 3 * 2]; //calibrationSize * nDivider * nMultiplier * nChannel
            }

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

                hwInterface.EraseROM();
                int bytesWritten = 0;
                while (bytesWritten < b.Length)
                {
                    byte[] tmp = new byte[16];
                    Array.Copy(b, bytesWritten, tmp, 0, Math.Min(16, b.Length - bytesWritten));
                    bytesWritten += hwInterface.Write16BytesToROM(bytesWritten, tmp);
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
                byte[] romContents = hwInterface.ReadBytesFromROM(0, size);
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
            }
        }

    }
}
