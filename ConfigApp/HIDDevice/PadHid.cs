using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ConfigAppNS
{
    /// <summary>
    /// One HID interface found on the bus: where it is (device path), what it is (VID / PID /
    /// usage page) and which physical board it belongs to (USB serial number).
    /// </summary>
    internal sealed class HidDeviceInfo
    {
        public string Path = "";
        public ushort Vid, Pid;
        /// <summary>iSerialNumber string. The pad firmware builds it from the MCU unique ID
        /// (12 hex digits), so it is stable per board and independent of the USB port.</summary>
        public string Serial = "";
        public ushort UsagePage, Usage;
        public int InputReportLen, OutputReportLen;

        /// <summary>
        /// Key that identifies the physical board. Serial number when the device has one;
        /// otherwise the parent-instance part of the device path (the "7&amp;xxxxxxxx&amp;0"
        /// segment Windows derives from the hub port), which is shared by all interfaces of
        /// one composite device and stays the same while the board sits on the same port.
        /// </summary>
        public string BoardKey
        {
            get
            {
                if (!string.IsNullOrEmpty(Serial)) return Serial;
                return PortKey;
            }
        }

        /// <summary>Instance segment of the device path without the trailing "&amp;000N" interface index.</summary>
        public string PortKey
        {
            get
            {
                // \\?\hid#vid_4d47&pid_5761&mi_01#7&1a2b3c4d&0&0000#{guid}
                string p = Path ?? "";
                int hash1 = p.IndexOf('#');
                if (hash1 < 0) return p;
                int hash2 = p.IndexOf('#', hash1 + 1);
                if (hash2 < 0) return p;
                int hash3 = p.IndexOf('#', hash2 + 1);
                string inst = hash3 > 0 ? p.Substring(hash2 + 1, hash3 - hash2 - 1) : p.Substring(hash2 + 1);
                int lastAmp = inst.LastIndexOf('&');
                if (lastAmp > 0 && inst.Length - lastAmp == 5) inst = inst.Substring(0, lastAmp);
                return "port:" + inst;
            }
        }

        public bool IsVendorPage { get { return UsagePage >= 0xFF00; } }

        public override string ToString()
        {
            return string.Format("{0:X4}:{1:X4} sn={2} page={3:X4} in={4} out={5} {6}",
                Vid, Pid, Serial.Length > 0 ? Serial : "-", UsagePage, InputReportLen, OutputReportLen, Path);
        }
    }

    /// <summary>
    /// An open HID interface used with HidD_SetOutputReport / HidD_GetInputReport, the same two
    /// control transfers the configuration protocol has always used. One instance per board, so
    /// two boards can be driven at the same time.
    /// </summary>
    internal sealed class HidPort : IDisposable
    {
        private SafeFileHandle handle;
        private readonly byte[] setBuf = new byte[65];
        private readonly byte[] getBuf = new byte[65];

        public HidDeviceInfo Info { get; private set; }
        public bool IsOpen { get { return handle != null && !handle.IsInvalid && !handle.IsClosed; } }
        /// <summary>Set when a transfer failed with "device not connected" or similar; the owner should re-enumerate.</summary>
        public bool Broken { get; private set; }
        public int LastError { get; private set; }
        private int consecutiveFailures;
        private const int FAILURES_UNTIL_BROKEN = 10;   // 100 ms of the 10 ms poll without a single good transfer

        private HidPort() { }

        public static HidPort Open(HidDeviceInfo info)
        {
            SafeFileHandle h = HidNative.OpenPath(info.Path);
            if (h == null || h.IsInvalid)
                return null;
            HidPort p = new HidPort();
            p.handle = h;
            p.Info = info;
            return p;
        }

        /// <summary>SET_REPORT(Output): [reportId][data..len]. Mirrors HIDLibrary.USBLib_DeviceCtrlOutput.</summary>
        public bool SetOutputReport(byte reportId, byte[] data, int len)
        {
            if (!IsOpen) return false;
            if (len > 64) len = 64;
            Array.Clear(setBuf, 0, setBuf.Length);
            setBuf[0] = reportId;
            Buffer.BlockCopy(data, 0, setBuf, 1, len);
            bool ok = HidNative.HidD_SetOutputReport(handle, setBuf, (uint)(len + 1));
            if (ok) consecutiveFailures = 0; else NoteFailure();
            return ok;
        }

        /// <summary>
        /// GET_REPORT(Input) into <paramref name="dst"/>: dst[i] = report[offset + i] for i &lt; len.
        /// Mirrors HIDLibrary.USBLib_DeviceCtrlInput (offset 1 strips the report-id byte).
        /// On failure dst is left untouched, so a sentinel pre-fill survives.
        /// </summary>
        public bool GetInputReport(byte reportId, int offset, int len, byte[] dst)
        {
            if (!IsOpen) return false;
            if (len > 64) len = 64;
            Array.Clear(getBuf, 0, getBuf.Length);
            getBuf[0] = reportId;
            bool ok = HidNative.HidD_GetInputReport(handle, getBuf, (uint)(len + 1));
            if (!ok)
            {
                NoteFailure();
                return false;
            }
            consecutiveFailures = 0;
            for (int i = 0; i < len && offset + i < getBuf.Length; i++)
                dst[i] = getBuf[offset + i];
            return true;
        }

        private void NoteFailure()
        {
            LastError = Marshal.GetLastWin32Error();
            consecutiveFailures++;
            // ERROR_FILE_NOT_FOUND / ERROR_INVALID_HANDLE / ERROR_GEN_FAILURE / ERROR_NO_SUCH_DEVICE /
            // ERROR_OPERATION_ABORTED / ERROR_DEVICE_NOT_CONNECTED, or nothing but failures for a while
            if (LastError == 2 || LastError == 6 || LastError == 31 || LastError == 433 || LastError == 995 || LastError == 1167
                || consecutiveFailures >= FAILURES_UNTIL_BROKEN)
                Broken = true;
        }

        public void Dispose()
        {
            try { if (handle != null && !handle.IsClosed) handle.Close(); } catch { }
            handle = null;
        }
    }

    /// <summary>Enumerates HID interfaces by VID / PID list, with serial number and usage data.</summary>
    internal static class HidEnumerator
    {
        /// <summary>
        /// Every HID interface whose VID matches and whose PID is in <paramref name="pids"/>.
        /// Interfaces Windows refuses to open for read / write (the boot keyboard collection of
        /// the pad) are skipped, exactly like the previous library did.
        /// </summary>
        public static List<HidDeviceInfo> Find(int vid, int[] pids)
        {
            List<HidDeviceInfo> list = new List<HidDeviceInfo>();
            Guid hidGuid;
            HidNative.HidD_GetHidGuid(out hidGuid);
            IntPtr devInfo = HidNative.SetupDiGetClassDevs(ref hidGuid, null, IntPtr.Zero, HidNative.DIGCF_PRESENT | HidNative.DIGCF_DEVICEINTERFACE);
            if (devInfo == IntPtr.Zero || devInfo == new IntPtr(-1))
                return list;
            try
            {
                HidNative.SP_DEVICE_INTERFACE_DATA ifData = new HidNative.SP_DEVICE_INTERFACE_DATA();
                ifData.cbSize = Marshal.SizeOf(typeof(HidNative.SP_DEVICE_INTERFACE_DATA));
                for (int index = 0; HidNative.SetupDiEnumDeviceInterfaces(devInfo, IntPtr.Zero, ref hidGuid, index, ref ifData); index++)
                {
                    int required = 0;
                    HidNative.SetupDiGetDeviceInterfaceDetail(devInfo, ref ifData, IntPtr.Zero, 0, ref required, IntPtr.Zero);
                    HidNative.SP_DEVICE_INTERFACE_DETAIL_DATA detail = new HidNative.SP_DEVICE_INTERFACE_DETAIL_DATA();
                    detail.cbSize = IntPtr.Size == 8 ? 8 : 5;
                    if (!HidNative.SetupDiGetDeviceInterfaceDetail(devInfo, ref ifData, ref detail, required, ref required, IntPtr.Zero))
                        continue;
                    string path = detail.DevicePath;
                    if (string.IsNullOrEmpty(path)) continue;

                    SafeFileHandle h = HidNative.OpenPath(path);
                    if (h == null || h.IsInvalid)
                        continue;
                    try
                    {
                        HidNative.HIDD_ATTRIBUTES attrs = new HidNative.HIDD_ATTRIBUTES();
                        attrs.Size = Marshal.SizeOf(typeof(HidNative.HIDD_ATTRIBUTES));
                        if (!HidNative.HidD_GetAttributes(h.DangerousGetHandle(), ref attrs))
                            continue;
                        if ((attrs.VendorID & 0xFFFF) != vid) continue;
                        bool pidOk = false;
                        foreach (int p in pids) if (p != 0 && (attrs.ProductID & 0xFFFF) == p) pidOk = true;
                        if (!pidOk) continue;

                        HidDeviceInfo info = new HidDeviceInfo();
                        info.Path = path;
                        info.Vid = (ushort)attrs.VendorID;
                        info.Pid = (ushort)attrs.ProductID;
                        info.Serial = ReadSerial(h);
                        ReadCaps(h, info);
                        list.Add(info);
                    }
                    finally
                    {
                        try { h.Close(); } catch { }
                    }
                }
            }
            finally
            {
                HidNative.SetupDiDestroyDeviceInfoList(devInfo);
            }
            return list;
        }

        private static string ReadSerial(SafeFileHandle h)
        {
            try
            {
                byte[] buf = new byte[256];
                if (!HidNative.HidD_GetSerialNumberString(h, buf, (uint)buf.Length))
                    return "";
                string s = Encoding.Unicode.GetString(buf);
                int nul = s.IndexOf('\0');
                if (nul >= 0) s = s.Substring(0, nul);
                return s.Trim();
            }
            catch
            {
                return "";
            }
        }

        private static void ReadCaps(SafeFileHandle h, HidDeviceInfo info)
        {
            IntPtr preparsed = IntPtr.Zero;
            try
            {
                if (!HidNative.HidD_GetPreparsedData(h, out preparsed) || preparsed == IntPtr.Zero)
                    return;
                HidNative.HIDP_CAPS caps = new HidNative.HIDP_CAPS();
                caps.Reserved = new ushort[17];
                HidNative.HidP_GetCaps(preparsed, ref caps);
                info.UsagePage = caps.UsagePage;
                info.Usage = caps.Usage;
                info.InputReportLen = caps.InputReportByteLength;
                info.OutputReportLen = caps.OutputReportByteLength;
            }
            catch { }
            finally
            {
                if (preparsed != IntPtr.Zero)
                    try { HidNative.HidD_FreePreparsedData(preparsed); } catch { }
            }
        }
    }

    internal static class HidNative
    {
        public const int DIGCF_PRESENT = 0x00000002;
        public const int DIGCF_DEVICEINTERFACE = 0x00000010;
        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;
        private const uint FILE_FLAG_OVERLAPPED = 0x40000000;

        [StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVICE_INTERFACE_DATA
        {
            public int cbSize;
            public Guid InterfaceClassGuid;
            public int Flags;
            public IntPtr RESERVED;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct SP_DEVICE_INTERFACE_DETAIL_DATA
        {
            public int cbSize;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string DevicePath;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HIDD_ATTRIBUTES
        {
            public int Size;
            public ushort VendorID;
            public ushort ProductID;
            public ushort VersionNumber;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HIDP_CAPS
        {
            public ushort Usage;
            public ushort UsagePage;
            public ushort InputReportByteLength;
            public ushort OutputReportByteLength;
            public ushort FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
            public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes;
            public ushort NumberInputButtonCaps;
            public ushort NumberInputValueCaps;
            public ushort NumberInputDataIndices;
            public ushort NumberOutputButtonCaps;
            public ushort NumberOutputValueCaps;
            public ushort NumberOutputDataIndices;
            public ushort NumberFeatureButtonCaps;
            public ushort NumberFeatureValueCaps;
            public ushort NumberFeatureDataIndices;
        }

        [DllImport("hid.dll", SetLastError = true)]
        public static extern void HidD_GetHidGuid(out Guid hidGuid);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_GetAttributes(IntPtr hidDeviceObject, ref HIDD_ATTRIBUTES attributes);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_GetSerialNumberString(SafeFileHandle hidDeviceObject, byte[] buffer, uint bufferLength);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_GetPreparsedData(SafeFileHandle hidDeviceObject, out IntPtr preparsedData);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern int HidP_GetCaps(IntPtr preparsedData, ref HIDP_CAPS capabilities);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_SetOutputReport(SafeFileHandle hidDeviceObject, byte[] reportBuffer, uint reportBufferLength);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_GetInputReport(SafeFileHandle hidDeviceObject, byte[] reportBuffer, uint reportBufferLength);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, string enumerator, IntPtr hwndParent, int flags);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet, IntPtr deviceInfoData, ref Guid interfaceClassGuid, int memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, IntPtr deviceInterfaceDetailData, int deviceInterfaceDetailDataSize, ref int requiredSize, IntPtr deviceInfoData);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, ref SP_DEVICE_INTERFACE_DETAIL_DATA deviceInterfaceDetailData, int deviceInterfaceDetailDataSize, ref int requiredSize, IntPtr deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern int SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        /// <summary>Open a HID interface for read / write with sharing, as HIDLibrary did.</summary>
        public static SafeFileHandle OpenPath(string path)
        {
            try
            {
                return CreateFile(path, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE,
                    IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_OVERLAPPED, IntPtr.Zero);
            }
            catch
            {
                return null;
            }
        }
    }
}
