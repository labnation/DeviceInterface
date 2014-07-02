using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ECore.DeviceMemories;
using Common;

namespace ECore.Devices
{
    public class ScopeV2Header
    {
        private byte[] raw;
        private byte bursts;
        public byte bytesPerBurst { get; private set; }
        public int Samples { get; private set; }
        public bool scopeRunning { get; private set; }
        public const int channels = 2;
        public ScopeV2Header(byte[] data)
        {
            int headerSize = AcquisitionRegisters.Length + DumpRegisters.Length + (int)Math.Ceiling(AcquisitionStrobes.Length / 8.0);
            raw = new byte[headerSize];
            if (data[0] != 'L' || data[1] != 'N')
                throw new Exception("Invalid magic number, can't parse header");

            int headerOffset = data[2];
            Array.Copy(data, headerOffset, raw, 0, headerSize);
            
            bytesPerBurst = data[3];
            bursts = data[4];
            Samples = bursts * bytesPerBurst / channels;

            scopeRunning = Utils.IsBitSet(data[5], 0);
        }

        public byte GetRegister(REG r)
        {
            int offset = 0;
            if (!AcquisitionRegisters.Contains(r))
            {
                if (!DumpRegisters.Contains(r))
                    throw new Exception("Register " + r.ToString("G") + " not part of header");
                else
                    offset = AcquisitionRegisters.Length + Array.IndexOf(DumpRegisters, r);;
            }
            else
            {
                offset = Array.IndexOf(AcquisitionRegisters, r);
            }

            return raw[offset]; 
        }

        public bool GetStrobe(STR s)
        {
            if(!AcquisitionStrobes.Contains(s))
                throw new Exception("Strobe  " + s.ToString("G") + " not part of header");
            int offset = AcquisitionRegisters.Length + DumpRegisters.Length;
            offset += Array.IndexOf(AcquisitionStrobes, s) / 8;
            return Utils.IsBitSet(raw[offset], (int)Array.IndexOf(AcquisitionStrobes, s) % 8);
        }
        /*
         * WARNING: the following arrays are manually constructed from VHDL code in
         * TypesConstants.vhd - Make sure that when you change the VHDL, you also
         * update this code
         */
        public static readonly REG[] AcquisitionRegisters = new REG[]
        {
            REG.TRIGGERLEVEL, 
			REG.TRIGGERHOLDOFF_B0, 
            REG.TRIGGERHOLDOFF_B1, 
			REG.CHA_YOFFSET_VOLTAGE, 
			REG.CHB_YOFFSET_VOLTAGE, 
			REG.DIVIDER_MULTIPLIER,
			REG.SAMPLECLOCKDIVIDER_B0, 
            REG.SAMPLECLOCKDIVIDER_B1
        };
        public static readonly REG[] DumpRegisters = new REG[]
        {
			REG.VIEW_DECIMATION,
			REG.VIEW_OFFSET,	
			REG.VIEW_ACQUISITIONS,
			REG.VIEW_BURSTS
        };
        public static readonly STR[] AcquisitionStrobes = new STR[]
        {
			STR.AWG_ENABLE,
			STR.LA_ENABLE,
			STR.FREE_RUNNING,
			STR.CHA_DCCOUPLING,
			STR.CHB_DCCOUPLING,
			STR.TRIGGER_CHB,
			STR.TRIGGER_FALLING,
            STR.DEBUG_RAM,
            STR.DIGI_DEBUG
        };
    }
}
