using System.IO;
using Microsoft.EntityFrameworkCore;
using MyCraftyStash.Data;
using MyCraftyStash.Models;

namespace MyCraftyStash.Services
{
    public class InventoryService
    {
        // Schema bootstrap runs at most once per process. Multiple ViewModels create
        // their own InventoryService, and Migrate() is not safe to race.
        private static readonly object _bootstrapLock = new();
        private static bool _bootstrapped;

        public InventoryService()
        {
            if (_bootstrapped) return;
            lock (_bootstrapLock)
            {
                if (_bootstrapped) return;
                try
                {
                    using var context = CreateContext();
                    context.Database.Migrate();
                }
                catch (Exception ex)
                {
                    LoggingService.LogDatabaseError(ex, "Database migration (non-fatal — app will continue in degraded mode)");
                }
                _bootstrapped = true;
            }
        }

        public async Task<List<string>> GetDistinctSentimentLinesAsync()
        {
            try
            {
                using var context = CreateContext();
                var allSentiments = await context.Items
                    .Where(i => i.Sentiments != null && i.Sentiments != "")
                    .Select(i => i.Sentiments!)
                    .ToListAsync();

                return allSentiments
                    .SelectMany(s => s.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    .Select(l => l.Trim())
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(l => l)
                    .ToList();
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex);
                return new List<string>();
            }
        }

        private static InventoryDbContext CreateContext() => new InventoryDbContext();
        
        private static readonly List<string> TypeOrder = new List<string>
        {
            "3D Embossing Folder", "5X7 Background Stamp", "6X6 Embossing Folder",
            "A2 Card Bases", "A2 Embossing Folder", "A2 Envelopes", "A7 Card Bases", "A7 Envelopes",
            "Background Stamps", "Bits & Pieces", "Cardstock", "Clear Stamp Set",
            "Cling & Clear Stamp Combo", "Die & Clear Stamp Combo", "Dies", "Embossing Powder",
            "Enamel Dots", "Foil Cardstock", "Foil-its", "Foils", "Glitter Cardstock", "Glitters",
            "Happy Medium", "Ink - Full Size", "Ink - Mini", "Ink - Refill", "Inspiration Gallery",
            "Kits", "Label Sheets", "Little Bits", "Mini Slim Card Bases", "Mini Slim Envelopes",
            "Mini Strip Stamps", "Miscellaneous", "OLO Markers", "Piercing & Cutting Plates",
            "Stacklets - A2", "Stacklets - A7", "Stacklets - Mini Slim", "Stamp & Die Combo",
            "Stamp & Stencil", "Stamp", "Stencil & 3D Embossing Folder Combo", "Stamps",
            "Stencil - Create-in-Quads", "Stencil - Multi Layer", "Stencil - Sextet",
            "Stencil - Single Layer", "Stencil - Triple Slim", "Stencil - Design Duo",
            "Stencil & Die Combo", "Storage", "Tools", "Watercolor"
        };

        private static readonly HashSet<string> ColorSortedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Ink - Full Size", "Ink - Mini", "Ink - Refill", "Watercolor", "Cardstock",
            "A2 Envelopes", "A7 Envelopes", "Mini Slim Envelopes"
        };

        private static List<string>? _colorOrder;

        public static List<string> ColorOrder
        {
            get
            {
                if (_colorOrder == null)
                {
                    _colorOrder = LoadColorOrderFromFile();
                }
                return _colorOrder;
            }
        }

        public static void ReloadColorOrder()
        {
            _colorOrder = null;
            _colorOrderLower = null;
        }

        private static List<string> LoadColorOrderFromFile()
        {
            try
            {
                return ConfigStore.GetLines(ConfigStore.ColorOrder);
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "Error loading color order");
                return new List<string>();
            }
        }

        private static List<string>? _inspirationColors;

        public static List<string> InspirationColors
        {
            get
            {
                if (_inspirationColors == null)
                    _inspirationColors = LoadInspirationColorsFromFile();
                return _inspirationColors;
            }
        }

        public static void ReloadInspirationColors()
        {
            _inspirationColors = null;
        }

        private static List<string> LoadInspirationColorsFromFile()
        {
            try
            {
                return ConfigStore.GetLines(ConfigStore.InspirationColors);
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "Error loading inspiration colors");
                return new List<string>();
            }
        }

        public static readonly List<string> SortOptions = new List<string>
        {
            "Color", "Type", "A-Z", "Z-A",
            "Date Purchased (Oldest)", "Date Purchased (Newest)",
            "Theme", "Location", "Subtype"
        };

        private static IOrderedEnumerable<Item> ApplySingleSort(IEnumerable<Item> items, string sortKey, bool isFirst)
        {
            // When chaining (isFirst=false), items must already be IOrderedEnumerable so we can ThenBy
            var ordered = isFirst ? null : items as IOrderedEnumerable<Item>
                ?? items.OrderBy(_ => 0); // fallback: stable no-op order

            return sortKey switch
            {
                "Color" => isFirst
                    ? items.OrderBy(i => GetColorSortIndex(i.Name))
                    : ordered!.ThenBy(i => GetColorSortIndex(i.Name)),

                "Type" => isFirst
                    ? items.OrderBy(i => { int x = TypeOrder.IndexOf(i.Type); return x >= 0 ? x : TypeOrder.Count; })
                    : ordered!.ThenBy(i => { int x = TypeOrder.IndexOf(i.Type); return x >= 0 ? x : TypeOrder.Count; }),

                "A-Z" => isFirst
                    ? items.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                    : ordered!.ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase),

                "Z-A" => isFirst
                    ? items.OrderByDescending(i => i.Name, StringComparer.OrdinalIgnoreCase)
                    : ordered!.ThenByDescending(i => i.Name, StringComparer.OrdinalIgnoreCase),

                "Date Purchased (Oldest)" => isFirst
                    ? items.OrderBy(i => i.DatePurchased ?? DateTime.MaxValue)
                    : ordered!.ThenBy(i => i.DatePurchased ?? DateTime.MaxValue),

                "Date Purchased (Newest)" => isFirst
                    ? items.OrderByDescending(i => i.DatePurchased ?? DateTime.MinValue)
                    : ordered!.ThenByDescending(i => i.DatePurchased ?? DateTime.MinValue),

                "Theme" => isFirst
                    ? items.OrderBy(i => i.Theme ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                    : ordered!.ThenBy(i => i.Theme ?? string.Empty, StringComparer.OrdinalIgnoreCase),

                "Location" => isFirst
                    ? items.OrderBy(i => i.Location ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                    : ordered!.ThenBy(i => i.Location ?? string.Empty, StringComparer.OrdinalIgnoreCase),

                "Subtype" => isFirst
                    ? items.OrderBy(i => i.Subtype ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                    : ordered!.ThenBy(i => i.Subtype ?? string.Empty, StringComparer.OrdinalIgnoreCase),

                _ => isFirst
                    ? items.OrderBy(_ => 0)   // no-op stable sort
                    : ordered!.ThenBy(_ => 0),
            };
        }

        private static List<Item> ApplySortOrder(List<Item> items)
        {
            var globalSort = UserSettingsService.Current.DefaultSortOrder;

            // Separate items into those with a custom sort config vs those without
            var grouped = items.GroupBy(i => i.Type).ToList();
            var customSortedGroups = new Dictionary<string, List<Item>>(StringComparer.OrdinalIgnoreCase);
            var defaultSortItems = new List<Item>();

            foreach (var group in grouped)
            {
                var customSort = UserSettingsService.GetSortOrderForType(group.Key);
                bool hasCustom = !string.IsNullOrEmpty(customSort.Sort1);

                if (hasCustom)
                {
                    // Apply multi-key sort entirely within this type group
                    // Sort1 is primary, Sort2 is ThenBy, Sort3 is ThenBy - all chained correctly
                    IEnumerable<Item> sorted = group.ToList();
                    sorted = ApplySingleSort(sorted, customSort.Sort1, isFirst: true);
                    if (!string.IsNullOrEmpty(customSort.Sort2))
                        sorted = ApplySingleSort(sorted, customSort.Sort2, isFirst: false);
                    if (!string.IsNullOrEmpty(customSort.Sort3))
                        sorted = ApplySingleSort(sorted, customSort.Sort3, isFirst: false);

                    customSortedGroups[group.Key] = sorted.ToList();
                }
                else
                {
                    defaultSortItems.AddRange(group);
                }
            }

            // Sort the default items using the global sort + fallback color/name
            IEnumerable<Item> globalSorted = globalSort switch
            {
                "Name (A-Z)"              => defaultSortItems.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase),
                "Name (Z-A)"              => defaultSortItems.OrderByDescending(i => i.Name, StringComparer.OrdinalIgnoreCase),
                "Date Purchased (Newest)" => defaultSortItems.OrderByDescending(i => i.DatePurchased ?? DateTime.MinValue),
                "Date Purchased (Oldest)" => defaultSortItems.OrderBy(i => i.DatePurchased ?? DateTime.MaxValue),
                "Price (Low to High)"     => defaultSortItems.OrderBy(i => i.Price ?? decimal.MaxValue),
                "Price (High to Low)"     => defaultSortItems.OrderByDescending(i => i.Price ?? 0),
                _                         => defaultSortItems
                    .OrderBy(i => { int x = TypeOrder.IndexOf(i.Type); return x >= 0 ? x : TypeOrder.Count; })
                    .ThenBy(i => ColorSortedTypes.Contains(i.Type) ? GetColorSortIndex(i.Name) : int.MaxValue)
                    .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            };

            // Now merge: interleave custom-sorted groups back in the correct type order
            // Build the final type order from TypeOrder list + any extras
            var allTypes = grouped.Select(g => g.Key).ToList();
            var orderedTypes = allTypes
                .OrderBy(t => { int x = TypeOrder.IndexOf(t); return x >= 0 ? x : TypeOrder.Count; })
                .ToList();

            var result = new List<Item>();

            // Add default-sorted items first, grouped by type order
            var defaultByType = globalSorted.GroupBy(i => i.Type)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            foreach (var type in orderedTypes)
            {
                if (customSortedGroups.TryGetValue(type, out var customGroup))
                    result.AddRange(customGroup);
                else if (defaultByType.TryGetValue(type, out var defaultGroup))
                    result.AddRange(defaultGroup);
            }

            return result;
        }

        private static List<string>? _colorOrderLower;

        private static List<string> ColorOrderLower
        {
            get
            {
                if (_colorOrderLower == null || _colorOrderLower.Count != ColorOrder.Count)
                {
                    _colorOrderLower = ColorOrder.Select(c => c.ToLowerInvariant()).ToList();
                }
                return _colorOrderLower;
            }
        }

        private static int GetColorSortIndex(string itemName)
        {
            var colorOrderLower = ColorOrderLower;
            if (colorOrderLower.Count == 0) return int.MaxValue;

            var nameLower = itemName.ToLowerInvariant();
            int bestIndex = -1;
            int bestLength = 0;
            for (int i = 0; i < colorOrderLower.Count; i++)
            {
                var color = colorOrderLower[i];
                if (nameLower.Contains(color) && color.Length > bestLength)
                {
                    bestIndex = i;
                    bestLength = color.Length;
                }
            }
            return bestIndex >= 0 ? bestIndex : int.MaxValue;
        }
        
        public async Task<List<Item>> GetItemsAsync(string? search = null, string? type = null, string? theme = null, string? sentiment = null, string? searchMode = null, bool noPictureOnly = false, List<string>? subtypes = null, bool discontinuedOnly = false)
        {
            try
            {
                using var context = CreateContext();
                var query = context.Items.AsNoTracking().AsQueryable();
                
                if (!string.IsNullOrWhiteSpace(search))
                {
                    search = search.ToLower();
                    if (searchMode == "name")
                    {
                        query = query.Where(i => i.Name.ToLower().Contains(search));
                    }
                    else if (searchMode == "theme")
                    {
                        query = query.Where(i => i.Theme != null && i.Theme.ToLower().Contains(search));
                    }
                    else
                    {
                        query = query.Where(i =>
                            i.Name.ToLower().Contains(search) ||
                            (i.Theme != null && i.Theme.ToLower().Contains(search)) ||
                            (i.Sentiments != null && i.Sentiments.ToLower().Contains(search)));
                    }
                }
                
                if (!string.IsNullOrWhiteSpace(type))
                {
                    if (type == "Combo Club")
                        query = query.Where(i => i.Theme != null && i.Theme.Contains("Combo Club"));
                    else
                        query = query.Where(i => i.Type == type);
                }
                
                if (!string.IsNullOrWhiteSpace(theme))
                {
                    query = query.Where(i => i.Theme != null && i.Theme.ToLower().Contains(theme.ToLower()));
                }
                
                if (!string.IsNullOrWhiteSpace(sentiment))
                {
                    query = query.Where(i => i.Sentiments != null && i.Sentiments.ToLower().Contains(sentiment.ToLower()));
                }

                if (noPictureOnly)
                {
                    query = query.Where(i => i.ImageUrl == null || i.ImageUrl == "");
                }

                if (discontinuedOnly)
                {
                    query = query.Where(i => i.IsDiscontinued);
                }

                var items = await query.Select(i => new Item
                {
                    Id = i.Id,
                    Name = i.Name,
                    Type = i.Type,
                    Location = i.Location,
                    Theme = i.Theme,
                    Sentiments = i.Sentiments,
                    ImageUrl = null,
                    Price = i.Price,
                    DatePurchased = i.DatePurchased,
                    ItemNumber = i.ItemNumber,
                    IsDiscontinued = i.IsDiscontinued,
                    StencilLayers = i.StencilLayers,
                    Subtype = i.Subtype,
                    PackSize = i.PackSize,
                    CurrentStock = i.CurrentStock,
                    PurchasedFrom = i.PurchasedFrom,
                    Notes = i.Notes,
                    CreatedAt = i.CreatedAt
                }).ToListAsync();

                if (subtypes != null && subtypes.Count > 0)
                {
                    if (type == "Combo Club")
                    {
                        // For Combo Club, the "subtypes" are edition labels (e.g. "2024", "2025").
                        // Filter by theme containing "Combo Club {edition}" for any checked edition.
                        items = items.Where(i =>
                            i.Theme != null &&
                            subtypes.Any(ed =>
                                i.Theme.Split(',').Select(p => p.Trim())
                                    .Any(p => string.Equals(p, $"Combo Club {ed}", StringComparison.OrdinalIgnoreCase))))
                            .ToList();
                    }
                    else
                    {
                        // Subtypes are stored as comma-separated values (e.g. "Stitched, A2").
                        // Keep items matching ANY checked subtype; items matching ALL come first.
                        var subtypeSet = subtypes.ToHashSet(StringComparer.OrdinalIgnoreCase);
                        items = items.Where(i =>
                            i.Subtype != null &&
                            subtypeSet.Any(selected =>
                                i.Subtype.Split(',').Select(s => s.Trim())
                                         .Contains(selected, StringComparer.OrdinalIgnoreCase))).ToList();
                    }
                }

                var sorted = ApplySortOrder(items);

                // When multiple subtypes are checked, float full-match items to the top
                // while preserving the existing sort order within each group.
                if (subtypes != null && subtypes.Count > 1 && type != "Combo Club")
                {
                    var subtypeSet = subtypes.ToHashSet(StringComparer.OrdinalIgnoreCase);
                    sorted = sorted
                        .OrderByDescending(i =>
                            i.Subtype != null &&
                            subtypeSet.All(selected =>
                                i.Subtype.Split(',').Select(s => s.Trim())
                                         .Contains(selected, StringComparer.OrdinalIgnoreCase)) ? 1 : 0)
                        .ToList();
                }

                return sorted;
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "GetItemsAsync");
                throw;
            }
        }
        
        /// <summary>
        /// Looks up the subset of the supplied catalog item numbers that already exist in the
        /// local inventory. Returns a dictionary keyed by item number (case-insensitive) with
        /// the existing Item's Id and Name, so callers can flag duplicates and link to them.
        /// </summary>
        public async Task<Dictionary<string, (int Id, string Name)>> GetExistingItemsByItemNumbersAsync(
            IEnumerable<string?> itemNumbers)
        {
            var result = new Dictionary<string, (int, string)>(StringComparer.OrdinalIgnoreCase);
            var keys = itemNumbers
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (keys.Count == 0) return result;

            try
            {
                using var context = CreateContext();
                var matches = await context.Items.AsNoTracking()
                    .Where(i => i.ItemNumber != null && keys.Contains(i.ItemNumber))
                    .Select(i => new { i.Id, i.Name, i.ItemNumber })
                    .ToListAsync();

                foreach (var m in matches)
                {
                    if (!string.IsNullOrEmpty(m.ItemNumber))
                        result[m.ItemNumber] = (m.Id, m.Name);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "GetExistingItemsByItemNumbersAsync");
            }
            return result;
        }

        public async Task<Item?> GetItemByIdAsync(int id)
        {
            try
            {
                using var context = CreateContext();
                return await context.Items.AsNoTracking()
                    .FirstOrDefaultAsync(i => i.Id == id);
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"GetItemByIdAsync (id: {id})");
                return null;
            }
        }

        public async Task<string?> GetItemImageUrlAsync(int id)
        {
            try
            {
                using var context = CreateContext();
                return await context.Items.AsNoTracking()
                    .Where(i => i.Id == id)
                    .Select(i => i.ImageUrl)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"GetItemImageUrlAsync (id: {id})");
                return null;
            }
        }
        
        public async Task<Item?> GetItemAsync(int id)
        {
            try
            {
                using var context = CreateContext();
                return await context.Items.FindAsync(id);
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"GetItemAsync (id: {id})");
                throw;
            }
        }
        
        public async Task<Item> CreateItemAsync(Item item)
        {
            try
            {
                using var context = CreateContext();
                item.CreatedAt = DateTime.Now;
                context.Items.Add(item);
                await context.SaveChangesAsync();
                return item;
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"CreateItemAsync (name: {item.Name})");
                throw;
            }
        }
        
        public async Task<Item> UpdateItemAsync(Item item)
        {
            try
            {
                using var context = CreateContext();
                context.Items.Update(item);
                await context.SaveChangesAsync();
                return item;
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"UpdateItemAsync (id: {item.Id})");
                throw;
            }
        }
        
        public async Task DeleteItemAsync(int id)
        {
            try
            {
                using var context = CreateContext();
                var item = await context.Items.FindAsync(id);
                if (item != null)
                {
                    // Clean up relationships where this item is the RelatedItem (NoAction FK)
                    // The relationships where this item is the ItemId will cascade automatically
                    var reverseRelationships = await context.ItemRelationships
                        .Where(r => r.RelatedItemId == id)
                        .ToListAsync();
                    if (reverseRelationships.Any())
                    {
                        context.ItemRelationships.RemoveRange(reverseRelationships);
                    }
                    
                    context.Items.Remove(item);
                    await context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"DeleteItemAsync (id: {id})");
                throw;
            }
        }
        
        public async Task<List<Item>> GetRelatedItemsAsync(int itemId)
        {
            try
            {
                using var context = CreateContext();
                var forwardIds = await context.ItemRelationships
                    .Where(r => r.ItemId == itemId)
                    .Select(r => r.RelatedItemId)
                    .ToListAsync();
                    
                var reverseIds = await context.ItemRelationships
                    .Where(r => r.RelatedItemId == itemId)
                    .Select(r => r.ItemId)
                    .ToListAsync();
                    
                var allIds = forwardIds.Concat(reverseIds).Distinct().ToList();
                
                if (allIds.Count == 0) return new List<Item>();
                return await context.Items.Where(i => allIds.Contains(i.Id)).ToListAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"GetRelatedItemsAsync (itemId: {itemId})");
                throw;
            }
        }
        
        public async Task AddItemRelationshipAsync(int itemId, int relatedItemId)
        {
            try
            {
                using var context = CreateContext();
                var exists = await context.ItemRelationships
                    .AnyAsync(r => (r.ItemId == itemId && r.RelatedItemId == relatedItemId) ||
                                  (r.ItemId == relatedItemId && r.RelatedItemId == itemId));
                
                if (!exists)
                {
                    context.ItemRelationships.Add(new ItemRelationship { ItemId = itemId, RelatedItemId = relatedItemId });
                    await context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"AddItemRelationshipAsync (itemId: {itemId}, relatedItemId: {relatedItemId})");
                throw;
            }
        }
        
        public async Task RemoveItemRelationshipAsync(int itemId, int relatedItemId)
        {
            try
            {
                using var context = CreateContext();
                var relationships = await context.ItemRelationships
                    .Where(r => (r.ItemId == itemId && r.RelatedItemId == relatedItemId) ||
                                (r.ItemId == relatedItemId && r.RelatedItemId == itemId))
                    .ToListAsync();
                
                if (relationships.Any())
                {
                    context.ItemRelationships.RemoveRange(relationships);
                    await context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"RemoveItemRelationshipAsync (itemId: {itemId}, relatedItemId: {relatedItemId})");
                throw;
            }
        }
        
        public async Task SetItemRelationshipsAsync(int itemId, List<int> relatedItemIds)
        {
            try
            {
                using var context = CreateContext();
                var desiredIds = relatedItemIds.Distinct().ToHashSet();
                
                var existingForward = await context.ItemRelationships
                    .Where(r => r.ItemId == itemId)
                    .ToListAsync();
                var existingReverse = await context.ItemRelationships
                    .Where(r => r.RelatedItemId == itemId)
                    .ToListAsync();
                
                var alreadyLinkedViaReverse = existingReverse.Select(r => r.ItemId).ToHashSet();
                
                context.ItemRelationships.RemoveRange(existingForward);
                
                var reverseToRemove = existingReverse.Where(r => !desiredIds.Contains(r.ItemId)).ToList();
                context.ItemRelationships.RemoveRange(reverseToRemove);
                
                foreach (var relatedId in desiredIds)
                {
                    if (!alreadyLinkedViaReverse.Contains(relatedId))
                    {
                        context.ItemRelationships.Add(new ItemRelationship { ItemId = itemId, RelatedItemId = relatedId });
                    }
                }
                
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"SetItemRelationshipsAsync (itemId: {itemId})");
                throw;
            }
        }
        
        public async Task<List<Project>> GetProjectsAsync(
            string? search = null,
            List<string>? itemTypes = null,
            List<string>? itemSubtypes = null,
            int? itemId = null)
        {
            try
            {
                using var context = CreateContext();
                var query = context.Projects
                    .Include(p => p.ProjectItems)
                    .ThenInclude(pi => pi.Item)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                    query = query.Where(p => p.Name.ToLower().Contains(search.ToLower()));

                // Specific item is the most precise filter - skip type/subtype when it's set
                if (itemId.HasValue)
                {
                    query = query.Where(p => p.ProjectItems.Any(pi => pi.ItemId == itemId.Value));
                }
                else
                {
                    if (itemTypes != null && itemTypes.Count > 0)
                    {
                        var types = itemTypes.Select(t => t.ToLower()).ToList();
                        query = query.Where(p =>
                            p.ProjectItems.Any(pi =>
                                pi.Item != null && pi.Item.Type != null &&
                                types.Contains(pi.Item.Type.ToLower())));
                    }
                }

                var results = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();

                // Subtype filter applied in-memory (EF can't translate Split/Trim inside expressions)
                // Only applies when no specific item is selected
                if (!itemId.HasValue && itemSubtypes != null && itemSubtypes.Count > 0)
                {
                    var subs = itemSubtypes.Select(s => s.ToLower()).ToList();
                    results = results.Where(p =>
                        p.ProjectItems.Any(pi =>
                            pi.Item?.Subtype != null &&
                            pi.Item.Subtype.Split(',')
                                .Select(part => part.Trim().ToLower())
                                .Any(part => subs.Contains(part))))
                        .ToList();
                }

                return results;
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "GetProjectsAsync");
                throw;
            }
        }
        
        public async Task<Project?> GetProjectAsync(int id)
        {
            try
            {
                using var context = CreateContext();
                return await context.Projects
                    .Include(p => p.ProjectItems)
                    .ThenInclude(pi => pi.Item)
                    .FirstOrDefaultAsync(p => p.Id == id);
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"GetProjectAsync (id: {id})");
                throw;
            }
        }
        
        public async Task<Project> CreateProjectAsync(Project project, List<(int ItemId, decimal? AmountUsed)> itemUsages)
        {
            try
            {
                using var context = CreateContext();
                project.CreatedAt = DateTime.Now;
                context.Projects.Add(project);
                await context.SaveChangesAsync();
                
                for (int i = 0; i < itemUsages.Count; i++)
                {
                    var (itemId, amountUsed) = itemUsages[i];
                    context.ProjectItems.Add(new ProjectItem
                    {
                        ProjectId = project.Id,
                        ItemId = itemId,
                        AmountUsedPerCreation = amountUsed,
                        SortOrder = i
                    });
                }
                await context.SaveChangesAsync();

                return project;
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"CreateProjectAsync (name: {project.Name})");
                throw;
            }
        }
        
        public async Task UpdateProjectAsync(Project project, List<(int ItemId, decimal? AmountUsed)> itemUsages)
        {
            try
            {
                using var context = CreateContext();
                var existing = await context.Projects.FindAsync(project.Id);
                if (existing == null) return;

                existing.Name = project.Name;
                existing.Description = project.Description;
                existing.ImageUrl = project.ImageUrl;
                existing.Technique = project.Technique;
                existing.Notes = project.Notes;
                await context.SaveChangesAsync();

                var existingLinks = context.ProjectItems.Where(pi => pi.ProjectId == project.Id);
                context.ProjectItems.RemoveRange(existingLinks);
                await context.SaveChangesAsync();

                for (int i = 0; i < itemUsages.Count; i++)
                {
                    var (itemId, amountUsed) = itemUsages[i];
                    context.ProjectItems.Add(new ProjectItem
                    {
                        ProjectId = project.Id,
                        ItemId = itemId,
                        AmountUsedPerCreation = amountUsed,
                        SortOrder = i
                    });
                }
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"UpdateProjectAsync (id: {project.Id})");
                throw;
            }
        }

        public async Task ClearProjectImagesAsync(int projectId)
        {
            try
            {
                using var context = CreateContext();
                var images = context.ProjectImages.Where(pi => pi.ProjectId == projectId);
                context.ProjectImages.RemoveRange(images);
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"ClearProjectImagesAsync (projectId: {projectId})");
                throw;
            }
        }

        public async Task DeleteProjectAsync(int id)
        {
            try
            {
                using var context = CreateContext();
                var project = await context.Projects.FindAsync(id);
                if (project != null)
                {
                    context.Projects.Remove(project);
                    await context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"DeleteProjectAsync (id: {id})");
                throw;
            }
        }
        
        // ── Card Build Wizard helpers ─────────────────────────────────────────

        /// <summary>
        /// Returns lightweight item options for wizard dropdowns, optionally filtered by name, type, or subtype.
        /// </summary>
        public async Task<List<WizardItemOption>> GetWizardItemsAsync(
            string? nameContains = null, string? type = null, string? subtype = null,
            bool typeContains = false, bool subtypeContains = false)
        {
            try
            {
                using var context = CreateContext();
                var query = context.Items.AsNoTracking().AsQueryable();
                if (!string.IsNullOrEmpty(nameContains))
                    query = query.Where(i => i.Name.ToLower().Contains(nameContains.ToLower()));
                if (!string.IsNullOrEmpty(type))
                {
                    if (typeContains)
                        query = query.Where(i => i.Type.ToLower().Contains(type.ToLower()));
                    else
                        query = query.Where(i => i.Type == type);
                }
                if (!string.IsNullOrEmpty(subtype))
                {
                    if (subtypeContains)
                        query = query.Where(i => i.Subtype != null && i.Subtype.ToLower().Contains(subtype.ToLower()));
                    else
                        query = query.Where(i => i.Subtype == subtype);
                }
                return await query.OrderBy(i => i.Name)
                    .Select(i => new WizardItemOption { Id = i.Id, Name = i.Name, ItemType = i.Type, Subtype = i.Subtype, StencilLayers = i.StencilLayers, ImageUrl = i.ImageUrl })
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "GetWizardItemsAsync");
                return new List<WizardItemOption>();
            }
        }

        /// <summary>Returns stamps and dies matching the given theme for the sentiment picker.</summary>
        public async Task<List<WizardItemOption>> GetWizardSentimentItemsByThemeAsync(string theme)
        {
            try
            {
                using var context = CreateContext();
                return await context.Items.AsNoTracking()
                    .Where(i => (i.Type.Contains("Stamp") || i.Type.Contains("Die")) &&
                                i.Theme != null && i.Theme.Contains(theme))
                    .OrderBy(i => i.Name)
                    .Select(i => new WizardItemOption
                    {
                        Id = i.Id, Name = i.Name, ItemType = i.Type,
                        Subtype = i.Subtype, StencilLayers = i.StencilLayers
                    })
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "GetWizardSentimentItemsByThemeAsync");
                return new List<WizardItemOption>();
            }
        }

        /// <summary>
        /// Returns stacklets compatible with the given card base. Pass null types for Fancy Fold (show all).
        /// </summary>
        public async Task<List<WizardItemOption>> GetWizardStackletsAsync(List<string>? compatibleTypes = null)
        {
            try
            {
                using var context = CreateContext();
                var query = context.Items.AsNoTracking().AsQueryable();
                if (compatibleTypes != null && compatibleTypes.Count > 0)
                    query = query.Where(i => compatibleTypes.Contains(i.Type));
                else
                    query = query.Where(i =>
                        i.Type == "Stacklets - A2" ||
                        i.Type == "Stacklets - A7" ||
                        i.Type == "Stacklets - Mini Slim");
                return await query.OrderBy(i => i.Name)
                    .Select(i => new WizardItemOption { Id = i.Id, Name = i.Name, ItemType = i.Type, Subtype = i.Subtype, StencilLayers = i.StencilLayers, ImageUrl = i.ImageUrl })
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "GetWizardStackletsAsync");
                return new List<WizardItemOption>();
            }
        }

        /// <summary>
        /// Returns all dies for a specific stacklet item from the stacklet_dies table.
        /// </summary>
        public async Task<List<WizardDieOption>> GetStackletDiesAsync(int itemId)
        {
            try
            {
                using var context = CreateContext();
                return await context.StackletDies.AsNoTracking()
                    .Where(d => d.ItemId == itemId)
                    .OrderBy(d => d.DieNumber)
                    .Select(d => new WizardDieOption
                    {
                        Id = d.Id,
                        DieNumber = d.DieNumber,
                        Label = d.Label ?? $"Die {d.DieNumber}",
                        Width = d.Width,
                        Height = d.Height
                    })
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "GetStackletDiesAsync");
                return new List<WizardDieOption>();
            }
        }

        /// <summary>
        /// Returns all item types filtered by the given list (for the focal mat type picker).
        /// </summary>
        public async Task<List<string>> GetAllItemTypesAsync()
        {
            try
            {
                using var context = CreateContext();
                return await context.Items.AsNoTracking()
                    .Select(i => i.Type)
                    .Distinct()
                    .OrderBy(t => t)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "GetAllItemTypesAsync");
                return new List<string>();
            }
        }

        /// <summary>
        /// Returns distinct Combo Club edition labels (the portion after "Combo Club ")
        /// derived from items whose theme contains "Combo Club". E.g. "2024", "2025".
        /// </summary>
        public async Task<List<string>> GetComboClubEditionsAsync()
        {
            try
            {
                using var context = CreateContext();
                var themes = await context.Items.AsNoTracking()
                    .Where(i => i.Theme != null && i.Theme.Contains("Combo Club"))
                    .Select(i => i.Theme!)
                    .ToListAsync();

                const string prefix = "Combo Club ";
                var editions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var theme in themes)
                {
                    foreach (var part in theme.Split(',').Select(p => p.Trim()))
                    {
                        if (part.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                            && part.Length > prefix.Length)
                            editions.Add(part.Substring(prefix.Length));
                    }
                }
                return editions.OrderBy(e => e).ToList();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "GetComboClubEditionsAsync");
                return new List<string>();
            }
        }

        /// <summary>
        /// Splits an item's Sentiments field into individual lines.
        /// </summary>
        public async Task<List<string>> GetItemSentimentLinesAsync(int itemId)
        {
            try
            {
                using var context = CreateContext();
                var item = await context.Items.AsNoTracking()
                    .Where(i => i.Id == itemId)
                    .Select(i => new { i.Sentiments })
                    .FirstOrDefaultAsync();
                if (item?.Sentiments == null) return new List<string>();
                return SentimentService.ParseSentimentsList(item.Sentiments);
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "GetItemSentimentLinesAsync");
                return new List<string>();
            }
        }

        /// <summary>
        /// Searches items by name or sentiments text. Returns options with a thumbnail
        /// (first sentiment image if available, otherwise item image).
        /// </summary>
        public async Task<List<WizardSentimentResult>> SearchSentimentItemsAsync(string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query)) return new List<WizardSentimentResult>();
                var q = query.ToLower();
                using var context = CreateContext();

                var items = await context.Items.AsNoTracking()
                    .Where(i => i.Name.ToLower().Contains(q) ||
                                (i.Sentiments != null && i.Sentiments.ToLower().Contains(q)))
                    .OrderBy(i => i.Name)
                    .Take(50)
                    .Select(i => new { i.Id, i.Name, i.Sentiments, i.ImageUrl })
                    .ToListAsync();

                // Load first sentiment image per item
                var itemIds = items.Select(i => i.Id).ToList();
                var sentimentImages = await context.SentimentImages.AsNoTracking()
                    .Where(si => itemIds.Contains(si.ItemId))
                    .GroupBy(si => si.ItemId)
                    .Select(g => new { ItemId = g.Key, FirstImage = g.OrderBy(si => si.SortOrder).First() })
                    .ToDictionaryAsync(x => x.ItemId, x => x.FirstImage.ImageData);

                return items.Select(i => new WizardSentimentResult
                {
                    ItemId = i.Id,
                    ItemName = i.Name,
                    SentimentPreview = i.Sentiments != null
                        ? string.Join(" • ", SentimentService.ParseSentimentsList(i.Sentiments).Take(3))
                        : null,
                    ThumbnailBase64 = sentimentImages.TryGetValue(i.Id, out var si) ? si : i.ImageUrl
                }).ToList();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "SearchSentimentItemsAsync");
                return new List<WizardSentimentResult>();
            }
        }

        /// <summary>
        /// Saves a card build and its steps for a project. Replaces any existing build for the project.
        /// </summary>
        public async Task SaveProjectCardBuildAsync(int projectId, string cardBaseType, List<WizardBuildStep> steps, string? stateSnapshot = null)
        {
            try
            {
                using var context = CreateContext();
                // Remove any existing build
                var existing = await context.ProjectCardBuilds
                    .Where(b => b.ProjectId == projectId).ToListAsync();
                context.ProjectCardBuilds.RemoveRange(existing);

                var build = new ProjectCardBuild
                {
                    ProjectId = projectId,
                    CardBaseType = cardBaseType,
                    StateSnapshot = stateSnapshot,
                    CreatedAt = DateTime.Now
                };
                context.ProjectCardBuilds.Add(build);
                await context.SaveChangesAsync();

                int order = 0;
                foreach (var step in steps)
                {
                    context.ProjectCardBuildSteps.Add(new ProjectCardBuildStep
                    {
                        BuildId = build.Id,
                        StepOrder = order++,
                        Section = step.Section,
                        StepType = step.StepType,
                        MatLayer = step.MatLayer,
                        ItemId = step.ItemId,
                        StackletDieId = step.StackletDieId,
                        CuttingMethod = step.CuttingMethod,
                        Label = step.Label
                    });
                }
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "SaveProjectCardBuildAsync");
                throw;
            }
        }

        /// <summary>
        /// Returns the card build (with steps) for a project, or null if none exists.
        /// </summary>
        public async Task<ProjectCardBuild?> GetProjectCardBuildAsync(int projectId)
        {
            try
            {
                using var context = CreateContext();
                return await context.ProjectCardBuilds.AsNoTracking()
                    .Include(b => b.Steps)
                    .Where(b => b.ProjectId == projectId)
                    .OrderByDescending(b => b.CreatedAt)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "GetProjectCardBuildAsync");
                return null;
            }
        }

        public List<string> GetItemTypes()
        {
            try
            {
                // Strip the trailing-backtick "excluded from projects" marker.
                var types = ConfigStore.GetLines(ConfigStore.Types)
                    .Select(line => line.TrimEnd('`').Trim())
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .ToList();
                if (types.Any()) return types;
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "Error loading types");
            }

            return new List<string> { "Stamp", "Die", "Stencil", "Embossing Folder", "Paper", "Ink", "Tool", "Other" };
        }

        public static HashSet<string> GetProjectExcludedItemTypes()
        {
            try
            {
                return ConfigStore.GetLines(ConfigStore.Types)
                    .Where(line => line.TrimEnd().EndsWith("`"))
                    .Select(line => line.TrimStart('*').TrimEnd('`').Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "Error loading project-excluded types");
            }
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public List<string> GetItemLocations()
        {
            try
            {
                var locations = ConfigStore.GetLines(ConfigStore.Locations);
                if (locations.Any()) return locations;
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "Error loading locations");
            }
            return new List<string> { "Miscellaneous" };
        }

        public List<string> GetPurchasedFromOptions()
        {
            try
            {
                var options = ConfigStore.GetLines(ConfigStore.PurchasedFrom);
                if (options.Any()) return options;
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "Error loading purchased-from options");
            }
            return new List<string> { "Amazon", "Local Store", "Online Shop" };
        }

        // Item Images
        public async Task<List<ItemImage>> GetItemImagesAsync(int itemId)
        {
            try
            {
                using var context = CreateContext();
                return await context.ItemImages
                    .Where(i => i.ItemId == itemId)
                    .OrderBy(i => i.SortOrder)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"GetItemImagesAsync (itemId: {itemId})");
                throw;
            }
        }
        
        public async Task<ItemImage> AddItemImageAsync(int itemId, string imageUrl, int sortOrder = 0)
        {
            try
            {
                using var context = CreateContext();
                var image = new ItemImage
                {
                    ItemId = itemId,
                    ImageUrl = imageUrl,
                    SortOrder = sortOrder,
                    CreatedAt = DateTime.Now
                };
                context.ItemImages.Add(image);
                await context.SaveChangesAsync();
                return image;
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"AddItemImageAsync (itemId: {itemId})");
                throw;
            }
        }
        
        public async Task DeleteItemImageAsync(int imageId)
        {
            try
            {
                using var context = CreateContext();
                var image = await context.ItemImages.FindAsync(imageId);
                if (image != null)
                {
                    context.ItemImages.Remove(image);
                    await context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"DeleteItemImageAsync (imageId: {imageId})");
                throw;
            }
        }
        
        public async Task ClearItemImagesAsync(int itemId)
        {
            try
            {
                using var context = CreateContext();
                var images = await context.ItemImages.Where(i => i.ItemId == itemId).ToListAsync();
                context.ItemImages.RemoveRange(images);
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"ClearItemImagesAsync (itemId: {itemId})");
                throw;
            }
        }
        
        // Project Images
        public async Task<List<ProjectImage>> GetProjectImagesAsync(int projectId)
        {
            try
            {
                using var context = CreateContext();
                return await context.ProjectImages
                    .Where(i => i.ProjectId == projectId)
                    .OrderBy(i => i.SortOrder)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"GetProjectImagesAsync (projectId: {projectId})");
                throw;
            }
        }
        
        public async Task<ProjectImage> AddProjectImageAsync(int projectId, string imageUrl, int sortOrder = 0)
        {
            try
            {
                using var context = CreateContext();
                var image = new ProjectImage
                {
                    ProjectId = projectId,
                    ImageUrl = imageUrl,
                    SortOrder = sortOrder,
                    CreatedAt = DateTime.Now
                };
                context.ProjectImages.Add(image);
                await context.SaveChangesAsync();
                return image;
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"AddProjectImageAsync (projectId: {projectId})");
                throw;
            }
        }
        
        public async Task DeleteProjectImageAsync(int imageId)
        {
            try
            {
                using var context = CreateContext();
                var image = await context.ProjectImages.FindAsync(imageId);
                if (image != null)
                {
                    context.ProjectImages.Remove(image);
                    await context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"DeleteProjectImageAsync (imageId: {imageId})");
                throw;
            }
        }
        
        // Item Purchases
        public async Task<List<ItemPurchase>> GetItemPurchasesAsync(int itemId)
        {
            try
            {
                using var context = CreateContext();
                return await context.ItemPurchases
                    .Where(p => p.ItemId == itemId)
                    .OrderByDescending(p => p.DatePurchased)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"GetItemPurchasesAsync (itemId: {itemId})");
                throw;
            }
        }
        
        public async Task<(ItemPurchase Purchase, int TotalQuantity, decimal TotalSpend, Item? UpdatedItem)> AddItemPurchaseAsync(int itemId, int quantity, decimal pricePerItem, DateTime? datePurchased = null)
        {
            try
            {
                using var context = CreateContext();
                var purchase = new ItemPurchase
                {
                    ItemId = itemId,
                    Quantity = quantity,
                    PricePerItem = pricePerItem,
                    DatePurchased = datePurchased,
                    CreatedAt = DateTime.Now
                };
                context.ItemPurchases.Add(purchase);
                await context.SaveChangesAsync();
                
                // Get all purchases to compute totals and find latest price
                var allPurchases = await context.ItemPurchases
                    .Where(p => p.ItemId == itemId)
                    .ToListAsync();
                
                var totalQuantity = allPurchases.Sum(p => p.Quantity);
                var totalSpend = allPurchases.Sum(p => p.Quantity * p.PricePerItem);
                
                // Find the latest purchase by date (or by id if no date). The list
                // contains the row we just added, but a concurrent delete could race
                // it away — fall back to the just-added purchase rather than throwing.
                var latestPurchase = allPurchases
                    .OrderByDescending(p => p.DatePurchased ?? DateTime.MinValue)
                    .ThenByDescending(p => p.Id)
                    .FirstOrDefault() ?? purchase;
                var latestPrice = latestPurchase.PricePerItem;

                // Update item's price to the latest purchase price
                var item = await context.Items.FindAsync(itemId);
                if (item != null)
                {
                    item.Price = latestPrice;
                    item.DatePurchased = latestPurchase.DatePurchased;

                    // For tracked item types, add stock: quantity * packSize sheets/items
                    if (IsTrackedType(item.Type) && item.PackSize.HasValue && item.PackSize.Value > 0)
                    {
                        var stockToAdd = quantity * item.PackSize.Value;
                        item.CurrentStock = (item.CurrentStock ?? 0) + stockToAdd;
                    }

                    await context.SaveChangesAsync();
                }
                
                // Return fresh item for UI binding refresh
                var updatedItem = await GetItemAsync(itemId);
                return (purchase, totalQuantity, totalSpend, updatedItem);
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"AddItemPurchaseAsync (itemId: {itemId})");
                throw;
            }
        }
        
        public async Task<(int TotalQuantity, decimal TotalSpend)> GetItemPurchaseTotalsAsync(int itemId)
        {
            try
            {
                using var context = CreateContext();
                var result = await context.ItemPurchases
                    .Where(p => p.ItemId == itemId)
                    .GroupBy(p => p.ItemId)
                    .Select(g => new
                    {
                        TotalQuantity = g.Sum(p => p.Quantity),
                        TotalSpend = g.Sum(p => p.Quantity * p.PricePerItem)
                    })
                    .FirstOrDefaultAsync();
                
                return result != null ? (result.TotalQuantity, result.TotalSpend) : (0, 0m);
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"GetItemPurchaseTotalsAsync (itemId: {itemId})");
                throw;
            }
        }
        
        public async Task DeleteItemPurchaseAsync(int purchaseId)
        {
            try
            {
                using var context = CreateContext();
                var purchase = await context.ItemPurchases.FindAsync(purchaseId);
                if (purchase == null) return;

                var itemId = purchase.ItemId;
                var deletedQuantity = purchase.Quantity;

                context.ItemPurchases.Remove(purchase);
                await context.SaveChangesAsync();

                // Recompute the item's latest-purchase price/date and reverse stock for tracked items.
                var item = await context.Items.FindAsync(itemId);
                if (item != null)
                {
                    if (IsTrackedType(item.Type) && item.PackSize.HasValue && item.PackSize.Value > 0)
                    {
                        var stockToRemove = deletedQuantity * item.PackSize.Value;
                        item.CurrentStock = Math.Max(0, (item.CurrentStock ?? 0) - stockToRemove);
                    }

                    var remaining = await context.ItemPurchases
                        .Where(p => p.ItemId == itemId)
                        .OrderByDescending(p => p.DatePurchased ?? DateTime.MinValue)
                        .ThenByDescending(p => p.Id)
                        .FirstOrDefaultAsync();

                    if (remaining != null)
                    {
                        item.Price = remaining.PricePerItem;
                        item.DatePurchased = remaining.DatePurchased;
                    }

                    await context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"DeleteItemPurchaseAsync (purchaseId: {purchaseId})");
                throw;
            }
        }

        // ---- Inventory tracking helpers ----

        // ── Tracked-type config ──────────────────────────────────────────────────

        /// <summary>Per-type warning-level thresholds stored in the shared config file.</summary>
        public class TrackedTypeConfig
        {
            public string Type { get; set; } = string.Empty;
            /// <summary>Units at or above this value are considered "good" (green).</summary>
            public int? GoodThreshold { get; set; }
            /// <summary>Units at or above this (but below GoodThreshold) are "low" (yellow).</summary>
            public int? LowThreshold { get; set; }
            /// <summary>Units at or below this value are "out" (red). Defaults to 0.</summary>
            public int? OutThreshold { get; set; }
        }

        public enum StockLevel { Good, Low, Out, Unknown }

        /// <summary>Default tracked types used when no config file exists.</summary>
        public static readonly HashSet<string> DefaultTrackedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Cardstock", "Glitter Cardstock", "Foil Cardstock", "Watercolor",
            "A2 Card Bases", "A7 Card Bases", "Mini Slim Card Bases",
            "A2 Envelopes", "A7 Envelopes", "Mini Slim Envelopes",
            "Foil-its", "Foils"
        };

        private static HashSet<string>? _trackedTypes;
        private static Dictionary<string, TrackedTypeConfig>? _trackedTypeConfigs;

        /// <summary>Currently configured set of tracked types (loaded from shared config file).</summary>
        public static HashSet<string> TrackedTypes
        {
            get
            {
                if (_trackedTypes == null)
                    LoadTrackedConfigFromFile();
                return _trackedTypes!;
            }
        }

        /// <summary>Per-type config including warning thresholds.</summary>
        public static Dictionary<string, TrackedTypeConfig> TrackedTypeConfigs
        {
            get
            {
                if (_trackedTypeConfigs == null)
                    LoadTrackedConfigFromFile();
                return _trackedTypeConfigs!;
            }
        }

        public static void ReloadTrackedTypes()
        {
            _trackedTypes = null;
            _trackedTypeConfigs = null;
            LoadTrackedConfigFromFile();
        }

        private static void LoadTrackedConfigFromFile()
        {
            try
            {
                var json = ConfigStore.GetText(ConfigStore.TrackedTypes);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    // Try new format first: List<TrackedTypeConfig>
                    try
                    {
                        var configs = System.Text.Json.JsonSerializer.Deserialize<List<TrackedTypeConfig>>(json,
                            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (configs != null && configs.Count > 0 && configs[0].Type != null)
                        {
                            _trackedTypeConfigs = configs.ToDictionary(c => c.Type, c => c,
                                StringComparer.OrdinalIgnoreCase);
                            _trackedTypes = new HashSet<string>(_trackedTypeConfigs.Keys, StringComparer.OrdinalIgnoreCase);
                            return;
                        }
                    }
                    catch { }

                    // Fall back to old format: List<string>
                    var list = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
                    if (list != null && list.Count > 0)
                    {
                        _trackedTypes = new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
                        _trackedTypeConfigs = list.ToDictionary(t => t,
                            t => new TrackedTypeConfig { Type = t }, StringComparer.OrdinalIgnoreCase);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "LoadTrackedConfigFromFile");
            }

            // Defaults
            _trackedTypes = new HashSet<string>(DefaultTrackedTypes, StringComparer.OrdinalIgnoreCase);
            _trackedTypeConfigs = DefaultTrackedTypes.ToDictionary(t => t,
                t => new TrackedTypeConfig { Type = t }, StringComparer.OrdinalIgnoreCase);
        }

        public static void SaveTrackedTypes(IEnumerable<string> types,
            Dictionary<string, TrackedTypeConfig>? configs = null)
        {
            try
            {
                var typeList = types.ToList();
                var existingConfigs = TrackedTypeConfigs;

                var merged = typeList.Select(t =>
                {
                    if (configs != null && configs.TryGetValue(t, out var supplied))
                        return supplied;
                    if (existingConfigs.TryGetValue(t, out var existing))
                        return existing;
                    return new TrackedTypeConfig { Type = t };
                }).ToList();

                var json = System.Text.Json.JsonSerializer.Serialize(merged,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                ConfigStore.SetText(ConfigStore.TrackedTypes, json);

                _trackedTypes = null;
                _trackedTypeConfigs = null;
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "SaveTrackedTypes");
                throw;
            }
        }

        // ── Project Tracked Items ──────────────────────────────────────────────

        public class ProjectTrackedConfig
        {
            public string Name { get; set; } = string.Empty;
            public int QuantityOnHand { get; set; }
        }

        private static Dictionary<string, ProjectTrackedConfig>? _projectTrackedConfigs;

        /// <summary>Set of project names the user has chosen to track in the Project Inventory tab.</summary>
        public static HashSet<string> ProjectTrackedItems
        {
            get
            {
                if (_projectTrackedConfigs == null)
                    LoadProjectTrackedItemsFromFile();
                return new HashSet<string>(_projectTrackedConfigs!.Keys, StringComparer.OrdinalIgnoreCase);
            }
        }

        public static Dictionary<string, ProjectTrackedConfig> ProjectTrackedConfigs
        {
            get
            {
                if (_projectTrackedConfigs == null)
                    LoadProjectTrackedItemsFromFile();
                return _projectTrackedConfigs!;
            }
        }

        public static void ReloadProjectTrackedItems()
        {
            _projectTrackedConfigs = null;
            LoadProjectTrackedItemsFromFile();
        }

        private static void LoadProjectTrackedItemsFromFile()
        {
            try
            {
                var json = ConfigStore.GetText(ConfigStore.ProjectTrackedItems);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    // Try rich format: List<ProjectTrackedConfig>
                    try
                    {
                        var configs = System.Text.Json.JsonSerializer.Deserialize<List<ProjectTrackedConfig>>(json,
                            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (configs != null)
                        {
                            _projectTrackedConfigs = configs.ToDictionary(c => c.Name, c => c,
                                StringComparer.OrdinalIgnoreCase);
                            return;
                        }
                    }
                    catch { }
                    // Fall back to old simple list
                    var list = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
                    if (list != null)
                    {
                        _projectTrackedConfigs = list.ToDictionary(n => n,
                            n => new ProjectTrackedConfig { Name = n }, StringComparer.OrdinalIgnoreCase);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "LoadProjectTrackedItemsFromFile");
            }
            _projectTrackedConfigs = new Dictionary<string, ProjectTrackedConfig>(StringComparer.OrdinalIgnoreCase);
        }

        public static void SaveProjectTrackedItems(IEnumerable<string> projectNames,
            Dictionary<string, int>? quantities = null)
        {
            try
            {
                var existing = ProjectTrackedConfigs;
                var list = projectNames.OrderBy(n => n).Select(n =>
                {
                    var qty = quantities != null && quantities.TryGetValue(n, out var q) ? q
                            : existing.TryGetValue(n, out var cfg) ? cfg.QuantityOnHand : 0;
                    return new ProjectTrackedConfig { Name = n, QuantityOnHand = qty };
                }).ToList();

                var json = System.Text.Json.JsonSerializer.Serialize(list,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                ConfigStore.SetText(ConfigStore.ProjectTrackedItems, json);
                _projectTrackedConfigs = null;
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "SaveProjectTrackedItems");
                throw;
            }
        }

        public static void UpdateProjectQuantityOnHand(string projectName, int quantity)
        {
            var configs = ProjectTrackedConfigs;
            if (configs.TryGetValue(projectName, out var cfg))
                cfg.QuantityOnHand = quantity;
            else
                configs[projectName] = new ProjectTrackedConfig { Name = projectName, QuantityOnHand = quantity };

            SaveProjectTrackedItems(configs.Keys, configs.ToDictionary(kv => kv.Key, kv => kv.Value.QuantityOnHand));
        }

        /// <summary>Returns the stock warning level for an item based on its type's configured thresholds.</summary>
        public static StockLevel GetStockLevel(string type, int? currentStock)
        {
            if (currentStock == null) return StockLevel.Unknown;
            if (!TrackedTypeConfigs.TryGetValue(type, out var cfg)) return StockLevel.Unknown;

            int stock = currentStock.Value;
            int outAt  = cfg.OutThreshold  ?? 0;
            int lowAt  = cfg.LowThreshold  ?? -1;
            int goodAt = cfg.GoodThreshold ?? -1;

            if (stock <= outAt) return StockLevel.Out;
            if (lowAt  >= 0 && stock <= lowAt)  return StockLevel.Low;
            if (goodAt >= 0 && stock >= goodAt) return StockLevel.Good;
            return StockLevel.Low; // has stock but no thresholds set
        }

        public static bool IsTrackedType(string type) => TrackedTypes.Contains(type);

        public static bool IsCardstockType(string type) =>
            type.Contains("Cardstock", StringComparison.OrdinalIgnoreCase) ||
            type.Contains("Card Bases", StringComparison.OrdinalIgnoreCase) ||
            type.Contains("Watercolor", StringComparison.OrdinalIgnoreCase);

        public static bool IsEnvelopeType(string type) =>
            type.Contains("Envelope", StringComparison.OrdinalIgnoreCase);

        public async Task UpdateItemStockAsync(int itemId, int newStock)
        {
            try
            {
                using var context = CreateContext();
                var item = await context.Items.FindAsync(itemId);
                if (item != null)
                {
                    item.CurrentStock = newStock;
                    await context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"UpdateItemStockAsync (itemId: {itemId})");
                throw;
            }
        }

        // ---- Project Creations ----

        public async Task<List<ProjectCreation>> GetProjectCreationsAsync(int projectId)
        {
            try
            {
                using var context = CreateContext();
                return await context.ProjectCreations
                    .Where(pc => pc.ProjectId == projectId)
                    .OrderByDescending(pc => pc.CreatedOn)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"GetProjectCreationsAsync (projectId: {projectId})");
                throw;
            }
        }

        public async Task<ProjectCreation> AddProjectCreationAsync(int projectId, string? notes,
            bool subtractMaterials, List<(int ItemId, decimal AmountUsed)> materialUsages)
        {
            try
            {
                using var context = CreateContext();
                var materialsJson = System.Text.Json.JsonSerializer.Serialize(
                    materialUsages.Select(m => new { m.ItemId, m.AmountUsed }));

                var creation = new ProjectCreation
                {
                    ProjectId = projectId,
                    CreatedOn = DateTime.Now,
                    Notes = notes,
                    MaterialsUsed = materialsJson
                };
                context.ProjectCreations.Add(creation);
                await context.SaveChangesAsync();

                if (subtractMaterials)
                {
                    foreach (var (itemId, amountUsed) in materialUsages)
                    {
                        var item = await context.Items.FindAsync(itemId);
                        if (item != null && item.CurrentStock.HasValue)
                        {
                            // amountUsed is fraction-of-sheet for cardstock, or whole units for others
                            // We store CurrentStock in whole sheets/units, so round up fractions
                            int toDeduct = (int)Math.Ceiling((double)amountUsed);
                            item.CurrentStock = Math.Max(0, item.CurrentStock.Value - toDeduct);
                        }
                    }
                    await context.SaveChangesAsync();
                }

                return creation;
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"AddProjectCreationAsync (projectId: {projectId})");
                throw;
            }
        }

        public async Task DeleteProjectCreationAsync(int creationId)
        {
            try
            {
                using var context = CreateContext();
                var creation = await context.ProjectCreations.FindAsync(creationId);
                if (creation != null)
                {
                    context.ProjectCreations.Remove(creation);
                    await context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"DeleteProjectCreationAsync (id: {creationId})");
                throw;
            }
        }
        
        public async Task<InspirationImage> AddInspirationImageAsync(InspirationImage image)
        {
            try
            {
                using var context = CreateContext();
                image.CreatedAt = DateTime.Now;
                context.InspirationImages.Add(image);
                await context.SaveChangesAsync();
                return image;
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "AddInspirationImageAsync");
                throw;
            }
        }
        
        public async Task DeleteInspirationImageAsync(int imageId)
        {
            try
            {
                using var context = CreateContext();
                var image = await context.InspirationImages.FindAsync(imageId);
                if (image != null)
                {
                    context.InspirationImages.Remove(image);
                    await context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"DeleteInspirationImageAsync (imageId: {imageId})");
                throw;
            }
        }

        public async Task<List<InspirationImage>> GetInspirationImagesLightAsync()
        {
            try
            {
                using var context = CreateContext();
                return await context.InspirationImages
                    .AsNoTracking()
                    .Select(i => new InspirationImage
                    {
                        Id = i.Id,
                        Title = i.Title,
                        Notes = i.Notes,
                        CreatedAt = i.CreatedAt
                    })
                    .OrderByDescending(i => i.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "GetInspirationImagesLightAsync");
                return new List<InspirationImage>();
            }
        }

        public async Task<string?> GetInspirationImageUrlAsync(int imageId)
        {
            try
            {
                using var context = CreateContext();
                return await context.InspirationImages
                    .Where(i => i.Id == imageId)
                    .Select(i => i.ImageUrl)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"GetInspirationImageUrlAsync (id: {imageId})");
                return null;
            }
        }

        public async Task<InspirationImage?> GetInspirationImageMetaAsync(int imageId)
        {
            try
            {
                using var context = CreateContext();
                return await context.InspirationImages
                    .AsNoTracking()
                    .Where(i => i.Id == imageId)
                    .Select(i => new InspirationImage
                    {
                        Id = i.Id,
                        Title = i.Title,
                        Notes = i.Notes,
                        CreatedAt = i.CreatedAt,
                        BoardId = i.BoardId,
                        Color = i.Color,
                        Types = i.Types,
                        Theme = i.Theme,
                        Sentiment = i.Sentiment,
                        TeColor = i.TeColor
                    })
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"GetInspirationImageMetaAsync (id: {imageId})");
                return null;
            }
        }

        private static bool _inspirationItemsTableEnsured = false;
        private static readonly SemaphoreSlim _tableEnsureLock = new(1, 1);

        private async Task EnsureInspirationImageItemsOnceAsync()
        {
            if (_inspirationItemsTableEnsured) return;
            await _tableEnsureLock.WaitAsync();
            try
            {
                if (_inspirationItemsTableEnsured) return;
                using var context = CreateContext();
                await context.Database.ExecuteSqlRawAsync(@"
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'inspiration_image_items')
                    BEGIN
                        CREATE TABLE inspiration_image_items (
                            id INT IDENTITY(1,1) PRIMARY KEY,
                            inspiration_image_id INT NOT NULL,
                            item_id INT NOT NULL,
                            FOREIGN KEY (inspiration_image_id) REFERENCES inspiration_images(id) ON DELETE CASCADE,
                            FOREIGN KEY (item_id) REFERENCES items(id) ON DELETE CASCADE
                        )
                    END");
                _inspirationItemsTableEnsured = true;
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "EnsureInspirationImageItemsOnceAsync");
            }
            finally
            {
                _tableEnsureLock.Release();
            }
        }

        public async Task<List<InspirationImageItem>> GetInspirationImageItemsAsync(int inspirationImageId)
        {
            try
            {
                using var context = CreateContext();
                await EnsureInspirationImageItemsOnceAsync();
                return await context.InspirationImageItems
                    .AsNoTracking()
                    .Include(ii => ii.Item)
                    .Where(ii => ii.InspirationImageId == inspirationImageId)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"GetInspirationImageItemsAsync (imageId: {inspirationImageId})");
                return new List<InspirationImageItem>();
            }
        }

        public async Task SetInspirationImageItemsAsync(int inspirationImageId, List<int> itemIds)
        {
            try
            {
                using var context = CreateContext();
                await EnsureInspirationImageItemsOnceAsync();
                var existing = await context.InspirationImageItems
                    .Where(ii => ii.InspirationImageId == inspirationImageId)
                    .ToListAsync();
                context.InspirationImageItems.RemoveRange(existing);

                foreach (var itemId in itemIds)
                {
                    context.InspirationImageItems.Add(new InspirationImageItem
                    {
                        InspirationImageId = inspirationImageId,
                        ItemId = itemId
                    });
                }
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"SetInspirationImageItemsAsync (imageId: {inspirationImageId})");
                throw;
            }
        }

        public async Task<List<Item>> GetItemsLightForSearchAsync(string? search = null, string? type = null, string? theme = null)
        {
            try
            {
                using var context = CreateContext();
                var query = context.Items.AsNoTracking().AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.ToLower();
                    query = query.Where(i =>
                        i.Name.ToLower().Contains(s) ||
                        (i.ItemNumber != null && i.ItemNumber.ToLower().Contains(s)));
                }

                if (!string.IsNullOrWhiteSpace(type))
                {
                    query = query.Where(i => i.Type == type);
                }

                if (!string.IsNullOrWhiteSpace(theme))
                {
                    query = query.Where(i => i.Theme != null && i.Theme.Contains(theme));
                }

                return await query
                    .Select(i => new Item
                    {
                        Id = i.Id,
                        Name = i.Name,
                        Type = i.Type,
                        ItemNumber = i.ItemNumber,
                        Theme = i.Theme
                    })
                    .OrderBy(i => i.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "GetItemsLightForSearchAsync");
                return new List<Item>();
            }
        }

        public async Task<List<int>> GetInspirationImageIdsByItemFilterAsync(string? type, string? theme)
        {
            try
            {
                using var context = CreateContext();
                await EnsureInspirationImageItemsOnceAsync();
                var query = context.InspirationImageItems
                    .AsNoTracking()
                    .Include(ii => ii.Item)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(type))
                {
                    query = query.Where(ii => ii.Item.Type == type);
                }

                if (!string.IsNullOrWhiteSpace(theme))
                {
                    query = query.Where(ii => ii.Item.Theme != null && ii.Item.Theme.Contains(theme));
                }

                return await query
                    .Select(ii => ii.InspirationImageId)
                    .Distinct()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "GetInspirationImageIdsByItemFilterAsync");
                return new List<int>();
            }
        }

        public async Task<List<int>> GetItemIdsForInspirationImageAsync(int inspirationImageId)
        {
            try
            {
                using var context = CreateContext();
                await EnsureInspirationImageItemsOnceAsync();
                return await context.InspirationImageItems
                    .Where(ii => ii.InspirationImageId == inspirationImageId)
                    .Select(ii => ii.ItemId)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"GetItemIdsForInspirationImageAsync (imageId: {inspirationImageId})");
                return new List<int>();
            }
        }

        // ── Inspiration Boards ───────────────────────────────────────────────

        // Schema is owned by EF Core migrations now; this stub keeps existing
        // call sites compiling without forcing a multi-file refactor.
        private static Task EnsureInspirationBoardsTableAsync() => Task.CompletedTask;

        public async Task<List<InspirationBoard>> GetBoardsAtLevelAsync(int? parentBoardId)
        {
            try
            {
                await EnsureInspirationBoardsTableAsync();
                using var context = CreateContext();
                return await context.InspirationBoards
                    .AsNoTracking()
                    .Where(b => b.ParentBoardId == parentBoardId)
                    .OrderBy(b => b.DisplayOrder)
                    .ThenBy(b => b.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "GetBoardsAtLevelAsync");
                return new List<InspirationBoard>();
            }
        }

        public async Task<InspirationBoard?> GetBoardAsync(int boardId)
        {
            try
            {
                await EnsureInspirationBoardsTableAsync();
                using var context = CreateContext();
                return await context.InspirationBoards.AsNoTracking()
                    .FirstOrDefaultAsync(b => b.Id == boardId);
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"GetBoardAsync ({boardId})");
                return null;
            }
        }

        public async Task<InspirationBoard> CreateBoardAsync(string name, string? description, int? parentBoardId,
            string? defaultTypes = null, string? defaultThemes = null, string? defaultColors = null,
            string? defaultSentiment = null, string? defaultTeColors = null, string? defaultSubtypes = null,
            string? defaultItemIds = null)
        {
            try
            {
                await EnsureInspirationBoardsTableAsync();
                using var context = CreateContext();
                var board = new InspirationBoard
                {
                    Name = name.Trim(),
                    Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                    ParentBoardId = parentBoardId,
                    CreatedAt = DateTime.Now,
                    DefaultTypes = string.IsNullOrWhiteSpace(defaultTypes) ? null : defaultTypes,
                    DefaultThemes = string.IsNullOrWhiteSpace(defaultThemes) ? null : defaultThemes,
                    DefaultColors = string.IsNullOrWhiteSpace(defaultColors) ? null : defaultColors,
                    DefaultSentiment = string.IsNullOrWhiteSpace(defaultSentiment) ? null : defaultSentiment,
                    DefaultTeColors = string.IsNullOrWhiteSpace(defaultTeColors) ? null : defaultTeColors,
                    DefaultSubtypes = string.IsNullOrWhiteSpace(defaultSubtypes) ? null : defaultSubtypes,
                    DefaultItemIds = string.IsNullOrWhiteSpace(defaultItemIds) ? null : defaultItemIds,
                };
                context.InspirationBoards.Add(board);
                await context.SaveChangesAsync();
                return board;
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "CreateBoardAsync");
                throw;
            }
        }

        public async Task UpdateBoardAsync(int boardId, string name, string? description,
            string? defaultTypes = null, string? defaultThemes = null, string? defaultColors = null,
            string? defaultSentiment = null, string? defaultTeColors = null, string? defaultSubtypes = null,
            string? defaultItemIds = null)
        {
            try
            {
                await EnsureInspirationBoardsTableAsync();
                using var context = CreateContext();
                var board = await context.InspirationBoards.FindAsync(boardId);
                if (board != null)
                {
                    board.Name = name.Trim();
                    board.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
                    board.DefaultTypes = string.IsNullOrWhiteSpace(defaultTypes) ? null : defaultTypes;
                    board.DefaultThemes = string.IsNullOrWhiteSpace(defaultThemes) ? null : defaultThemes;
                    board.DefaultColors = string.IsNullOrWhiteSpace(defaultColors) ? null : defaultColors;
                    board.DefaultSentiment = string.IsNullOrWhiteSpace(defaultSentiment) ? null : defaultSentiment;
                    board.DefaultTeColors = string.IsNullOrWhiteSpace(defaultTeColors) ? null : defaultTeColors;
                    board.DefaultSubtypes = string.IsNullOrWhiteSpace(defaultSubtypes) ? null : defaultSubtypes;
                    board.DefaultItemIds = string.IsNullOrWhiteSpace(defaultItemIds) ? null : defaultItemIds;
                    await context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"UpdateBoardAsync ({boardId})");
                throw;
            }
        }

        public async Task<List<WizardItemOption>> GetWizardItemsByIdsAsync(IEnumerable<int> ids)
        {
            var idList = ids.ToList();
            if (idList.Count == 0) return new();
            try
            {
                using var context = CreateContext();
                return await context.Items.AsNoTracking()
                    .Where(i => idList.Contains(i.Id))
                    .OrderBy(i => i.Name)
                    .Select(i => new WizardItemOption { Id = i.Id, Name = i.Name, ItemType = i.Type, Subtype = i.Subtype })
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "GetWizardItemsByIdsAsync");
                return new();
            }
        }

        public async Task DeleteBoardAsync(int boardId)
        {
            try
            {
                await EnsureInspirationBoardsTableAsync();
                using var context = CreateContext();
                var board = await context.InspirationBoards.FindAsync(boardId);
                if (board == null) return;

                // Move images in this board to unassigned
                var images = await context.InspirationImages
                    .Where(i => i.BoardId == boardId).ToListAsync();
                foreach (var img in images) img.BoardId = null;

                // Promote child boards to this board's parent
                var children = await context.InspirationBoards
                    .Where(b => b.ParentBoardId == boardId).ToListAsync();
                foreach (var child in children) child.ParentBoardId = board.ParentBoardId;

                context.InspirationBoards.Remove(board);
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"DeleteBoardAsync ({boardId})");
                throw;
            }
        }

        public async Task<List<InspirationBoard>> GetBoardPathAsync(int boardId)
        {
            try
            {
                await EnsureInspirationBoardsTableAsync();
                using var context = CreateContext();
                var path = new List<InspirationBoard>();
                var current = await context.InspirationBoards.AsNoTracking()
                    .FirstOrDefaultAsync(b => b.Id == boardId);
                int guard = 0;
                while (current != null && guard++ < 20)
                {
                    path.Insert(0, current);
                    if (current.ParentBoardId == null) break;
                    current = await context.InspirationBoards.AsNoTracking()
                        .FirstOrDefaultAsync(b => b.Id == current.ParentBoardId);
                }
                return path;
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"GetBoardPathAsync ({boardId})");
                return new List<InspirationBoard>();
            }
        }

        public async Task<List<InspirationBoard>> GetAllBoardsFlatAsync()
        {
            try
            {
                await EnsureInspirationBoardsTableAsync();
                using var context = CreateContext();
                return await context.InspirationBoards
                    .AsNoTracking()
                    .OrderBy(b => b.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "GetAllBoardsFlatAsync");
                return new List<InspirationBoard>();
            }
        }

        public async Task<List<InspirationImage>> GetImagesForBoardLightAsync(int? boardId)
        {
            try
            {
                await EnsureInspirationBoardsTableAsync();
                using var context = CreateContext();
                return await context.InspirationImages
                    .AsNoTracking()
                    .Where(i => i.BoardId == boardId)
                    .Select(i => new InspirationImage
                    {
                        Id = i.Id,
                        Title = i.Title,
                        CreatedAt = i.CreatedAt,
                        BoardId = i.BoardId
                    })
                    .OrderByDescending(i => i.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"GetImagesForBoardLightAsync (boardId: {boardId})");
                return new List<InspirationImage>();
            }
        }

        public async Task MoveImageToBoardAsync(int imageId, int? boardId)
        {
            try
            {
                await EnsureInspirationBoardsTableAsync();
                using var context = CreateContext();
                var image = await context.InspirationImages.FindAsync(imageId);
                if (image != null)
                {
                    image.BoardId = boardId;

                    // Apply target board's defaults to this image
                    if (boardId.HasValue)
                    {
                        var board = await context.InspirationBoards.AsNoTracking()
                            .FirstOrDefaultAsync(b => b.Id == boardId.Value);
                        if (board != null)
                        {
                            if (!string.IsNullOrEmpty(board.DefaultTypes)) image.Types = board.DefaultTypes;
                            if (!string.IsNullOrEmpty(board.DefaultThemes)) image.Theme = board.DefaultThemes;
                            if (!string.IsNullOrEmpty(board.DefaultColors)) image.Color = board.DefaultColors;
                            if (!string.IsNullOrEmpty(board.DefaultSentiment)) image.Sentiment = board.DefaultSentiment;
                            if (!string.IsNullOrEmpty(board.DefaultTeColors)) image.TeColor = board.DefaultTeColors;
                        }
                    }

                    await context.SaveChangesAsync();

                    // Link board's default specific items (ink, cardstock, etc.) to this image
                    if (boardId.HasValue)
                    {
                        var boardForItems = await context.InspirationBoards.AsNoTracking()
                            .FirstOrDefaultAsync(b => b.Id == boardId.Value);
                        if (!string.IsNullOrEmpty(boardForItems?.DefaultItemIds))
                            await AddDefaultItemsToImageAsync(image.Id, boardForItems.DefaultItemIds);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"MoveImageToBoardAsync (imageId: {imageId}, boardId: {boardId})");
                throw;
            }
        }

        /// <summary>Returns IDs of boardId plus all descendant boards (BFS).</summary>
        private async Task<List<int>> GetBoardSubtreeIdsAsync(InventoryDbContext context, int boardId)
        {
            var result = new List<int> { boardId };
            var queue = new Queue<int>(new[] { boardId });
            int guard = 0;
            while (queue.Count > 0 && guard++ < 200)
            {
                var current = queue.Dequeue();
                var children = await context.InspirationBoards
                    .Where(b => b.ParentBoardId == current)
                    .Select(b => b.Id)
                    .ToListAsync();
                foreach (var c in children) { result.Add(c); queue.Enqueue(c); }
            }
            return result;
        }

        private async Task AddDefaultItemsToImageAsync(int imageId, string defaultItemIds)
        {
            var itemIds = defaultItemIds.Split(',')
                .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
                .Where(id => id > 0).ToList();
            if (itemIds.Count == 0) return;

            await EnsureInspirationImageItemsOnceAsync();
            using var ctx = CreateContext();
            var existing = await ctx.InspirationImageItems
                .Where(ii => ii.InspirationImageId == imageId)
                .Select(ii => ii.ItemId).ToListAsync();
            bool changed = false;
            foreach (var itemId in itemIds.Where(id => !existing.Contains(id)))
            {
                ctx.InspirationImageItems.Add(new InspirationImageItem { InspirationImageId = imageId, ItemId = itemId });
                changed = true;
            }
            if (changed) await ctx.SaveChangesAsync();
        }

        /// <summary>Overwrites matching fields on every image in boardId and all sub-boards.</summary>
        public async Task CascadeApplyBoardDefaultsAsync(int boardId, InspirationBoard boardDefaults)
        {
            try
            {
                using var context = CreateContext();
                var allBoardIds = await GetBoardSubtreeIdsAsync(context, boardId);
                var images = await context.InspirationImages
                    .Where(i => i.BoardId != null && allBoardIds.Contains(i.BoardId.Value))
                    .ToListAsync();

                foreach (var image in images)
                {
                    if (!string.IsNullOrEmpty(boardDefaults.DefaultTypes)) image.Types = boardDefaults.DefaultTypes;
                    if (!string.IsNullOrEmpty(boardDefaults.DefaultThemes)) image.Theme = boardDefaults.DefaultThemes;
                    if (!string.IsNullOrEmpty(boardDefaults.DefaultColors)) image.Color = boardDefaults.DefaultColors;
                    if (!string.IsNullOrEmpty(boardDefaults.DefaultSentiment)) image.Sentiment = boardDefaults.DefaultSentiment;
                    if (!string.IsNullOrEmpty(boardDefaults.DefaultTeColors)) image.TeColor = boardDefaults.DefaultTeColors;
                }

                if (images.Count > 0)
                    await context.SaveChangesAsync();

                // Link default specific items to all images in the subtree
                if (!string.IsNullOrEmpty(boardDefaults.DefaultItemIds))
                {
                    foreach (var imgId in images.Select(i => i.Id))
                        await AddDefaultItemsToImageAsync(imgId, boardDefaults.DefaultItemIds);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"CascadeApplyBoardDefaultsAsync ({boardId})");
                throw;
            }
        }

        public async Task<(int imageCount, int childBoardCount, int coverImageId)> GetBoardStatsAsync(int boardId)
        {
            try
            {
                await EnsureInspirationBoardsTableAsync();
                using var context = CreateContext();
                var imageCount = await context.InspirationImages.CountAsync(i => i.BoardId == boardId);
                var childBoardCount = await context.InspirationBoards.CountAsync(b => b.ParentBoardId == boardId);
                var coverImageId = await context.InspirationImages
                    .Where(i => i.BoardId == boardId)
                    .OrderByDescending(i => i.CreatedAt)
                    .Select(i => (int?)i.Id)
                    .FirstOrDefaultAsync() ?? 0;
                return (imageCount, childBoardCount, coverImageId);
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"GetBoardStatsAsync ({boardId})");
                return (0, 0, 0);
            }
        }
    }

    // ── Wizard DTOs ───────────────────────────────────────────────────────────

    public class WizardItemOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ItemType { get; set; }
        public string? Subtype { get; set; }
        public int? StencilLayers { get; set; }
        public string? ImageUrl { get; set; }
        public override string ToString() => Subtype != null ? $"{Name} ({Subtype})" : Name;
    }

    public class WizardDieOption
    {
        public int Id { get; set; }
        public int DieNumber { get; set; }
        public string Label { get; set; } = string.Empty;
        public decimal? Width { get; set; }
        public decimal? Height { get; set; }
        public override string ToString() => Label;
    }

    public class WizardSentimentResult
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string? SentimentPreview { get; set; }
        public string? ThumbnailBase64 { get; set; }
        public bool IsSelected { get; set; }
    }

    public record WizardBuildStep(
        string Section,
        string StepType,
        int? MatLayer,
        int? ItemId,
        int? StackletDieId,
        string? CuttingMethod,
        string Label);
}
