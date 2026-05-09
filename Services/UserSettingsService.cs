using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using MyCraftyStash.Data;
using MyCraftyStash.Models;

namespace MyCraftyStash.Services
{
    public enum CardSizeOption { Small, Medium, Large }
    public enum TextSizeOption { Small, Medium, Large }

    public class TypeSortOrder
    {
        public string Sort1 { get; set; } = "Color";
        public string Sort2 { get; set; } = "";
        public string Sort3 { get; set; } = "";
    }

    /// <summary>
    /// In-memory snapshot of user prefs. Backed by settings.db (kv_settings,
    /// type_sort_orders, custom_colors). Subtypes go through ConfigStore.
    /// </summary>
    public class UserSettings
    {
        public string CardSize { get; set; } = "Medium";
        public string TextSize { get; set; } = "Medium";
        public bool DarkMode { get; set; } = false;
        public string DefaultItemType { get; set; } = "";
        public string DefaultLocation { get; set; } = "";
        public string DefaultSortOrder { get; set; } = "Type";
        public Dictionary<string, TypeSortOrder> TypeSortOrders { get; set; } = new();
        public Dictionary<string, string> CustomColors { get; set; } = new();
    }

    public static class UserSettingsService
    {
        private static UserSettings _current = new();
        public static UserSettings Current => _current;

        public static event Action? SettingsChanged;

        private static SettingsDbContext CreateContext() => new SettingsDbContext();

        // ── Bootstrap + load ─────────────────────────────────────────────────

        public static void Load()
        {
            try
            {
                using (var ctx = CreateContext())
                {
                    ctx.Database.Migrate();
                    MigrateLegacyJsonIfPresent(ctx);
                    _current = ReadFromDb(ctx);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "UserSettingsService.Load - falling back to defaults");
                _current = new UserSettings();
            }

            // Always boot in light mode regardless of saved preference; user can toggle in Settings.
            _current.DarkMode = false;
            ThemeService.SetTheme(false);
            ApplyCardSize();
            ApplyTextSize();
        }

        private static UserSettings ReadFromDb(SettingsDbContext ctx)
        {
            var s = new UserSettings();
            foreach (var kv in ctx.KvSettings.AsNoTracking())
            {
                switch (kv.Key)
                {
                    case nameof(UserSettings.CardSize):         s.CardSize = kv.Value ?? "Medium"; break;
                    case nameof(UserSettings.TextSize):         s.TextSize = kv.Value ?? "Medium"; break;
                    case nameof(UserSettings.DarkMode):         s.DarkMode = kv.Value == "true"; break;
                    case nameof(UserSettings.DefaultItemType):  s.DefaultItemType = kv.Value ?? ""; break;
                    case nameof(UserSettings.DefaultLocation):  s.DefaultLocation = kv.Value ?? ""; break;
                    case nameof(UserSettings.DefaultSortOrder): s.DefaultSortOrder = kv.Value ?? "Type"; break;
                }
            }
            foreach (var t in ctx.TypeSortOrders.AsNoTracking())
            {
                s.TypeSortOrders[t.Type] = new TypeSortOrder { Sort1 = t.Sort1, Sort2 = t.Sort2, Sort3 = t.Sort3 };
            }
            foreach (var c in ctx.CustomColors.AsNoTracking())
            {
                s.CustomColors[c.BrushKey] = c.Hex;
            }
            return s;
        }

        private static void UpsertKv(SettingsDbContext ctx, string key, string? value)
        {
            var row = ctx.KvSettings.FirstOrDefault(k => k.Key == key);
            if (row == null) ctx.KvSettings.Add(new KvSetting { Key = key, Value = value });
            else row.Value = value;
        }

        private static void SaveScalar(string key, string? value)
        {
            try
            {
                using var ctx = CreateContext();
                UpsertKv(ctx, key, value);
                ctx.SaveChanges();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"UserSettingsService.SaveScalar({key})");
            }
        }

        // ── One-shot migration from legacy JSON ──────────────────────────────

        private static void MigrateLegacyJsonIfPresent(SettingsDbContext ctx)
        {
            try
            {
                var legacyPath = LegacyJsonPath();
                if (string.IsNullOrEmpty(legacyPath) || !File.Exists(legacyPath)) return;

                // Don't overwrite if the DB has already been populated.
                if (ctx.KvSettings.Any()) return;

                var json = File.ReadAllText(legacyPath);
                var legacy = JsonSerializer.Deserialize<UserSettings>(json);
                if (legacy == null) return;

                UpsertKv(ctx, nameof(UserSettings.CardSize),         legacy.CardSize);
                UpsertKv(ctx, nameof(UserSettings.TextSize),         legacy.TextSize);
                UpsertKv(ctx, nameof(UserSettings.DarkMode),         legacy.DarkMode ? "true" : "false");
                UpsertKv(ctx, nameof(UserSettings.DefaultItemType),  legacy.DefaultItemType);
                UpsertKv(ctx, nameof(UserSettings.DefaultLocation),  legacy.DefaultLocation);
                UpsertKv(ctx, nameof(UserSettings.DefaultSortOrder), legacy.DefaultSortOrder);

                foreach (var (type, order) in legacy.TypeSortOrders)
                {
                    ctx.TypeSortOrders.Add(new TypeSortOrderEntry
                    {
                        Type = type, Sort1 = order.Sort1, Sort2 = order.Sort2, Sort3 = order.Sort3
                    });
                }
                foreach (var (brush, hex) in legacy.CustomColors)
                {
                    ctx.CustomColors.Add(new CustomColor { BrushKey = brush, Hex = hex });
                }
                ctx.SaveChanges();

                try { File.Move(legacyPath, legacyPath + ".migrated", overwrite: true); } catch { }
                LoggingService.LogInfo($"Migrated legacy settings JSON: {legacyPath}");
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "UserSettingsService.MigrateLegacyJsonIfPresent");
            }
        }

        private static string LegacyJsonPath()
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(appData, "MyCraftyStash", $"settings_{Environment.UserName}.json");
            }
            catch
            {
                return string.Empty;
            }
        }

        // ── Read accessors used by the rest of the app ───────────────────────

        public static CardSizeOption GetCardSize() => _current.CardSize switch
        {
            "Small" => CardSizeOption.Small,
            "Large" => CardSizeOption.Large,
            _       => CardSizeOption.Medium,
        };

        public static TextSizeOption GetTextSize() => _current.TextSize switch
        {
            "Small" => TextSizeOption.Small,
            "Large" => TextSizeOption.Large,
            _       => TextSizeOption.Medium,
        };

        // ── Write accessors ──────────────────────────────────────────────────

        public static void SetCardSize(CardSizeOption size)
        {
            _current.CardSize = size.ToString();
            SaveScalar(nameof(UserSettings.CardSize), _current.CardSize);
            ApplyCardSize();
            ApplyTextSize();
            SettingsChanged?.Invoke();
        }

        public static void SetTextSize(TextSizeOption size)
        {
            _current.TextSize = size.ToString();
            SaveScalar(nameof(UserSettings.TextSize), _current.TextSize);
            ApplyTextSize();
            SettingsChanged?.Invoke();
        }

        public static void SetDarkMode(bool dark)
        {
            _current.DarkMode = dark;
            SaveScalar(nameof(UserSettings.DarkMode), dark ? "true" : "false");
            ThemeService.SetTheme(dark);
            SettingsChanged?.Invoke();
        }

        public static readonly string[] CustomizableBrushKeys = new[]
        {
            "PrimaryBrush",
            "PrimaryLightBrush",
            "AccentBrush",
            "BackgroundBrush",
            "SidebarBrush",
            "SidebarSelectedBrush",
            "TitleBarBrush",
        };

        public static string? GetCustomColor(string brushKey)
            => _current.CustomColors.TryGetValue(brushKey, out var hex) ? hex : null;

        public static void SetCustomColor(string brushKey, string? hex)
        {
            try
            {
                using var ctx = CreateContext();
                if (string.IsNullOrWhiteSpace(hex))
                {
                    var existing = ctx.CustomColors.FirstOrDefault(c => c.BrushKey == brushKey);
                    if (existing != null) ctx.CustomColors.Remove(existing);
                    _current.CustomColors.Remove(brushKey);
                }
                else
                {
                    var trimmed = hex.Trim();
                    var existing = ctx.CustomColors.FirstOrDefault(c => c.BrushKey == brushKey);
                    if (existing == null) ctx.CustomColors.Add(new CustomColor { BrushKey = brushKey, Hex = trimmed });
                    else existing.Hex = trimmed;
                    _current.CustomColors[brushKey] = trimmed;
                }
                ctx.SaveChanges();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"UserSettingsService.SetCustomColor({brushKey})");
            }

            ThemeService.SetTheme(_current.DarkMode);
            SettingsChanged?.Invoke();
        }

        public static void ResetCustomColors()
        {
            try
            {
                using var ctx = CreateContext();
                ctx.CustomColors.RemoveRange(ctx.CustomColors);
                ctx.SaveChanges();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "UserSettingsService.ResetCustomColors");
            }
            _current.CustomColors.Clear();
            ThemeService.SetTheme(_current.DarkMode);
            SettingsChanged?.Invoke();
        }

        public static void SetDefaultItemType(string type)
        {
            _current.DefaultItemType = type;
            SaveScalar(nameof(UserSettings.DefaultItemType), type);
        }

        public static void SetDefaultLocation(string location)
        {
            _current.DefaultLocation = location;
            SaveScalar(nameof(UserSettings.DefaultLocation), location);
        }

        public static void SetDefaultSortOrder(string sortOrder)
        {
            _current.DefaultSortOrder = sortOrder;
            SaveScalar(nameof(UserSettings.DefaultSortOrder), sortOrder);
            SettingsChanged?.Invoke();
        }

        public static TypeSortOrder GetSortOrderForType(string type)
            => _current.TypeSortOrders.TryGetValue(type, out var order) ? order : new TypeSortOrder();

        public static void SetSortOrderForType(string type, TypeSortOrder order)
        {
            _current.TypeSortOrders[type] = order;
            try
            {
                using var ctx = CreateContext();
                var existing = ctx.TypeSortOrders.FirstOrDefault(t => t.Type == type);
                if (existing == null)
                {
                    ctx.TypeSortOrders.Add(new TypeSortOrderEntry
                    {
                        Type = type, Sort1 = order.Sort1, Sort2 = order.Sort2, Sort3 = order.Sort3
                    });
                }
                else
                {
                    existing.Sort1 = order.Sort1;
                    existing.Sort2 = order.Sort2;
                    existing.Sort3 = order.Sort3;
                }
                ctx.SaveChanges();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"UserSettingsService.SetSortOrderForType({type})");
            }
        }

        // ── Generic kv accessors for misc app-wide flags ─────────────────────
        // Used by features that don't fit the typed UserSettings shape (e.g.
        // per-catalog-provider enable flags). Reads and writes go straight to
        // settings.db.kv_settings without touching the in-memory _current.

        public static string? GetSettingValue(string key)
        {
            try
            {
                using var ctx = CreateContext();
                return ctx.KvSettings.AsNoTracking()
                    .FirstOrDefault(k => k.Key == key)?.Value;
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"UserSettingsService.GetSettingValue({key})");
                return null;
            }
        }

        public static void SetSettingValue(string key, string? value)
        {
            try
            {
                using var ctx = CreateContext();
                UpsertKv(ctx, key, value);
                ctx.SaveChanges();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"UserSettingsService.SetSettingValue({key})");
            }
        }

        // ── Subtypes (lives in ConfigStore so it shares the same DB) ─────────

        private static Dictionary<string, List<string>>? _subtypes;

        private static Dictionary<string, List<string>> LoadSubtypes()
        {
            try
            {
                var json = ConfigStore.GetText(ConfigStore.Subtypes);
                if (string.IsNullOrWhiteSpace(json)) return new();
                return JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json)
                       ?? new Dictionary<string, List<string>>();
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "UserSettingsService.LoadSubtypes");
                return new Dictionary<string, List<string>>();
            }
        }

        private static void EnsureSubtypesLoaded()
        {
            if (_subtypes == null) _subtypes = LoadSubtypes();
        }

        public static void ReloadSubtypes()
        {
            _subtypes = LoadSubtypes();
        }

        public static void SetTypeSubtypes(string type, List<string> subtypes)
        {
            EnsureSubtypesLoaded();
            if (subtypes == null || subtypes.Count == 0)
                _subtypes!.Remove(type);
            else
                _subtypes![type] = subtypes;

            try
            {
                var json = JsonSerializer.Serialize(_subtypes, new JsonSerializerOptions { WriteIndented = true });
                ConfigStore.SetText(ConfigStore.Subtypes, json);
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "UserSettingsService.SetTypeSubtypes - Failed to save");
                throw;
            }
        }

        public static List<string> GetSubtypesForType(string type)
        {
            EnsureSubtypesLoaded();
            return _subtypes!.TryGetValue(type, out var list) ? list : new List<string>();
        }

        // ── XAML resource binding (unchanged) ────────────────────────────────

        public static void ApplyCardSize()
        {
            var resources = Application.Current.Resources;
            var size = GetCardSize();

            switch (size)
            {
                case CardSizeOption.Small:
                    resources["CardItemWidth"] = 180.0;
                    resources["CardItemHeight"] = 260.0;
                    resources["CardBorderWidth"] = 164.0;
                    resources["CardImageHeight"] = 100.0;
                    break;
                case CardSizeOption.Large:
                    resources["CardItemWidth"] = 296.0;
                    resources["CardItemHeight"] = 400.0;
                    resources["CardBorderWidth"] = 280.0;
                    resources["CardImageHeight"] = 200.0;
                    break;
                default:
                    resources["CardItemWidth"] = 236.0;
                    resources["CardItemHeight"] = 320.0;
                    resources["CardBorderWidth"] = 220.0;
                    resources["CardImageHeight"] = 140.0;
                    break;
            }
        }

        public static void ApplyTextSize()
        {
            var resources = Application.Current.Resources;
            var size = GetTextSize();

            switch (size)
            {
                case TextSizeOption.Small:
                    resources["CardNameFontSize"] = 12.0;
                    resources["CardTypeFontSize"] = 10.0;
                    resources["CardBadgeFontSize"] = 9.0;
                    resources["CardNameMaxHeight"] = 48.0;
                    resources["ListFontSize"] = 12.0;
                    resources["DetailTitleFontSize"] = 20.0;
                    resources["DetailLabelFontSize"] = 12.0;
                    resources["DetailValueFontSize"] = 13.0;
                    break;
                case TextSizeOption.Large:
                    resources["CardNameFontSize"] = 16.0;
                    resources["CardTypeFontSize"] = 14.0;
                    resources["CardBadgeFontSize"] = 11.0;
                    resources["CardNameMaxHeight"] = 80.0;
                    resources["ListFontSize"] = 16.0;
                    resources["DetailTitleFontSize"] = 28.0;
                    resources["DetailLabelFontSize"] = 14.0;
                    resources["DetailValueFontSize"] = 16.0;
                    break;
                default:
                    resources["CardNameFontSize"] = 14.0;
                    resources["CardTypeFontSize"] = 12.0;
                    resources["CardBadgeFontSize"] = 10.0;
                    resources["CardNameMaxHeight"] = 60.0;
                    resources["ListFontSize"] = 14.0;
                    resources["DetailTitleFontSize"] = 24.0;
                    resources["DetailLabelFontSize"] = 12.0;
                    resources["DetailValueFontSize"] = 14.0;
                    break;
            }

            var cardSize = GetCardSize();
            switch (cardSize)
            {
                case CardSizeOption.Small:
                    resources["CardItemHeight"] = size == TextSizeOption.Large ? 330.0
                                                : size == TextSizeOption.Small ? 250.0
                                                : 275.0;
                    break;
                case CardSizeOption.Large:
                    resources["CardItemHeight"] = size == TextSizeOption.Large ? 450.0
                                                : size == TextSizeOption.Small ? 400.0
                                                : 420.0;
                    break;
                default:
                    resources["CardItemHeight"] = size == TextSizeOption.Large ? 390.0
                                                : size == TextSizeOption.Small ? 315.0
                                                : 340.0;
                    break;
            }
        }
    }
}
