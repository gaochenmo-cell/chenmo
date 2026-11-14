using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace ConfigAppNS
{
    /// <summary>
    /// Diagnostic log written next to the exe (YuanconDancepad.log). Enabled by default; Config.ini
    /// DEBUGLOG=0 turns it off. Every USB transaction (bytes sent, raw reply, verdict) is
    /// recorded so a failure on a real board can be diagnosed from the file alone.
    /// Logging must never break the UI: every public method swallows its own exceptions.
    /// </summary>
    internal static class DebugLog
    {
        private const long MaxBytes = 2 * 1024 * 1024;
        private static readonly object sync = new object();
        private static bool? enabled;
        private static string path;

        public static bool Enabled
        {
            get
            {
                if (enabled == null)
                {
                    string v = AppConfig.Read("DEBUGLOG");
                    enabled = !IsOff(v);
                }
                return enabled.Value;
            }
        }

        public static string Path
        {
            get
            {
                if (path == null)
                {
                    try { path = System.IO.Path.Combine(Application.StartupPath, "YuanconDancepad.log"); }
                    catch { path = "YuanconDancepad.log"; }
                }
                return path;
            }
        }

        private static bool IsOff(string v)
        {
            if (v == null) return false;
            v = v.Trim().ToUpperInvariant();
            return v == "0" || v == "NO" || v == "OFF" || v == "FALSE";
        }

        public static void Log(string message)
        {
            if (!Enabled) return;
            try
            {
                lock (sync)
                {
                    Rotate();
                    File.AppendAllText(Path,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + message + "\r\n",
                        Encoding.UTF8);
                }
            }
            catch { }
        }

        public static void Log(string format, params object[] args)
        {
            if (!Enabled) return;
            try { Log(string.Format(format, args)); }
            catch { }
        }

        /// <summary>"prefix: 47 80 A2 13 02 11 ..." — count bytes from buf starting at start.</summary>
        public static void LogHex(string prefix, byte[] buf, int start, int count)
        {
            if (!Enabled) return;
            try { Log(prefix + ": " + Hex(buf, start, count)); }
            catch { }
        }

        public static string Hex(byte[] buf, int start, int count)
        {
            if (buf == null) return "(null)";
            StringBuilder sb = new StringBuilder(count * 3);
            for (int i = 0; i < count && start + i < buf.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(buf[start + i].ToString("X2"));
            }
            return sb.ToString();
        }

        private static void Rotate()
        {
            try
            {
                FileInfo fi = new FileInfo(Path);
                if (fi.Exists && fi.Length > MaxBytes)
                {
                    string old = Path + ".1";
                    if (File.Exists(old)) File.Delete(old);
                    File.Move(Path, old);
                }
            }
            catch { }
        }
    }
}
