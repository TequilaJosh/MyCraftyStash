using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyCraftyStash.Data;
using MyCraftyStash.Models;

namespace MyCraftyStash.Services
{
    /// <summary>
    /// Field-scoped bulk rename pipeline for Items: Type / Subtype / Location / Theme /
    /// PurchasedFrom. Provides preview + apply, and keeps the matching ConfigStore list
    /// in sync so the dropdowns reflect the new name.
    ///
    /// Notes for porting from JandHInventory: the source app used file-based configs
    /// (locations.txt, subtypes.json, etc.). This port writes through <see cref="ConfigStore"/>
    /// instead — same content, but DB-backed via settings.db.
    /// </summary>
    public static class BulkRenameService
    {
        public enum RenameField
        {
            Type,
            Subtype,
            Location,
            Theme,
            PurchasedFrom,
        }

        /// <summary>Lists every distinct non-empty value currently in use for the given field.</summary>
        /// <param name="parentType">Only honored for Subtype — limits the listing to the rows
        /// whose Type equals <paramref name="parentType"/>. Ignored for other fields.</param>
        public static async Task<List<string>> GetExistingValuesAsync(RenameField field, string? parentType = null)
        {
            using var ctx = new InventoryDbContext();

            if (field == RenameField.Theme)
            {
                // Themes are stored comma-separated in a single column. Expand to tokens.
                var rawList = await ctx.Items
                    .Where(i => i.Theme != null && i.Theme != "")
                    .Select(i => i.Theme!)
                    .ToListAsync();

                return rawList
                    .SelectMany(t => t.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    .Select(t => t.Trim())
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            IQueryable<string?> q = field switch
            {
                RenameField.Type          => ctx.Items.Select(i => i.Type),
                RenameField.Subtype       => string.IsNullOrWhiteSpace(parentType)
                                              ? ctx.Items.Select(i => i.Subtype)
                                              : ctx.Items.Where(i => i.Type == parentType).Select(i => i.Subtype),
                RenameField.Location      => ctx.Items.Select(i => i.Location),
                RenameField.PurchasedFrom => ctx.Items.Select(i => i.PurchasedFrom),
                _ => throw new ArgumentOutOfRangeException(nameof(field)),
            };

            var values = await q
                .Where(v => v != null && v != "")
                .Distinct()
                .ToListAsync();

            return values
                .Select(v => v!.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>Returns Item Types that have at least one row with a non-empty Subtype.
        /// Used by the Subtype rename UI to pick the parent Type before showing values.</summary>
        public static async Task<List<string>> GetTypesWithSubtypesAsync()
        {
            using var ctx = new InventoryDbContext();
            var types = await ctx.Items
                .Where(i => i.Subtype != null && i.Subtype != "")
                .Select(i => i.Type)
                .Distinct()
                .ToListAsync();
            return types.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>Returns the items that would be updated by an Apply call.</summary>
        public static async Task<List<Item>> PreviewItemsAsync(RenameField field, string oldValue, string? parentType = null)
        {
            if (string.IsNullOrWhiteSpace(oldValue)) return new List<Item>();
            using var ctx = new InventoryDbContext();
            var q = BuildMatchQuery(ctx, field, oldValue, parentType);
            return await q.OrderBy(i => i.Name).ToListAsync();
        }

        /// <summary>
        /// Renames every occurrence of <paramref name="oldValue"/> to <paramref name="newValue"/>
        /// in the given field, updates the matching ConfigStore list, and invalidates caches.
        /// Returns the number of rows touched.
        /// </summary>
        public static async Task<int> ApplyAsync(RenameField field, string oldValue, string newValue, string? parentType = null)
        {
            if (string.IsNullOrWhiteSpace(oldValue)) return 0;
            newValue = (newValue ?? string.Empty).Trim();
            oldValue = oldValue.Trim();

            using var ctx = new InventoryDbContext();
            int touched;

            if (field == RenameField.Theme)
            {
                touched = await RenameThemeAsync(ctx, oldValue, newValue);
            }
            else
            {
                var matches = await BuildMatchQuery(ctx, field, oldValue, parentType).ToListAsync();
                foreach (var item in matches)
                {
                    switch (field)
                    {
                        case RenameField.Type:          item.Type          = newValue; break;
                        case RenameField.Subtype:       item.Subtype       = string.IsNullOrEmpty(newValue) ? null : newValue; break;
                        case RenameField.Location:      item.Location      = string.IsNullOrEmpty(newValue) ? null : newValue; break;
                        case RenameField.PurchasedFrom: item.PurchasedFrom = string.IsNullOrEmpty(newValue) ? null : newValue; break;
                    }
                }
                touched = matches.Count;
                if (touched > 0) await ctx.SaveChangesAsync();
            }

            UpdateConfigList(field, oldValue, newValue, parentType);

            LoggingService.LogInfo(
                $"BulkRenameService.Apply field={field} parent={parentType ?? "-"} '{oldValue}' → '{newValue}' rows={touched}");

            return touched;
        }

        private static IQueryable<Item> BuildMatchQuery(InventoryDbContext ctx, RenameField field, string oldValue, string? parentType)
        {
            return field switch
            {
                RenameField.Type          => ctx.Items.Where(i => i.Type == oldValue),
                RenameField.Subtype       => string.IsNullOrWhiteSpace(parentType)
                                              ? ctx.Items.Where(i => i.Subtype == oldValue)
                                              : ctx.Items.Where(i => i.Subtype == oldValue && i.Type == parentType),
                RenameField.Location      => ctx.Items.Where(i => i.Location == oldValue),
                RenameField.PurchasedFrom => ctx.Items.Where(i => i.PurchasedFrom == oldValue),
                RenameField.Theme         => ctx.Items.Where(i => i.Theme != null && i.Theme!.Contains(oldValue)),
                _ => throw new ArgumentOutOfRangeException(nameof(field)),
            };
        }

        // Theme is comma-separated within one column. Decompose to tokens, swap, dedupe, recompose.
        private static async Task<int> RenameThemeAsync(InventoryDbContext ctx, string oldToken, string newToken)
        {
            var candidates = await ctx.Items
                .Where(i => i.Theme != null && i.Theme!.Contains(oldToken))
                .ToListAsync();

            int touched = 0;
            foreach (var item in candidates)
            {
                var tokens = (item.Theme ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim())
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .ToList();

                bool changed = false;
                var rebuilt = new List<string>(tokens.Count);
                foreach (var t in tokens)
                {
                    if (string.Equals(t, oldToken, StringComparison.OrdinalIgnoreCase))
                    {
                        changed = true;
                        if (!string.IsNullOrWhiteSpace(newToken)
                            && !rebuilt.Any(r => string.Equals(r, newToken, StringComparison.OrdinalIgnoreCase)))
                        {
                            rebuilt.Add(newToken);
                        }
                    }
                    else if (!rebuilt.Any(r => string.Equals(r, t, StringComparison.OrdinalIgnoreCase)))
                    {
                        rebuilt.Add(t);
                    }
                }

                if (changed)
                {
                    item.Theme = rebuilt.Count > 0 ? string.Join(", ", rebuilt) : null;
                    touched++;
                }
            }

            if (touched > 0) await ctx.SaveChangesAsync();
            return touched;
        }

        // Keep the dropdown's source-of-truth list aligned with the DB rename.
        private static void UpdateConfigList(RenameField field, string oldValue, string newValue, string? parentType)
        {
            try
            {
                switch (field)
                {
                    case RenameField.Type:
                        ReplaceLineInList(ConfigStore.Types, oldValue, newValue);
                        // Type rename also needs to re-key the subtypes JSON.
                        RenameSubtypesJsonKey(oldValue, newValue);
                        break;
                    case RenameField.Location:
                        ReplaceLineInList(ConfigStore.Locations, oldValue, newValue);
                        break;
                    case RenameField.PurchasedFrom:
                        ReplaceLineInList(ConfigStore.PurchasedFrom, oldValue, newValue);
                        break;
                    case RenameField.Theme:
                        ReplaceLineInList(ConfigStore.Themes, oldValue, newValue);
                        break;
                    case RenameField.Subtype:
                        ReplaceSubtypeInJson(parentType, oldValue, newValue);
                        break;
                }

                ConfigStore.InvalidateCache();
                UserSettingsService.ReloadSubtypes();
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, $"BulkRenameService.UpdateConfigList field={field}");
            }
        }

        private static void ReplaceLineInList(string listName, string oldValue, string newValue)
        {
            var lines = ConfigStore.GetLines(listName);
            bool changed = false;
            var rebuilt = new List<string>(lines.Count);
            foreach (var line in lines)
            {
                if (string.Equals(line, oldValue, StringComparison.OrdinalIgnoreCase))
                {
                    changed = true;
                    if (!string.IsNullOrWhiteSpace(newValue)
                        && !rebuilt.Any(r => string.Equals(r, newValue, StringComparison.OrdinalIgnoreCase)))
                    {
                        rebuilt.Add(newValue);
                    }
                }
                else
                {
                    rebuilt.Add(line);
                }
            }

            if (changed)
            {
                ConfigStore.SetText(listName, string.Join(Environment.NewLine, rebuilt));
            }
        }

        // Subtypes is a JSON dictionary keyed by parent Type; values are subtype lists.
        // Renaming the parent Type means re-keying the entry.
        private static void RenameSubtypesJsonKey(string oldType, string newType)
        {
            if (string.IsNullOrWhiteSpace(oldType) || string.IsNullOrWhiteSpace(newType)) return;
            var json = ConfigStore.GetText(ConfigStore.Subtypes);
            if (string.IsNullOrWhiteSpace(json)) return;

            Dictionary<string, List<string>>? map;
            try
            {
                map = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json);
            }
            catch
            {
                return;
            }
            if (map == null || !map.TryGetValue(oldType, out var subtypes)) return;

            map.Remove(oldType);
            if (map.TryGetValue(newType, out var existing))
            {
                foreach (var s in subtypes)
                {
                    if (!existing.Any(e => string.Equals(e, s, StringComparison.OrdinalIgnoreCase)))
                        existing.Add(s);
                }
            }
            else
            {
                map[newType] = subtypes;
            }

            ConfigStore.SetText(ConfigStore.Subtypes,
                JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true }));
        }

        // Rename a single subtype value within (optionally) one parent Type's list.
        private static void ReplaceSubtypeInJson(string? parentType, string oldValue, string newValue)
        {
            var json = ConfigStore.GetText(ConfigStore.Subtypes);
            if (string.IsNullOrWhiteSpace(json)) return;

            Dictionary<string, List<string>>? map;
            try
            {
                map = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json);
            }
            catch
            {
                return;
            }
            if (map == null) return;

            bool changed = false;
            foreach (var key in map.Keys.ToList())
            {
                if (!string.IsNullOrWhiteSpace(parentType)
                    && !string.Equals(key, parentType, StringComparison.OrdinalIgnoreCase)) continue;

                var list = map[key];
                var rebuilt = new List<string>(list.Count);
                bool localChanged = false;
                foreach (var v in list)
                {
                    if (string.Equals(v, oldValue, StringComparison.OrdinalIgnoreCase))
                    {
                        localChanged = true;
                        if (!string.IsNullOrWhiteSpace(newValue)
                            && !rebuilt.Any(r => string.Equals(r, newValue, StringComparison.OrdinalIgnoreCase)))
                        {
                            rebuilt.Add(newValue);
                        }
                    }
                    else if (!rebuilt.Any(r => string.Equals(r, v, StringComparison.OrdinalIgnoreCase)))
                    {
                        rebuilt.Add(v);
                    }
                }
                if (localChanged)
                {
                    map[key] = rebuilt;
                    changed = true;
                }
            }

            if (changed)
            {
                ConfigStore.SetText(ConfigStore.Subtypes,
                    JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true }));
            }
        }
    }
}
