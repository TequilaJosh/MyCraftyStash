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

        /// <summary>
        /// Overwrite every config list with its bundled default (the JandH
        /// shared-share snapshot baked into <see cref="Defaults"/>). Destructive:
        /// user customizations made via Settings tabs are lost. Invoked by the
        /// "Reset all config lists to defaults" button on the Display tab.
        /// </summary>
        public static void ResetAllToDefaults()
        {
            try
            {
                using var ctx = CreateContext();
                ctx.Database.Migrate();

                foreach (var (name, body) in Defaults.All)
                {
                    var row = ctx.ConfigLists.FirstOrDefault(c => c.Name == name);
                    if (row == null)
                        ctx.ConfigLists.Add(new ConfigList { Name = name, Content = body });
                    else
                        row.Content = body;
                }
                ctx.SaveChanges();

                lock (_cacheLock) { _cache.Clear(); }
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "ConfigStore.ResetAllToDefaults");
                throw;
            }
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
                (ColorOrder,        ColorOrderText),
                (Subtypes,          SubtypesJson),
                (TrackedTypes,      TrackedTypesJson),
                (ProjectTrackedItems, "{}"),
                (PurchasedFrom,     PurchasedFromText),
                (InspirationColors, InspirationColorsText),
                (CardLabels,        "{}"),
            };

            // ── JandH config snapshot (May 2026) ──────────────────────────────
            // Sourced from the network share at
            // \\Win-u5iq2hisnh3\e\JandH Inventory\Installation\Application Files\Configs\
            // so MCS fresh installs and Reset-to-defaults present the same
            // type/theme/location/color taxonomy the JandH wife has curated.
            // Trailing-backtick and leading-asterisk markers preserved
            // verbatim — JandH's settings UI strips them with
            // l.TrimStart('*').TrimEnd('`').Trim() before display.

            private const string TypesText = """
Embossing Folders
Envelopes
Embellishments
Cardstock
Card Bases
Dies
Inks
Kits
OLO Markers
Stacklets
Stencils
Stamps
Storage`
Tools`
Adhesives
Watercolor
Labeling`
Color Inspiration`
Combo Club
Foils
Miscellaneous
""";

            private const string ThemesText = """
Birthday
Sympathy
Greetings
Thank You
Get Well Soon
Encouraging
Graduation
Wedding & Anniversary
Baby
Retirement
Pet/Cat & Dog
New Years
Valentine's Day
St. Patrick's Day
Easter
Parent's Days
American
Halloween
Thanksgiving
Christmas
Winter
Spring
Summer
Autumn
Sports
Words
Alphabets & Numbers
Floral
Strips
Embellishment
Inspiration
Freebies
Gift with Purchase
Kits
*Combo Club
*Sidewalk Sale
*Advent Calendar
*Stampjoy
*Summer School
""";

            private const string LocationsText = """
Overhead Top Right
Overhead Bottom Right
Overhead Top Left
Overhead Bottom Left
Front Top Right
Front Bottom Right
Front Top Left
Front Bottom Left
Binder 1: Quads
Binder 2: Quads
Binder 3: Quads, Take Two
Binder 4: Triple Slims, Hexad, Design Duo
Binder 5: Single Layer
Binder 6: Single Layer
Binder 7: Multi Layer
Binder 8: Mini Slim
Binder 9: Palette Playbook
Binder 10: Palette Playbook
Binder 11: Palette Playbook, Happy Half Sheets
Binder 12: Happy Half Sheets, Happy Square Sheets
Foil-it Box 1: Holidays
Foil-it Box 2: Sentiments
Foil-it Box 3: Quads, Slims, Backgrounds
Foil-it Box 4: Sentiments
Foil-it Top Drawer: 6X6
Box 1
Box 2
Box 3
Box 4
Box 5
Box 6
Box 7
Box 8
Box 9
Box 10
Box 11
""";

            private const string PurchasedFromText = """
Taylored Expressions
Buy/Sell/Trade
Sidewalk Sale
E-Bay
Garage Sale
""";

            private const string ColorOrderText = """
Rose Water
Cupcake
Pink Champagne
Strawberry Milkshake
Bubblegum
Lollipop
Dragon Fruit
Raspberry Sorbet
Guava
Fruit Punch
Watermelon
Cherry Pop
Red Pepper
Mulled Wine
Jujube
Peaches 'n Cream
Papaya
Persimmon
Candy Corn
Sweet Potato Pie
Pumpkin
Banana Cream Pie
Potato Chip
Pineapple
Lemon Meringue
Mango
Sweet Corn
Dijon
Honey
Key Lime
Mint Julep
Honeydew Melon
Granny Smith
Kiwi
Lime Zest
Pear
Dill Pickle
Wintergreen
Peapod
Cilantro
Avocado
Sweet Basil
Olive
Green Tea
Jalapeño
Jalapeno
Brussels Sprout
Spearmint
Gumdrop
Rock Candy
Poblano Pepper
Royal Icing
Confetti Cake
Cookie Monster
Blue Raspberry
Tiki Mule
Tropical Punch
Sour Gummy
Salt Water Taffy
Sprinkles
Gumball
Bleu Cheese
Cotton Candy
Snow Cone
Blueberry
Blue Corn
Cake Pop
Macaron
Jelly Donut
Dried Fig
Concord Grape
Lavender Glaze
Plum Tart
Popsicle
Plum Punch
Berry Smoothie
Passion Fruit
Huckleberry
Eggplant
Sugar Cube
Sea Salt
Oyster
Oat Milk
Earl Grey
Poppy Seed
Oreo
Black Licorice
Buttercream Frosting
Mushroom
Latte
Toffee
Cinnamon
Mocha
Chocolate Truffle
Hot Fudge
VersaFine Clair
VersaMark Watermark
""";

            private const string InspirationColorsText = """
Blue
Red
Purple
Yellow
Green
Brown
Grey
Teal
""";

            // Pretty-printed for readability; deserializer doesn't care.
            private const string SubtypesJson = """
{
  "Envelopes": ["A2", "A7", "Mini Slim", "3X3"],
  "Embossing Folders": ["2D", "3D", "A2", "6X6"],
  "Embellishments": ["Bits & Pieces", "Embossing Powder", "Enamel Dots", "Little Bits", "Tiny Diamonds", "Glitter", "Happy Medium", "Astro Paste"],
  "Cardstock": ["8.5X11", "Create in Quads", "Triple Slim", "6X6", "Design Duo", "Glitter", "Foil", "Foil-its", "Insiders", "Maps", "Shakers"],
  "Card Bases": ["A2 Top Fold", "A2 Side Fold", "A7 Top Fold", "A7 Side Fold", "Mini Slim Top Fold", "Mini Slim Side Fold"],
  "Dies": ["Clear Stamp Combo", "Sentiment Sets", "Mega Messages", "Itty Bitty", "Frames", "Cutting & Piercing Plates", "All Planned Out", "Floral", "Graduation", "Edgers", "Words", "Dimensional", "Alphabets & Numbers", "Holiday & Miscellaneous", "Strips & Tags", "Simple Tags", "Little Bits", "Sports", "Mini Strips", "Bitty Strips"],
  "Inks": ["Mini Cube", "Full Pad", "Refill"],
  "Stacklets": ["A2", "A7", "Mini Slim", "Scallop", "Stitched"],
  "Stencils": ["Create in Quads", "Triple Slim", "Multi Layer", "Hexad", "Design Duo", "Single Layer", "Mini Slim", "Take Two", "Die Combo", "3D Embossing Folder Combo"],
  "Stamps": ["Sentiment Set", "5X7", "6X6", "A7", "Background", "Clear Set", "Cling & Clear Combo", "Simple Strips", "Mini Strips", "Bitty Strips", "Die Combo", "Sencil Combo", "Graduation", "Wedding, Anniversary & Baby", "Sympathy", "Big", "Flip The", "You Are", "Modern", "Greetings", "Oh My Word", "All Together", "Building Blocks", "New Years", "Valentines", "St Patricks", "Easter", "Parents Day", "American", "Thanksgiving", "Halloween", "Christmas", "Winter", "Spring", "Summer", "Autumn", "Itty Bitty", "Miscellanous"],
  "Adhesives": ["Glue", "Foam", "Tape Runner"]
}
""";

            private const string TrackedTypesJson = """
[
  { "Type": "Envelopes", "GoodThreshold": 10, "LowThreshold": 5, "OutThreshold": 1 }
]
""";
        }
    }
}
