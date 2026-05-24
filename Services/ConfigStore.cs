using System.IO;
using Microsoft.EntityFrameworkCore;
using JandH.Core.Models;
using JandH.Core.Data;
using MyCraftyStash.Data;
using JandH.Core.Models;
using JandH.Core.Data;
using MyCraftyStash.Models;

using JandH.Core.Services;
using JandH.Core.ViewModels;

namespace MyCraftyStash.Services
{
    /// <summary>
    /// Replaces the old file-based ConfigPathService. All shared lists
    /// (types/themes/locations/colors/subtypes/etc.) live in settings.db now,
    /// each as a single row in the config_lists table whose Content is the
    /// raw text body — line-separated for the line lists, JSON for the
    /// structured ones — so existing parsers stay unchanged.
    /// </summary>
    public static class ConfigStore
    {
        // Stable list names (used as primary keys in config_lists).
        public const string Types               = "types";
        public const string Themes              = "themes";
        public const string Locations           = "locations";
        public const string ColorOrder          = "color_order";
        public const string Subtypes            = "subtypes";
        public const string TrackedTypes        = "tracked_types";
        public const string ProjectTrackedItems = "project_tracked_items";
        public const string PurchasedFrom       = "purchased_from";
        public const string InspirationColors   = "inspiration_colors";
        public const string CardLabels          = "card_labels";

        private static readonly object _cacheLock = new();
        private static readonly Dictionary<string, string> _cache = new();
        private static bool _seeded;

        private static SettingsDbContext CreateContext() => new SettingsDbContext();

        private static void EnsureSeeded()
        {
            if (_seeded) return;
            lock (_cacheLock)
            {
                if (_seeded) return;
                try
                {
                    using var ctx = CreateContext();
                    ctx.Database.Migrate();

                    var existing = ctx.ConfigLists.AsNoTracking().Select(c => c.Name).ToHashSet();
                    foreach (var (name, body) in Defaults.All)
                    {
                        if (!existing.Contains(name))
                            ctx.ConfigLists.Add(new ConfigList { Name = name, Content = body });
                    }
                    if (ctx.ChangeTracker.HasChanges())
                        ctx.SaveChanges();
                }
                catch (Exception ex)
                {
                    LoggingService.LogDatabaseError(ex, "ConfigStore.EnsureSeeded");
                }
                _seeded = true;
            }
        }

        /// <summary>Read raw text content for a config list. Empty string if missing.</summary>
        public static string GetText(string name)
        {
            EnsureSeeded();
            lock (_cacheLock)
            {
                if (_cache.TryGetValue(name, out var cached)) return cached;
            }

            string text;
            try
            {
                using var ctx = CreateContext();
                text = ctx.ConfigLists.AsNoTracking().FirstOrDefault(c => c.Name == name)?.Content
                       ?? string.Empty;
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"ConfigStore.GetText({name})");
                return string.Empty;
            }

            lock (_cacheLock) { _cache[name] = text; }
            return text;
        }

        /// <summary>
        /// Read non-empty trimmed lines from a list config. Returns empty list
        /// if the entry is missing or has no usable lines.
        /// </summary>
        public static List<string> GetLines(string name)
        {
            var text = GetText(name);
            if (string.IsNullOrWhiteSpace(text)) return new List<string>();
            return text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();
        }

        /// <summary>Replace the content of a config list (creates row if missing).</summary>
        public static void SetText(string name, string content)
        {
            EnsureSeeded();
            try
            {
                using var ctx = CreateContext();
                var row = ctx.ConfigLists.FirstOrDefault(c => c.Name == name);
                if (row == null)
                {
                    ctx.ConfigLists.Add(new ConfigList { Name = name, Content = content ?? string.Empty });
                }
                else
                {
                    row.Content = content ?? string.Empty;
                }
                ctx.SaveChanges();

                lock (_cacheLock) { _cache[name] = content ?? string.Empty; }
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"ConfigStore.SetText({name})");
                throw;
            }
        }

        public static void InvalidateCache()
        {
            lock (_cacheLock) { _cache.Clear(); }
        }

        // ── Default seed content for fresh installs ───────────────────────────
        // Hardcoded so a new install has working defaults without needing the
        // old file-based Config\ folder. User edits via the Settings dialog
        // override these (writes flip the row to a non-default value).
        private static class Defaults
        {
            public static readonly (string Name, string Body)[] All = new[]
            {
                (Types,             TypesText),
                (Themes,            ThemesText),
                (Locations,         LocationsText),
                (ColorOrder,        string.Empty),
                (Subtypes,          "{}"),
                (TrackedTypes,      "[]"),
                (ProjectTrackedItems, "{}"),
                (PurchasedFrom,     PurchasedFromText),
                (InspirationColors, string.Empty),
                (CardLabels,        "{}"),
            };

            private const string TypesText = """
3D Embossing Folder
5X7 Background Stamp
6X6 Embossing Folder
A2 Card Bases
A2 Embossing Folder
A2 Envelopes
A7 Card Bases
A7 Envelopes
Background Stamps
Bits & Pieces
Cardstock
Clear Stamp Set
Cling & Clear Stamp Combo
Combo Club
Die & Clear Stamp Combo
Dies
Embossing Powder
Enamel Dots
Foil Cardstock
Foil-its
Foils
Glitter Cardstock
Glitters
Happy Medium
Ink - Full Size
Ink - Mini
Ink - Refill
Inspiration Gallery
Kits
Label Sheets
Little Bits
Mini Slim Card Bases
Mini Slim Envelopes
Mini Strip Stamps
Miscellaneous
OLO Markers
Piercing & Cutting Plates
Stacklets - A2
Stacklets - A7
Stacklets - Mini Slim
Stamp & Die Combo
Stamp & Stencil
Stamp
Stencil & 3D Embossing Folder Combo
Stamps
Stencil - Create-in-Quads
Stencil - Multi Layer
Stencil - Sextet
Stencil - Single Layer
Stencil - Triple Slim
Stencil - Design Duo
Stencil & Die Combo
Storage
Tools
Watercolor
""";

            private const string ThemesText = """
Birthday
Christmas
Easter
Floral
Graduation
Greetings
Halloween
Inspiration
Sympathy
Thanksgiving
Valentine's Day
Wedding & Baby
Winter
Spring
Summer
Autumn
Words
Alphabets & Numbers
""";

            private const string LocationsText = """
Box #1
Box #2
Box #3
Box #4
Box #5
""";

            private const string PurchasedFromText = """
Amazon
Local Store
Online Shop
""";
        }
    }
}
