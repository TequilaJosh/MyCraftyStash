using Microsoft.EntityFrameworkCore;
using MyCraftyStash.Data;
using MyCraftyStash.Models;

namespace MyCraftyStash.Services
{
    /// <summary>
    /// Cross-reference between external color systems (DMC floss numbers,
    /// OLO marker codes, …) and Taylored Expressions color names. Backed by
    /// settings.db.color_matches. Most mappings are 1:1 but a few TE colors
    /// match multiple external codes, so the table is keyed on (system, code).
    /// </summary>
    public class ColorMatchService
    {
        public const string SystemDmc = "DMC";
        public const string SystemOlo = "OLO";

        private static readonly object _seedLock = new();
        private static bool _seeded;

        private SettingsDbContext CreateContext() => new SettingsDbContext();

        public ColorMatchService()
        {
            EnsureSeeded();
        }

        public List<ColorMatch> GetAll(string system)
        {
            try
            {
                using var ctx = CreateContext();
                return ctx.ColorMatches.AsNoTracking()
                    .Where(c => c.System == system)
                    .OrderBy(c => c.TeColorName)
                    .ThenBy(c => c.ExternalCode)
                    .ToList();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"ColorMatchService.GetAll({system})");
                return new List<ColorMatch>();
            }
        }

        /// <summary>Look up by external code (DMC#, OLO marker code).</summary>
        public ColorMatch? GetByExternalCode(string system, string code)
        {
            try
            {
                using var ctx = CreateContext();
                return ctx.ColorMatches.AsNoTracking()
                    .FirstOrDefault(c => c.System == system && c.ExternalCode == code);
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"ColorMatchService.GetByExternalCode({system},{code})");
                return null;
            }
        }

        /// <summary>Reverse lookup. May return multiple — many-to-1 is allowed.</summary>
        public List<ColorMatch> GetByTeColor(string system, string teColorName)
        {
            try
            {
                using var ctx = CreateContext();
                return ctx.ColorMatches.AsNoTracking()
                    .Where(c => c.System == system && c.TeColorName == teColorName)
                    .OrderBy(c => c.ExternalCode)
                    .ToList();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"ColorMatchService.GetByTeColor({system},{teColorName})");
                return new List<ColorMatch>();
            }
        }

        public ColorMatch Upsert(ColorMatch match)
        {
            using var ctx = CreateContext();
            var existing = ctx.ColorMatches
                .FirstOrDefault(c => c.System == match.System && c.ExternalCode == match.ExternalCode);
            if (existing == null)
            {
                ctx.ColorMatches.Add(match);
                ctx.SaveChanges();
                return match;
            }
            existing.TeColorName = match.TeColorName;
            existing.ExternalHex = match.ExternalHex;
            existing.TeColorHex  = match.TeColorHex;
            existing.Notes       = match.Notes;
            ctx.SaveChanges();
            return existing;
        }

        public void Delete(int id)
        {
            using var ctx = CreateContext();
            var row = ctx.ColorMatches.FirstOrDefault(c => c.Id == id);
            if (row == null) return;
            ctx.ColorMatches.Remove(row);
            ctx.SaveChanges();
        }

        // ── Seeding ──────────────────────────────────────────────────────────

        public void EnsureSeeded()
        {
            if (_seeded) return;
            lock (_seedLock)
            {
                if (_seeded) return;
                try
                {
                    using var ctx = CreateContext();
                    SeedSystem(ctx, SystemDmc, DmcSeed);
                    SeedSystem(ctx, SystemOlo, OloSeed);
                }
                catch (Exception ex)
                {
                    LoggingService.LogDatabaseError(ex, "ColorMatchService.EnsureSeeded");
                }
                _seeded = true;
            }
        }

        private static void SeedSystem(SettingsDbContext ctx, string system,
            (string TeColor, string Code, string? Notes)[] entries)
        {
            // Skip if anything already exists for this system — user edits win.
            if (ctx.ColorMatches.Any(c => c.System == system)) return;
            foreach (var (teColor, code, notes) in entries)
            {
                if (string.IsNullOrWhiteSpace(code)) continue;
                ctx.ColorMatches.Add(new ColorMatch
                {
                    System       = system,
                    ExternalCode = code,
                    TeColorName  = teColor,
                    Notes        = notes,
                });
            }
            ctx.SaveChanges();
        }

        // DMC floss matches — provided 2026-05-08.
        private static readonly (string TeColor, string Code, string? Notes)[] DmcSeed =
        {
            ("Rose Water",            "225",   null),
            ("Cupcake",               "3713",  null),
            ("Pink Champagne",        "761",   null),
            ("Strawberry Milkshake",  "605",   null),
            ("Bubblegum",             "603",   null),
            ("Lollipop",              "3805",  null),
            ("Dragon Fruit",          "3804",  null),
            ("Raspberry Sorbet",      "223",   null),
            ("Guava",                 "335",   null),
            ("Fruit Punch",           "309",   null),
            ("Watermelon",            "349",   null),
            ("Cherry Pop",            "321",   null),
            ("Red Pepper",            "816",   null),
            ("Mulled Wine",           "902",   null),
            ("Jujube",                "3721",  null),
            ("Peaches 'n Cream",      "754",   null),
            ("Papaya",                "352",   null),
            ("Persimmon",             "350",   null),
            ("Candy Corn",            "722",   null),
            ("Sweet Potato Pie",      "921",   null),
            ("Pumpkin",               "900",   null),
            ("Banana Cream Pie",      "3078",  null),
            ("Potato Chip",           "445",   null),
            ("Pineapple",             "743",   null),
            ("Lemon Meringue",        "726",   null),
            ("Mango",                 "972",   null),
            ("Sweet Corn",            "3821",  null),
            ("Dijon",                 "3828",  null),
            ("Honey",                 "680",   null),
            ("Mint Julep",            "564",   null),
            ("Honeydew Melon",        "524",   null),
            ("Granny Smith",          "471",   null),
            ("Kiwi",                  "907",   null),
            ("Lime Zest",             "581",   null),
            ("Pear",                  "166",   null),
            ("Dill Pickle",           "702",   null),
            ("Wintergreen",           "910",   null),
            ("Peapod",                "470",   null),
            ("Cilantro",              "987",   null),
            ("Sweet Basil",           "580",   null),
            ("Green Tea",             "3052",  null),
            ("Jalapeno",              "319",   null),
            ("Brussels Sprout",       "895",   null),
            ("Spearmint",             "3817",  null),
            ("Gumdrop",               "3815",  null),
            ("Rock Candy",            "3812",  null),
            ("Poblano Pepper",        "3847",  null),
            ("Royal Icing",           "168",   null),
            ("Confetti Cake",         "598",   null),
            ("Cookie Monster",        "959",   null),
            ("Blue Raspberry",        "958",   null),
            ("Tiki Mule",             "597",   null),
            ("Tropical Punch",        "3809",  null),
            ("Sour Gummy",            "3808",  null),
            ("Salt Water Taffy",      "828",   null),
            ("Sprinkles",             "598",   "Shares DMC# with Confetti Cake"),
            ("Gumball",               "3325",  null),
            ("Bleu Cheese",           "931",   null),
            ("Cotton Candy",          "3846",  null),
            ("Snow Cone",             "3843",  null),
            ("Blueberry",             "824",   null),
            ("Blue Corn",             "930",   null),
            ("Cake Pop",              "157",   null),
            ("Macaron",               "26",    null),
            ("Jelly Donut",           "553",   null),
            ("Dried Fig",             "793",   null),
            ("Concord Grape",         "792",   null),
            ("Lavender Glaze",        "211",   null),
            ("Plum Tart",             "3609",  null),
            ("Popsicle",              "554",   null),
            ("Plum Punch",            "33",    null),
            ("Berry Smoothie",        "917",   null),
            ("Passion Fruit",         "3834",  null),
            ("Huckleberry",           "29",    null),
            ("Sugar Cube",            "B5200", null),
            ("Sea Salt",              "1",     null),
            ("Oyster",                "3",     null),
            ("Oat Milk",              "644",   null),
            ("Earl Grey",             "4",     null),
            ("Poppy Seed",            "535",   null),
            ("Oreo",                  "310",   null),
            ("Black Licorice",        "310",   "Shares DMC# with Oreo"),
            ("Buttercream Frosting",  "746",   null),
            ("Mushroom",              "646",   null),
            ("Latte",                 "3790",  null),
            ("Toffee",                "842",   null),
            ("Cinnamon",              "610",   null),
            ("Mocha",                 "3021",  null),
            ("Chocolate Truffle",     "838",   null),
            ("Hot Fudge",             "844",   null),
        };

        // OLO marker matches — extracted from oloupdatedchart.pdf 2026-02-17.
        // Entries flagged "Retired" reflect markers OLO no longer produces but
        // were the closest historical match.
        private static readonly (string TeColor, string Code, string? Notes)[] OloSeed =
        {
            ("Rose Water",            "R0.1",   null),
            ("Cupcake",               "R2.0",   null),
            ("Pink Champagne",        "R5.1",   null),
            ("Strawberry Milkshake",  "RV3.1",  null),
            ("Bubblegum",             "RV0.2",  null),
            ("Lollipop",              "RV1.5",  null),
            ("Dragon Fruit",          "RV1.7",  null),
            ("Raspberry Sorbet",      "R5.3",   null),
            ("Guava",                 "R2.4",   null),
            ("Fruit Punch",           "RV0.4",  null),
            ("Watermelon",            "R1.4",   null),
            ("Cherry Pop",            "R1.5",   null),
            ("Red Pepper",            "R0.6",   null),
            ("Mulled Wine",           "R5.7",   null),
            ("Jujube",                "R1.7",   null),
            ("Peaches 'n Cream",      "OR4.2",  null),
            ("Papaya",                "R0.3",   null),
            ("Persimmon",             "R0.5",   null),
            ("Candy Corn",            "O2.4",   null),
            ("Sweet Potato Pie",      "OR1.6",  null),
            ("Pumpkin",               "OR2.6",  null),
            ("Banana Cream Pie",      "Y2.0",   null),
            ("Potato Chip",           "Y1.1",   null),
            ("Pineapple",             "YO2.2",  null),
            ("Lemon Meringue",        "Y2.3",   null),
            ("Mango",                 "YO0.4",  null),
            ("Sweet Corn",            "Y1.4",   null),
            ("Dijon",                 "YO0.6",  null),
            ("Honey",                 "YO2.5",  null),
            ("Key Lime",              "G1.4",   "Retired"),
            ("Mint Julep",            "G0.1",   null),
            ("Honeydew Melon",        "YG8.3",  null),
            ("Granny Smith",          "YG1.2",  null),
            ("Kiwi",                  "YG2.3",  null),
            ("Lime Zest",             "YG2.5",  null),
            ("Pear",                  "Y0.3",   null),
            ("Dill Pickle",           "YG1.6",  null),
            ("Wintergreen",           "G1.7",   null),
            ("Peapod",                "G5.3",   null),
            ("Cilantro",              "YG1.7",  null),
            ("Avocado",               "Y8.6",   "Retired"),
            ("Sweet Basil",           "YG2.7",  null),
            ("Olive",                 "Y8.7",   "Retired"),
            ("Green Tea",             "YG8.5",  null),
            ("Jalapeno",              "G7.6",   null),
            ("Brussels Sprout",       "G1.8",   null),
            ("Spearmint",             "BG5.1",  null),
            ("Rock Candy",            "BG2.5",  null),
            ("Poblano Pepper",        "BG2.7",  null),
            ("Royal Icing",           "BG7.3",  null),
            ("Confetti Cake",         "BG1.2",  null),
            ("Cookie Monster",        "BG1.4",  null),
            ("Blue Raspberry",        "BG2.4",  null),
            ("Tiki Mule",             "BG5.3",  null),
            ("Tropical Punch",        "BG5.5",  null),
            ("Sour Gummy",            "BG0.8",  null),
            ("Salt Water Taffy",      "B1.0",   null),
            ("Sprinkles",             "B4.1",   null),
            ("Gumball",               "B4.3",   null),
            ("Bleu Cheese",           "B4.6",   null),
            ("Cotton Candy",          "B2.4",   null),
            ("Snow Cone",             "B2.6",   null),
            ("Blueberry",             "B0.7",   null),
            ("Blue Corn",             "B4.7",   null),
            ("Cake Pop",              "BV2.3",  null),
            ("Macaron",               "BV7.1",  null),
            ("Jelly Donut",           "BV0.5",  null),
            ("Dried Fig",             "BV7.3",  null),
            ("Concord Grape",         "BV1.7",  null),
            ("Lavender Glaze",        "BV0.0",  null),
            ("Plum Tart",             "RV7.4",  null),
            ("Popsicle",              "V1.2",   null),
            ("Plum Punch",            "V1.4",   null),
            ("Berry Smoothie",        "RV3.7",  null),
            ("Passion Fruit",         "RV7.7",  null),
            ("Huckleberry",           "V2.7",   null),
            ("Sea Salt",              "N-G0",   null),
            ("Oyster",                "N-G3",   null),
            ("Oat Milk",              "BG7.0",  null),
            ("Earl Grey",             "N-G5",   null),
            ("Poppy Seed",            "N-G7",   null),
            ("Oreo",                  "K",      null),
            ("Black Licorice",        "C-G9",   null),
            ("Buttercream Frosting",  "YO2.0",  null),
            ("Latte",                 "O7.5",   null),
            ("Toffee",                "O4.3",   null),
            ("Cinnamon",              "O7.7",   null),
            ("Mocha",                 "W-G7",   null),
            ("Chocolate Truffle",     "OR3.8",  null),
            ("Hot Fudge",             "O7.8",   null),
        };
    }
}
