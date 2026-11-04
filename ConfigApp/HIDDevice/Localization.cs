using System;
using System.Globalization;
using System.Resources;
using System.Threading;

namespace ConfigAppNS
{
    /// <summary>
    /// UI string lookup backed by Strings.resx (English, neutral) and Strings.zh-CN.resx.
    /// The active culture is chosen explicitly so every Chinese variant (zh-CN / zh-Hans /
    /// zh-TW / zh-HK ...) resolves to Simplified Chinese instead of falling back to English.
    /// </summary>
    internal static class L
    {
        public const string SettingAuto = "AUTO";
        public const string SettingChinese = "CN";
        public const string SettingEnglish = "EN";

        private static readonly ResourceManager rm =
            new ResourceManager("ConfigAppNS.Strings", typeof(L).Assembly);

        private static CultureInfo culture = ResolveCulture(SettingAuto);
        private static string setting = SettingAuto;

        public static event EventHandler LanguageChanged;

        /// <summary>Current setting: AUTO / CN / EN (what is persisted in Config.ini).</summary>
        public static string Setting { get { return setting; } }

        public static CultureInfo Culture { get { return culture; } }

        public static bool IsChinese
        {
            get { return culture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase); }
        }

        public static string NormalizeSetting(string value)
        {
            if (string.IsNullOrEmpty(value)) return SettingAuto;
            string v = value.Trim().ToUpperInvariant();
            if (v == SettingChinese || v == "ZH" || v == "ZH-CN") return SettingChinese;
            if (v == SettingEnglish) return SettingEnglish;
            return SettingAuto;
        }

        public static void Apply(string newSetting)
        {
            setting = NormalizeSetting(newSetting);
            culture = ResolveCulture(setting);
            Thread.CurrentThread.CurrentUICulture = culture;
            EventHandler h = LanguageChanged;
            if (h != null) h(null, EventArgs.Empty);
        }

        private static CultureInfo ResolveCulture(string s)
        {
            if (s == SettingChinese) return CultureInfo.GetCultureInfo("zh-CN");
            if (s == SettingEnglish) return CultureInfo.GetCultureInfo("en");
            CultureInfo sys = CultureInfo.InstalledUICulture;
            try { sys = CultureInfo.CurrentUICulture; } catch { }
            bool zh = sys != null && sys.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase);
            return CultureInfo.GetCultureInfo(zh ? "zh-CN" : "en");
        }

        public static string Get(string key)
        {
            try
            {
                string s = rm.GetString(key, culture);
                return s ?? key;
            }
            catch
            {
                return key;
            }
        }

        public static string F(string key, params object[] args)
        {
            try { return string.Format(culture, Get(key), args); }
            catch { return Get(key); }
        }
    }
}
