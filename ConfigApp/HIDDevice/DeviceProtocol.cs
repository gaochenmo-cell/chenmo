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
        /// <summary>HVer is neither the 5-key (0x13) nor the 4-key (0x14) board.</summary>
        public bool HVerUnknown { get { return !DeviceProtocol.IsKnownHVer(HVer); } }
        public string VersionText { get { return FwMajor.ToString() + "." + FwMinor.ToString("D02"); } }
    }

    /// <summary>
    /// What a firmware image says about itself. The GET_MODE reply is built at
    /// 0x08006148 from four `movs r1,#imm` constants; relative to the APP base 0x08004000 the
    /// HVer, FW major and FW minor immediates sit at file offsets 0x215E, 0x2164 and 0x216A
    /// (each `imm 21`). Images that do not have that opcode pattern report HasVersion == false.
    /// </summary>
    internal sealed class FirmwareImageInfo
    {
        public const int OFF_HVER = 0x215E;
        public const int OFF_FW_MAJOR = 0x2164;
        public const int OFF_FW_MINOR = 0x216A;
        private const byte MOVS_R1_HI = 0x21;

        public int Length;
        public string Sha256 = "";
        public bool HasVersion;
        public int HVer, FwMajor, FwMinor;

        public static FirmwareImageInfo Parse(byte[] data)
        {
            FirmwareImageInfo info = new FirmwareImageInfo();
            if (data == null) return info;
            info.Length = data.Length;
            try
            {
                using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
                {
                    byte[] h = sha.ComputeHash(data);
                    System.Text.StringBuilder sb = new System.Text.StringBuilder(64);
                    for (int i = 0; i < h.Length; i++) sb.Append(h[i].ToString("x2"));
                    info.Sha256 = sb.ToString();
                }
            }
            catch { info.Sha256 = "?"; }
            if (data.Length > OFF_FW_MINOR + 1
                && data[OFF_HVER + 1] == MOVS_R1_HI
                && data[OFF_FW_MAJOR + 1] == MOVS_R1_HI
                && data[OFF_FW_MINOR + 1] == MOVS_R1_HI)
            {
                info.HasVersion = true;
                info.HVer = data[OFF_HVER];
                info.FwMajor = data[OFF_FW_MAJOR];
                info.FwMinor = data[OFF_FW_MINOR];
            }
            return info;
        }

        /// <summary>Same formatting as the main window: "V3.2" for major >= 3, "2.17" for the old images.</summary>
        public static string VersionText(int major, int minor)
        {
            if (major >= 3) return "V" + major + "." + minor;
            return major.ToString() + "." + minor.ToString("D02");
        }

        public string VersionText() { return HasVersion ? VersionText(FwMajor, FwMinor) : "?"; }

        /// <summary>For YuanconDancepad.log: "HVer=14 FVer=V3.2 (03 02)" or "version=unknown".</summary>
        public string VersionLogText()
        {
            if (!HasVersion) return "version=unknown (no movs r1 pattern at 0x215E/0x2164/0x216A)";
            return string.Format("HVer={0:X2} FVer={1} ({2:X2} {3:X2})", HVer, VersionText(), FwMajor, FwMinor);
        }

        /// <summary>Compare the image version with a board version: &lt;0 older, 0 same, &gt;0 newer.</summary>
        public int CompareVersion(int major, int minor)
        {
            if (FwMajor != major) return FwMajor < major ? -1 : 1;
            if (FwMinor != minor) return FwMinor < minor ? -1 : 1;
            return 0;
        }

        public bool SameVersionAs(int hver, int major, int minor)
        {
            return HasVersion && HVer == hver && FwMajor == major && FwMinor == minor;
        }
    }

    /// <summary>
    /// Byte-level view of the HID protocol (no USB, no UI).
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
        /// <summary>HVer byte of the GET_MODE reply: 0x13 = 5-key board, 0x14 = 4-key board (firmware V3.0+).</summary>
        public const byte HVER_5KEY = 0x13;
        public const byte HVER_4KEY = 0x14;

        public static bool IsKnownHVer(int hver)
        {
            return hver == HVER_5KEY || hver == HVER_4KEY;
        }
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
        /// GET_MODE decode: accept any frame whose first byte is 0x47. UID / HVer are
        /// reported back for annotation but never cause a rejection.
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
