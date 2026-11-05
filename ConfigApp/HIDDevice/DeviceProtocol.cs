using System;

namespace ConfigAppNS
{
    /// <summary>What a 64-byte input report read back from the board turned out to be.</summary>
    internal enum ReplyKind
    {
        /// <summary>Frame starts with the command byte we asked for.</summary>
        Match,
        /// <summary>HidD_GetInputReport failed: HIDLibrary leaves the caller's buffer untouched, so the 0xFF sentinel survives.</summary>
        NoData,
        /// <summary>All zero: device has not written a reply yet (or just powered up).</summary>
        Empty,
        /// <summary>"A M R A W" — the board is streaming RAW debug frames and overwrites every reply.</summary>
        RawStream,
        /// <summary>Something else (another command's reply, keyboard report, garbage).</summary>
        Other,
    }

    /// <summary>Decoded GET_MODE (0x47) reply.</summary>
    internal sealed class ModeReply
    {
        public byte Mode;
        public byte Uid;
        public byte HVer;
        public byte FwMajor;
        public byte FwMinor;
        /// <summary>UID is not 0xA2 (ID_DDR). Shown as a tag, never a reason to reject the frame.</summary>
        public bool UidMismatch { get { return Uid != DeviceProtocol.ID_DDR; } }
        /// <summary>HVer is not 0x13 (the only hardware revision seen so far).</summary>
        public bool HVerUnknown { get { return HVer != DeviceProtocol.HVER_KNOWN; } }
        public string VersionText { get { return FwMajor.ToString() + "." + FwMinor.ToString("D02"); } }
    }

    /// <summary>
    /// Pure byte-level view of the HID protocol (no USB, no UI) so the parsing can be unit-tested
    /// and so App_DDR only has to decide what to do with a verdict.
    ///
    /// Input reports are fetched with HidD_GetInputReport through HIDLibrary.USBLib_DeviceCtrlInput
    /// (Offset = 1 strips the leading Report-ID byte), therefore buf[0] is already the command byte:
    ///   GET_MODE reply : 47 mode A2 13 FWmajor FWminor
    ///   GET_SENS reply : 52 52 53 50 type data[40] crc8
    ///   RAW stream     : 41 4D 52 41 57 keyHi keyLo AA raw...
    /// </summary>
    internal static class DeviceProtocol
    {
        public const byte CMD_GET_MODE = 0x47;
        public const byte CMD_GET_SENS = 0x52;
        public const byte CMD_SET_SENS = 0x57;
        public const byte CMD_SET_MRAW = 0x41;
        public const byte ID_DDR = 0xA2;
        public const byte HVER_KNOWN = 0x13;
        /// <summary>Written into the read buffer before HidD_GetInputReport; survives only if the read failed.</summary>
        public const byte SENTINEL = 0xFF;

        public static void FillSentinel(byte[] buf)
        {
            for (int i = 0; i < buf.Length; i++) buf[i] = SENTINEL;
        }

        public static bool IsRawFrame(byte[] buf, int offset)
        {
            return buf.Length >= offset + 5 &&
                   buf[offset + 0] == CMD_SET_MRAW &&
                   buf[offset + 1] == 0x4D && buf[offset + 2] == 0x52 &&
                   buf[offset + 3] == 0x41 && buf[offset + 4] == 0x57;
        }

        public static bool AllBytes(byte[] buf, int offset, int count, byte value)
        {
            for (int i = 0; i < count && offset + i < buf.Length; i++)
                if (buf[offset + i] != value) return false;
            return true;
        }

        /// <summary>Classify a reply against the command we sent. Only the first byte decides a Match.</summary>
        public static ReplyKind Classify(byte[] buf, int offset, byte expectedCmd)
        {
            if (buf == null || buf.Length < offset + 6) return ReplyKind.NoData;
            if (buf[offset] == expectedCmd) return ReplyKind.Match;
            if (IsRawFrame(buf, offset)) return ReplyKind.RawStream;
            if (AllBytes(buf, offset, buf.Length - offset, SENTINEL)) return ReplyKind.NoData;
            if (AllBytes(buf, offset, buf.Length - offset, 0x00)) return ReplyKind.Empty;
            return ReplyKind.Other;
        }

        /// <summary>
        /// Lenient GET_MODE decode: accept any frame whose first byte is 0x47. UID / HVer are
        /// reported back for annotation but never cause a rejection ("show something rather than nothing").
        /// </summary>
        public static ModeReply ParseModeReply(byte[] buf, int offset)
        {
            if (Classify(buf, offset, CMD_GET_MODE) != ReplyKind.Match) return null;
            ModeReply r = new ModeReply();
            r.Mode = buf[offset + 1];
            r.Uid = buf[offset + 2];
            r.HVer = buf[offset + 3];
            r.FwMajor = buf[offset + 4];
            r.FwMinor = buf[offset + 5];
            return r;
        }

        /// <summary>Strict 1.0.0.58/1.0.0.59 rule, kept only for the test harness / release notes comparison.</summary>
        public static bool LegacyStrictAccept(byte[] buf, int offset)
        {
            return buf != null && buf.Length >= offset + 3 &&
                   buf[offset] == CMD_GET_MODE && buf[offset + 2] == ID_DDR;
        }

        /// <summary>GET_SENS reply header check: 52 52 53 50 type.</summary>
        public static bool IsSensReplyHeader(byte[] buf, int offset, byte configType)
        {
            return buf.Length >= offset + 5 &&
                   buf[offset + 0] == CMD_GET_SENS && buf[offset + 1] == 0x52 &&
                   buf[offset + 2] == 0x53 && buf[offset + 3] == 0x50 &&
                   buf[offset + 4] == configType;
        }

        /// <summary>SET_SENS ack header check: 57 4F 4B crc type.</summary>
        public static bool IsSensAckHeader(byte[] buf, int offset, byte configType)
        {
            return buf.Length >= offset + 5 &&
                   buf[offset + 0] == CMD_SET_SENS && buf[offset + 1] == 0x4F &&
                   buf[offset + 2] == 0x4B && buf[offset + 4] == configType;
        }

        /// <summary>CRC8, poly 0x8C reflected — same as firmware / HIDLibrary.UPDATE_CRC.</summary>
        public static byte Crc8(byte[] buf, int offset, int count)
        {
            byte crc = 0;
            for (int i = 0; i < count; i++)
            {
                crc ^= buf[offset + i];
                for (int b = 0; b < 8; b++)
                    crc = (crc & 1) != 0 ? (byte)((crc >> 1) ^ 0x8C) : (byte)(crc >> 1);
            }
            return crc;
        }
    }
}
