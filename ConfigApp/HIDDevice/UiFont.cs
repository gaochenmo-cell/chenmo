using System;
using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;

namespace ConfigAppNS
{
    /// <summary>
    /// Single source of truth for UI typography. Family fallback chain:
    /// Microsoft YaHei UI → Microsoft YaHei → system default. Sizes are in points.
    /// Apply fonts here (and from the theme pass) instead of hard-coding them per control.
    /// </summary>
    internal static class UiFont
    {
        public const float Body = 10.5f;      // labels, buttons, radio / combo / text boxes
        public const float Tab = 11f;         // tab captions
        public const float GroupTitle = 11f;  // group-box card titles
        public const float Status = 10f;      // top-left device version / status line
        public const float Value = 12f;       // important numeric values (sensitivity, debounce)
        public const float BarValue = 9.5f;   // live RAW value drawn inside the narrow bars

        private static readonly string[] Preferred = new string[] { "Microsoft YaHei UI", "Microsoft YaHei" };
        private static string family;

        public static string Family
        {
            get
            {
                if (family == null)
                    family = Resolve();
                return family;
            }
        }

        private static string Resolve()
        {
            foreach (string name in Preferred)
            {
                if (IsInstalled(name))
                    return name;
            }
            try { return SystemFonts.DefaultFont.FontFamily.Name; }
            catch { return FontFamily.GenericSansSerif.Name; }
        }

        private static bool IsInstalled(string name)
        {
            try
            {
                using (InstalledFontCollection installed = new InstalledFontCollection())
                {
                    foreach (FontFamily ff in installed.Families)
                    {
                        if (string.Equals(ff.Name, name, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }
            catch { }
            return false;
        }

        public static Font Get(float size)
        {
            return Get(size, FontStyle.Regular);
        }

        public static Font Get(float size, FontStyle style)
        {
            try { return new Font(Family, size, style, GraphicsUnit.Point); }
            catch { return new Font(FontFamily.GenericSansSerif, size, style, GraphicsUnit.Point); }
        }

        /// <summary>Assign a font only when it differs, so repeated theme passes do not churn handles.</summary>
        public static void Set(Control c, float size, FontStyle style)
        {
            if (c == null)
                return;
            Font cur = c.Font;
            if (cur != null && cur.Name == Family && Math.Abs(cur.SizeInPoints - size) < 0.01f && cur.Style == style)
                return;
            c.Font = Get(size, style);
        }

        public static void Set(Control c, float size)
        {
            Set(c, size, FontStyle.Regular);
        }

        /// <summary>Re-family every control in a tree, keeping each control's own size and style
        /// (used by fixed-layout dialogs whose designer sizes are already tuned).</summary>
        public static void Refamily(Control root, float minSize)
        {
            if (root == null)
                return;
            Font cur = root.Font;
            float size = cur != null ? Math.Max(minSize, cur.SizeInPoints) : minSize;
            FontStyle style = cur != null ? cur.Style : FontStyle.Regular;
            Set(root, size, style);
            foreach (Control c in root.Controls)
                Refamily(c, minSize);
        }
    }
}
