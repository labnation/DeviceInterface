using LabNation.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace LabNation.DeviceInterface.Hardware
{
    public interface ISmartScopeHardwareUsb : IHardwareInterface
    {
        void WriteControlBytes(byte[] message, bool async);
        void WriteControlBytesBulk(byte[] message, bool async);
        void WriteControlBytesBulk(byte[] message, int offset, int length, bool async);
        void ReadControlBytes(byte[] buffer, int offset, int length);

        void GetData(byte[] buffer, int offset, int length);

        bool Destroyed { get; }
        void Destroy();
        void FlushDataPipe();
    }

    [FlagsAttribute]
    public enum HeaderFlags : byte
    {
        None = 0,
        Acquiring = 1,
        IsOverview = 2,
        IsLastAcquisition = 4,
        Rolling = 8,
        TimedOut = 16,
        AwaitingTrigger = 32,
        Armded = 64,
        IsFullAcqusition = 128,
    }

    [StructLayout(LayoutKind.Explicit, Size = Constants.SZ_HDR, Pack = 1)]
    unsafe public struct SmartScopeHeader
    {
        [FieldOffset(0)]
        public fixed byte magic[2];
        [FieldOffset(2)]
        public byte header_offset;
        [FieldOffset(3)]
        public byte bytes_per_burst;
        [FieldOffset(4)]
        public ushort n_bursts;
        [FieldOffset(6)]
        public ushort offset;
        [FieldOffset(10)]
        public HeaderFlags flags;
        [FieldOffset(11)]
        public byte acquisition_id;
        [FieldOffset(Constants.HDR_OFFSET)]
        public fixed byte regs[Constants.N_HDR_REGS];
        [FieldOffset(Constants.HDR_OFFSET + Constants.N_HDR_REGS)]
        public fixed byte strobes[(Constants.N_HDR_STROBES + 7) / 8];
    }

    public static class ISmartScopeHardwareUsbHelpers
    {
        public static unsafe bool IsValid(this SmartScopeHeader hdr)
        {
            return hdr.magic[0] == 'L' && hdr.magic[1] == 'N';
        }

        public static unsafe byte GetRegister(this SmartScopeHeader hdr, REG r)
        {
            return hdr.regs[Constants.HDR_REGS[r]];
        }

        public static unsafe bool GetStrobe(this SmartScopeHeader hdr, STR s)
        {
            int offset = Constants.HDR_STROBES[s];
            byte reg = hdr.strobes[offset / 8];
            return Utils.IsBitSet(reg, offset % 8);
        }
        public static int GetAcquisition(this ISmartScopeHardwareUsb usb, byte[] buffer)
        {
            usb.GetData(buffer, 0, Constants.SZ_HDR);
            GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            SmartScopeHeader hdr = (SmartScopeHeader)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(SmartScopeHeader));
            handle.Free();
            if (!hdr.IsValid())
            {
                Logger.Error("Invalid header magic");
                return 0;
            }
                

            if (hdr.flags.HasFlag(HeaderFlags.TimedOut))
                return Constants.SZ_HDR;

            if (hdr.flags.HasFlag(HeaderFlags.IsOverview))
            {
                usb.GetData(buffer, Constants.SZ_HDR, Constants.SZ_OVERVIEW);
                return Constants.SZ_HDR + Constants.SZ_OVERVIEW;
            }

            if (hdr.n_bursts == 0)
                throw new ScopeIOException("number of bursts in this USB pacakge is 0, cannot fetch");

            int len = hdr.n_bursts * hdr.bytes_per_burst;
            usb.GetData(buffer, Constants.SZ_HDR, len);
            return Constants.SZ_HDR + len;
        }
    }
}
