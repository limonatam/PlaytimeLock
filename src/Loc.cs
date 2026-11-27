using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace PlaytimeLock
{
    /// <summary>
    /// Small built-in localization (English + Turkish). Falls back to English.
    /// To add a language, add a dictionary below and a check in Init().
    /// </summary>
    internal static class Loc
    {
        // Assigned in Init(); null-checked in Get() because static initializers run in order.
        private static Dictionary<string, string> current;

        public static void Init(IPlayniteAPI api)
        {
            string lang = null;
            try
            {
                // Read via reflection so the plugin also loads on SDK versions without this property.
                var settings = api.ApplicationSettings;
                var prop = settings?.GetType().GetProperty("Language");
                lang = prop?.GetValue(settings) as string;
            }
            catch { }
            if (string.IsNullOrEmpty(lang) || lang.Equals("english", StringComparison.OrdinalIgnoreCase))
            {
                lang = CultureInfo.CurrentUICulture.Name;
            }

            current = lang != null && lang.StartsWith("tr", StringComparison.OrdinalIgnoreCase) ? Tr : En;
        }

        public static string Get(string key)
        {
            string value;
            if (current != null && current.TryGetValue(key, out value)) return value;
            if (En.TryGetValue(key, out value)) return value;
            return key;
        }

        public static string Format(string key, params object[] args)
        {
            return string.Format(CultureInfo.CurrentCulture, Get(key), args);
        }

        public static string Playtime(ulong seconds)
        {
            var hours = seconds / 3600;
            var minutes = (seconds % 3600) / 60;
            return Format("PlaytimeFormat", hours, minutes);
        }

        private static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
            ["PlaytimeFormat"] = "{0} h {1} min",
            ["LockSection"] = "PlaytimeLock",
            ["MenuLock"] = "Lock playtime...",
            ["MenuUnlock"] = "Unlock playtime",
            ["MenuListLocks"] = "List locked games",
            ["LockPrompt"] = "Enter the total playtime to lock for {0}.\n\nExamples:  12  (12 hours)  •  12.5  •  12:30  •  45m",
            ["ParseError"] = "Could not understand '{0}'. Examples: 12:30 or 12.5",
            ["NoLocks"] = "No locked games yet.\n\nRight-click a game → Lock playtime...",
            ["LockList"] = "{0} locked games:\n\n{1}",
            ["LockTagName"] = "Playtime Locked",
            ["SettingsLockHeader"] = "PlaytimeLock",
            ["SettingsAddTag"] = "Add a \"Playtime Locked\" tag to locked games",
            ["SettingsLockHelp"] = "Right-click a game → Lock playtime... and enter the playtime to keep.\nLocked games never lose playtime. Time tracked by Playnite and higher values from store sync are still accepted. To change a locked playtime, unlock it and lock it again with the new value.",
        };

        private static readonly Dictionary<string, string> Tr = new Dictionary<string, string>
        {
            ["PlaytimeFormat"] = "{0} sa {1} dk",
            ["LockSection"] = "PlaytimeLock",
            ["MenuLock"] = "Oynama süresini kilitle...",
            ["MenuUnlock"] = "Süre kilidini kaldır",
            ["MenuListLocks"] = "Kilitli oyunları listele",
            ["LockPrompt"] = "{0} için kilitlenecek toplam oynama süresini gir.\n\nÖrnekler:  12  (12 saat)  •  12,5  •  12:30  •  45dk",
            ["ParseError"] = "'{0}' anlaşılamadı. Örnek: 12:30 veya 12,5",
            ["NoLocks"] = "Henüz kilitli oyun yok.\n\nBir oyuna sağ tıkla → Oynama süresini kilitle...",
            ["LockList"] = "{0} kilitli oyun:\n\n{1}",
            ["LockTagName"] = "Playtime Locked",
            ["SettingsLockHeader"] = "PlaytimeLock",
            ["SettingsAddTag"] = "Kilitli oyunlara \"Playtime Locked\" etiketi ekle",
            ["SettingsLockHelp"] = "Bir oyuna sağ tıkla → Oynama süresini kilitle... ve korunacak süreyi gir.\nKilitli oyunların süresi asla azalmaz. Playnite'ın saydığı süre ve mağazadan gelen daha yüksek değerler yine kabul edilir. Kilitli süreyi değiştirmek için kilidi kaldırıp yeni değerle tekrar kilitle.",
        };
    }
}
