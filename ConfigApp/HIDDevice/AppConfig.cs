using System;
using System.IO;
using System.Windows.Forms;

namespace ConfigAppNS
{
    /// <summary>
    /// Minimal key=value access to Config.ini ([APP Config] section). Used for the settings the
    /// user can change from the UI (LANGUAGE, THEME) so they survive a restart.
    /// </summary>
    internal static class AppConfig
    {
        public static string Path
        {
            get
            {
                string p = System.IO.Path.Combine(Application.StartupPath, "Config.ini");
                if (File.Exists(p)) return p;
                string cwd = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Config.ini");
                return File.Exists(cwd) ? cwd : p;
            }
        }

        public static string Read(string key)
        {
            try
            {
                string path = Path;
                if (!File.Exists(path)) return null;
                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Split(';')[0].Trim();
                    if (line.StartsWith("[")) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    if (string.Equals(line.Substring(0, eq).Trim(), key, StringComparison.OrdinalIgnoreCase))
                        return line.Substring(eq + 1).Trim();
                }
            }
            catch { }
            return null;
        }

        /// <summary>Replace (or append inside [APP Config]) KEY=value. Silently ignored if the file is missing.</summary>
        public static void Write(string key, string value)
        {
            try
            {
                string path = Path;
                if (!File.Exists(path)) return;
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].TrimStart().StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
                    {
                        lines[i] = key + "=" + value;
                        File.WriteAllLines(path, lines);
                        return;
                    }
                }
                // Insert right after the [APP Config] header so the legacy line-limited reader still sees it
                int insertAt = lines.Length;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Trim() == "[APP Config]") { insertAt = i + 1; break; }
                }
                string[] outLines = new string[lines.Length + 1];
                Array.Copy(lines, 0, outLines, 0, insertAt);
                outLines[insertAt] = key + "=" + value;
                Array.Copy(lines, insertAt, outLines, insertAt + 1, lines.Length - insertAt);
                File.WriteAllLines(path, outLines);
            }
            catch { }
        }
    }
}
