                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               using System.Windows;
using System.Windows.Media;

namespace MyCraftyStash.Services
{
    public static class ThemeService
    {
        public static bool IsDarkMode { get; private set; } = false;
        
        public static event Action? ThemeChanged;
        
        public static void ToggleTheme()
        {
            IsDarkMode = !IsDarkMode;
            ApplyTheme();
            ThemeChanged?.Invoke();
        }
        
        public static void SetTheme(bool darkMode)
        {
            IsDarkMode = darkMode;
            ApplyTheme();
            ThemeChanged?.Invoke();
        }
        
        private static void ApplyTheme()
        {
            var resources = Application.Current.Resources;
            
            if (IsDarkMode)
            {
                // Crafty Stash dark — warm near-black with crimson accent
                resources["PrimaryBrush"] = new SolidColorBrush(Color.FromRgb(208, 64, 70));
                resources["PrimaryLightBrush"] = new SolidColorBrush(Color.FromRgb(225, 92, 98));
                resources["PrimarySoftBrush"] = new SolidColorBrush(Color.FromRgb(60, 26, 28));
                resources["DangerBrush"] = new SolidColorBrush(Color.FromRgb(208, 64, 70));
                resources["DangerSoftBrush"] = new SolidColorBrush(Color.FromRgb(60, 26, 28));
                resources["SuccessBrush"] = new SolidColorBrush(Color.FromRgb(86, 168, 122));
                resources["SuccessSoftBrush"] = new SolidColorBrush(Color.FromRgb(28, 48, 38));
                resources["WarnBrush"] = new SolidColorBrush(Color.FromRgb(214, 142, 70));
                resources["WarnSoftBrush"] = new SolidColorBrush(Color.FromRgb(58, 44, 28));
                resources["AccentBrush"] = new SolidColorBrush(Color.FromRgb(208, 64, 70));
                resources["OverlayDimBrush"] = new SolidColorBrush(Color.FromArgb(0x80, 0, 0, 0));
                resources["OverlayHeavyBrush"] = new SolidColorBrush(Color.FromArgb(0xCC, 0, 0, 0));
                resources["OverlayTextBrush"] = new SolidColorBrush(Color.FromArgb(0xCC, 255, 255, 255));
                resources["CropGuidePrimaryBrush"] = new SolidColorBrush(Color.FromRgb(208, 64, 70));
                resources["CropGuidePrimaryFillBrush"] = new SolidColorBrush(Color.FromArgb(0x30, 208, 64, 70));
                resources["CropGuideSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(170, 100, 240));
                resources["CropGuideHotBrush"] = new SolidColorBrush(Color.FromRgb(255, 80, 80));
                resources["CropGuideHotSoftBrush"] = new SolidColorBrush(Color.FromArgb(0xCC, 80, 26, 26));
                resources["SentimentSelectionBrush"] = new SolidColorBrush(Color.FromArgb(0x22, 50, 180, 220));
                resources["SentimentTooltipBrush"] = new SolidColorBrush(Color.FromArgb(0xDD, 30, 26, 26));
                resources["BackgroundBrush"] = new SolidColorBrush(Color.FromRgb(20, 18, 18));
                resources["SurfaceBrush"] = new SolidColorBrush(Color.FromRgb(28, 26, 26));
                resources["CardBrush"] = new SolidColorBrush(Color.FromRgb(28, 26, 26));
                resources["BorderBrush"] = new SolidColorBrush(Color.FromRgb(54, 49, 47));
                resources["BorderSoftBrush"] = new SolidColorBrush(Color.FromRgb(40, 36, 35));
                resources["TextBrush"] = new SolidColorBrush(Color.FromRgb(238, 232, 220));
                resources["TextMutedBrush"] = new SolidColorBrush(Color.FromRgb(150, 142, 130));
                resources["SidebarBrush"] = new SolidColorBrush(Color.FromRgb(24, 22, 22));
                resources["SidebarHoverBrush"] = new SolidColorBrush(Color.FromRgb(40, 36, 35));
                resources["SidebarSelectedBrush"] = new SolidColorBrush(Color.FromRgb(48, 42, 41));
                resources["TitleBarBrush"] = new SolidColorBrush(Color.FromRgb(15, 13, 13));
                resources["TitleBarTextBrush"] = new SolidColorBrush(Color.FromRgb(238, 232, 220));
                resources["TabActiveBrush"] = new SolidColorBrush(Color.FromRgb(20, 18, 18));
                resources["MultiPickerBrush"] = new SolidColorBrush(Color.FromRgb(28, 26, 26));
                resources["InputBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(36, 32, 31));
                resources["BadgeBrush"] = new SolidColorBrush(Color.FromRgb(54, 49, 47));
            }
            else
            {
                // Crafty Stash light — warm cream with deep crimson accent
                resources["PrimaryBrush"] = new SolidColorBrush(Color.FromRgb(168, 44, 50));
                resources["PrimaryLightBrush"] = new SolidColorBrush(Color.FromRgb(192, 58, 65));
                resources["PrimarySoftBrush"] = new SolidColorBrush(Color.FromRgb(248, 224, 225));
                resources["DangerBrush"] = new SolidColorBrush(Color.FromRgb(168, 44, 50));
                resources["DangerSoftBrush"] = new SolidColorBrush(Color.FromRgb(248, 224, 225));
                resources["SuccessBrush"] = new SolidColorBrush(Color.FromRgb(63, 143, 98));
                resources["SuccessSoftBrush"] = new SolidColorBrush(Color.FromRgb(226, 239, 230));
                resources["WarnBrush"] = new SolidColorBrush(Color.FromRgb(199, 122, 42));
                resources["WarnSoftBrush"] = new SolidColorBrush(Color.FromRgb(244, 229, 208));
                resources["AccentBrush"] = new SolidColorBrush(Color.FromRgb(168, 44, 50));
                resources["OverlayDimBrush"] = new SolidColorBrush(Color.FromArgb(0x80, 0, 0, 0));
                resources["OverlayHeavyBrush"] = new SolidColorBrush(Color.FromArgb(0xCC, 0, 0, 0));
                resources["OverlayTextBrush"] = new SolidColorBrush(Color.FromArgb(0xCC, 255, 255, 255));
                resources["CropGuidePrimaryBrush"] = new SolidColorBrush(Color.FromRgb(168, 44, 50));
                resources["CropGuidePrimaryFillBrush"] = new SolidColorBrush(Color.FromArgb(0x30, 168, 44, 50));
                resources["CropGuideSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(147, 51, 234));
                resources["CropGuideHotBrush"] = new SolidColorBrush(Color.FromRgb(255, 0, 0));
                resources["CropGuideHotSoftBrush"] = new SolidColorBrush(Color.FromArgb(0xCC, 26, 26, 26));
                resources["SentimentSelectionBrush"] = new SolidColorBrush(Color.FromArgb(0x22, 0, 153, 204));
                resources["SentimentTooltipBrush"] = new SolidColorBrush(Color.FromArgb(0xDD, 255, 255, 255));
                resources["BackgroundBrush"] = new SolidColorBrush(Color.FromRgb(242, 238, 230));
                resources["SurfaceBrush"] = new SolidColorBrush(Colors.White);
                resources["CardBrush"] = new SolidColorBrush(Colors.White);
                resources["BorderBrush"] = new SolidColorBrush(Color.FromRgb(225, 220, 208));
                resources["BorderSoftBrush"] = new SolidColorBrush(Color.FromRgb(236, 231, 219));
                resources["TextBrush"] = new SolidColorBrush(Color.FromRgb(30, 27, 23));
                resources["TextMutedBrush"] = new SolidColorBrush(Color.FromRgb(122, 114, 100));
                resources["SidebarBrush"] = new SolidColorBrush(Color.FromRgb(235, 230, 217));
                resources["SidebarHoverBrush"] = new SolidColorBrush(Color.FromRgb(224, 218, 201));
                resources["SidebarSelectedBrush"] = new SolidColorBrush(Colors.White);
                resources["TitleBarBrush"] = new SolidColorBrush(Color.FromRgb(26, 23, 21));
                resources["TitleBarTextBrush"] = new SolidColorBrush(Color.FromRgb(242, 238, 230));
                resources["TabActiveBrush"] = new SolidColorBrush(Color.FromRgb(242, 238, 230));
                resources["MultiPickerBrush"] = new SolidColorBrush(Colors.White);
                resources["InputBackgroundBrush"] = new SolidColorBrush(Colors.White);
                resources["BadgeBrush"] = new SolidColorBrush(Color.FromRgb(224, 218, 201));
            }

            ApplyUserColorOverrides(resources);
        }

        // Overlay any per-user custom color choices from UserSettingsService on top of
        // the base theme. Keeps personalization additive — anything the user hasn't set
        // falls back to the standard light/dark palette.
        private static void ApplyUserColorOverrides(ResourceDictionary resources)
        {
            try
            {
                var overrides = UserSettingsService.Current?.CustomColors;
                if (overrides == null || overrides.Count == 0) return;

                foreach (var kvp in overrides)
                {
                    if (string.IsNullOrWhiteSpace(kvp.Value)) continue;
                    if (TryParseHex(kvp.Value, out var color))
                        resources[kvp.Key] = new SolidColorBrush(color);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "ThemeService.ApplyUserColorOverrides");
            }
        }

        public static bool TryParseHex(string hex, out Color color)
        {
            color = Colors.Transparent;
            if (string.IsNullOrWhiteSpace(hex)) return false;
            try
            {
                var trimmed = hex.Trim();
                if (!trimmed.StartsWith("#")) trimmed = "#" + trimmed;
                var obj = ColorConverter.ConvertFromString(trimmed);
                if (obj is Color c) { color = c; return true; }
            }
            catch { /* TryParse pattern: malformed hex → return false */ }
            return false;
        }
    }
}
