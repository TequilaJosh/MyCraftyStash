using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MyCraftyStash.Data;
using MyCraftyStash.Models;

namespace MyCraftyStash.Services
{
    public class SentimentService
    {
        private static InventoryDbContext CreateContext() => new InventoryDbContext();
        
        private static readonly Dictionary<string, string> Contractions = new(StringComparer.OrdinalIgnoreCase)
        {
            { "you're", "you are" },
            { "you are", "you're" },
            { "we're", "we are" },
            { "we are", "we're" },
            { "they're", "they are" },
            { "they are", "they're" },
            { "i'm", "i am" },
            { "i am", "i'm" },
            { "it's", "it is" },
            { "it is", "it's" },
            { "he's", "he is" },
            { "he is", "he's" },
            { "she's", "she is" },
            { "she is", "she's" },
            { "that's", "that is" },
            { "that is", "that's" },
            { "what's", "what is" },
            { "what is", "what's" },
            { "who's", "who is" },
            { "who is", "who's" },
            { "here's", "here is" },
            { "here is", "here's" },
            { "there's", "there is" },
            { "there is", "there's" },
            { "where's", "where is" },
            { "where is", "where's" },
            { "can't", "cannot" },
            { "cannot", "can't" },
            { "couldn't", "could not" },
            { "could not", "couldn't" },
            { "wouldn't", "would not" },
            { "would not", "wouldn't" },
            { "shouldn't", "should not" },
            { "should not", "shouldn't" },
            { "won't", "will not" },
            { "will not", "won't" },
            { "don't", "do not" },
            { "do not", "don't" },
            { "doesn't", "does not" },
            { "does not", "doesn't" },
            { "didn't", "did not" },
            { "did not", "didn't" },
            { "isn't", "is not" },
            { "is not", "isn't" },
            { "aren't", "are not" },
            { "are not", "aren't" },
            { "wasn't", "was not" },
            { "was not", "wasn't" },
            { "weren't", "were not" },
            { "were not", "weren't" },
            { "hasn't", "has not" },
            { "has not", "hasn't" },
            { "haven't", "have not" },
            { "have not", "haven't" },
            { "hadn't", "had not" },
            { "had not", "hadn't" },
            { "let's", "let us" },
            { "let us", "let's" },
            { "i'll", "i will" },
            { "i will", "i'll" },
            { "you'll", "you will" },
            { "you will", "you'll" },
            { "we'll", "we will" },
            { "we will", "we'll" },
            { "they'll", "they will" },
            { "they will", "they'll" },
            { "he'll", "he will" },
            { "he will", "he'll" },
            { "she'll", "she will" },
            { "she will", "she'll" },
            { "it'll", "it will" },
            { "it will", "it'll" },
            { "i've", "i have" },
            { "i have", "i've" },
            { "you've", "you have" },
            { "you have", "you've" },
            { "we've", "we have" },
            { "we have", "we've" },
            { "they've", "they have" },
            { "they have", "they've" },
            { "i'd", "i would" },
            { "i would", "i'd" },
            { "you'd", "you would" },
            { "you would", "you'd" },
            { "we'd", "we would" },
            { "we would", "we'd" },
            { "they'd", "they would" },
            { "they would", "they'd" },
            { "he'd", "he would" },
            { "he would", "he'd" },
            { "she'd", "she would" },
            { "she would", "she'd" },
        };

        private static List<string> GetSearchVariants(string searchText)
        {
            var variants = new List<string> { searchText.ToLowerInvariant() };
            var lower = searchText.ToLowerInvariant();

            foreach (var kvp in Contractions)
            {
                var key = kvp.Key.ToLowerInvariant();
                if (lower.Contains(key))
                {
                    var alt = lower.Replace(key, kvp.Value.ToLowerInvariant());
                    variants.Add(alt);
                }
            }

            return variants.Select(v => NormalizeText(v)).Distinct().ToList();
        }


        public Task<List<SentimentImage>> SearchSentimentsAsync(string searchText)
            => SearchSentimentsAsync(searchText, insidersOnly: false);

        public async Task<List<SentimentImage>> SearchSentimentsAsync(string searchText, bool insidersOnly)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchText))
                    return new List<SentimentImage>();

                using var context = CreateContext();
                var searchVariants = GetSearchVariants(searchText);
                var normalizedSearch = NormalizeText(searchText);

                var baseQuery = context.SentimentImages
                    .AsNoTracking()
                    .Include(s => s.Item)
                    .Where(s => s.SearchText != null);

                if (insidersOnly)
                {
                    baseQuery = baseQuery.Where(s =>
                        s.Item != null && (
                            (s.Item.Type.ToLower() == "cardstock" && s.Item.Subtype != null && s.Item.Subtype.ToLower().Contains("insider"))
                            || s.Item.Name.ToLower().Contains("inside scoop")
                        ));
                }
                
                if (searchVariants.Count <= 1)
                {
                    return await baseQuery
                        .Where(s => s.SearchText!.Contains(normalizedSearch))
                        .Select(s => new SentimentImage
                        {
                            Id = s.Id,
                            ItemId = s.ItemId,
                            ImageData = s.ImageData,
                            ExtractedText = s.ExtractedText,
                            SearchText = s.SearchText,
                            SortOrder = s.SortOrder,
                            CreatedAt = s.CreatedAt,
                            Item = s.Item
                        })
                        .OrderBy(s => s.Item != null ? s.Item.Name : "")
                        .ThenBy(s => s.SortOrder)
                        .ToListAsync();
                }
                
                var candidateResults = await baseQuery
                    .Where(s => s.SearchText!.Contains(normalizedSearch))
                    .Select(s => new SentimentImage
                    {
                        Id = s.Id,
                        ItemId = s.ItemId,
                        ImageData = s.ImageData,
                        ExtractedText = s.ExtractedText,
                        SearchText = s.SearchText,
                        SortOrder = s.SortOrder,
                        CreatedAt = s.CreatedAt,
                        Item = s.Item
                    })
                    .ToListAsync();
                
                var candidateIds = candidateResults.Select(s => s.Id).ToHashSet();
                
                foreach (var variant in searchVariants.Skip(1))
                {
                    var variantTerm = variant;
                    var variantResults = await baseQuery
                        .Where(s => s.SearchText!.Contains(variantTerm) && !candidateIds.Contains(s.Id))
                        .Select(s => new SentimentImage
                        {
                            Id = s.Id,
                            ItemId = s.ItemId,
                            ImageData = s.ImageData,
                            ExtractedText = s.ExtractedText,
                            SearchText = s.SearchText,
                            SortOrder = s.SortOrder,
                            CreatedAt = s.CreatedAt,
                            Item = s.Item
                        })
                        .ToListAsync();
                    
                    foreach (var r in variantResults)
                    {
                        if (candidateIds.Add(r.Id))
                            candidateResults.Add(r);
                    }
                }
                
                var results = candidateResults
                    .Where(s => MatchesSearch(s.SearchText, searchVariants))
                    .OrderBy(s => s.Item?.Name ?? "")
                    .ThenBy(s => s.SortOrder)
                    .ToList();
                
                return results;
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"SearchSentimentsAsync (search: {searchText})");
                throw;
            }
        }
        
        private static bool MatchesSearch(string storedSearchText, List<string> searchVariants)
        {
            if (string.IsNullOrWhiteSpace(storedSearchText) || searchVariants.Count == 0)
                return false;
            
            foreach (var variant in searchVariants)
            {
                var searchTokens = variant.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (searchTokens.Length == 0)
                    continue;

                bool allTokensFound = searchTokens.All(token =>
                    storedSearchText.Contains(token));

                if (allTokensFound)
                    return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Searches by text, then expands results to include ALL sentiments from every matching set.
        /// Within each set, matched sentiments appear first (preserving their sort order),
        /// followed by the remaining sentiments of that set.
        /// Sets are ordered alphabetically by item name.
        /// </summary>
        public Task<List<SentimentImage>> SearchSentimentsExpandedAsync(string searchText)
            => SearchSentimentsExpandedAsync(searchText, insidersOnly: false);

        public async Task<List<SentimentImage>> SearchSentimentsExpandedAsync(string searchText, bool insidersOnly)
        {
            try
            {
                var matched = await SearchSentimentsAsync(searchText, insidersOnly);
                if (matched.Count == 0) return matched;

                var matchedItemIds  = matched.Select(s => s.ItemId).Distinct().ToHashSet();
                var matchedIds      = matched.Select(s => s.Id).ToHashSet();

                using var context = CreateContext();
                var all = await context.SentimentImages
                    .AsNoTracking()
                    .Include(s => s.Item)
                    .Where(s => matchedItemIds.Contains(s.ItemId))
                    .Select(s => new SentimentImage
                    {
                        Id            = s.Id,
                        ItemId        = s.ItemId,
                        ImageData     = s.ImageData,
                        ExtractedText = s.ExtractedText,
                        SearchText    = s.SearchText,
                        SortOrder     = s.SortOrder,
                        CreatedAt     = s.CreatedAt,
                        Item          = s.Item
                    })
                    .ToListAsync();

                // Matched sentiments float to the top within each set
                return all
                    .OrderBy(s => s.Item?.Name ?? "", StringComparer.OrdinalIgnoreCase)
                    .ThenBy(s => matchedIds.Contains(s.Id) ? 0 : 1)
                    .ThenBy(s => s.SortOrder)
                    .ToList();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"SearchSentimentsExpandedAsync (search: {searchText})");
                throw;
            }
        }

        public async Task<List<SentimentImage>> GetSentimentsByItemIdAsync(int itemId)
        {
            try
            {
                using var context = CreateContext();
                return await context.SentimentImages
                    .Where(s => s.ItemId == itemId)
                    .OrderBy(s => s.SortOrder)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"GetSentimentsByItemIdAsync (itemId: {itemId})");
                throw;
            }
        }
        
        public async Task<SentimentImage> AddSentimentImageAsync(int itemId, string imageData, string manualText)
        {
            try
            {
                using var context = CreateContext();

                var extractedText = manualText ?? string.Empty;
                var searchText = NormalizeText(extractedText);
                
                var existingSortOrders = await context.SentimentImages
                    .Where(s => s.ItemId == itemId)
                    .Select(s => s.SortOrder)
                    .ToListAsync();
                var nextSortOrder = existingSortOrders.Count > 0 ? existingSortOrders.Max() + 1 : 1;
                
                var sentiment = new SentimentImage
                {
                    ItemId = itemId,
                    ImageData = imageData,
                    ExtractedText = extractedText,
                    SearchText = searchText,
                    SortOrder = nextSortOrder,
                    CreatedAt = DateTime.Now
                };
                
                context.SentimentImages.Add(sentiment);
                await context.SaveChangesAsync();
                
                LoggingService.LogInfo($"Added sentiment image for item {itemId}: '{extractedText}'");
                return sentiment;
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"AddSentimentImageAsync (itemId: {itemId})");
                throw;
            }
        }
        
        public async Task UpdateSentimentTextAsync(int sentimentId, string newText)
        {
            try
            {
                using var context = CreateContext();
                var sentiment = await context.SentimentImages.FindAsync(sentimentId);
                if (sentiment != null)
                {
                    sentiment.ExtractedText = newText;
                    sentiment.SearchText = NormalizeText(newText);
                    await context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"UpdateSentimentTextAsync (sentimentId: {sentimentId})");
                throw;
            }
        }
        
        public async Task DeleteSentimentImageAsync(int sentimentId)
        {
            try
            {
                using var context = CreateContext();
                var sentiment = await context.SentimentImages.FindAsync(sentimentId);
                if (sentiment != null)
                {
                    context.SentimentImages.Remove(sentiment);
                    await context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"DeleteSentimentImageAsync (sentimentId: {sentimentId})");
                throw;
            }
        }
        
        public async Task DeleteSentimentsByItemIdAsync(int itemId)
        {
            try
            {
                using var context = CreateContext();
                var sentiments = await context.SentimentImages
                    .Where(s => s.ItemId == itemId)
                    .ToListAsync();
                
                if (sentiments.Any())
                {
                    context.SentimentImages.RemoveRange(sentiments);
                    await context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"DeleteSentimentsByItemIdAsync (itemId: {itemId})");
                throw;
            }
        }
        
        public static string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";
            
            var normalized = text.ToLowerInvariant();
            normalized = Regex.Replace(normalized, @"[^a-z0-9'\s]", " ");
            normalized = Regex.Replace(normalized, @"\s+", " ");
            normalized = normalized.Trim();
            
            return normalized;
        }
        
        
        /// <summary>
        /// Splits the stored Sentiments text into one chip per non-empty line, but
        /// treats a `"..."` quoted span as a single chip so chips containing commas
        /// or newlines round-trip without re-splitting. Quote characters are stripped
        /// from the returned chip text.
        /// </summary>
        public static List<string> ParseSentimentLines(string sentimentsText)
        {
            if (string.IsNullOrWhiteSpace(sentimentsText))
                return new List<string>();

            return SplitRespectingQuotes(sentimentsText, new[] { '\r', '\n' })
                .Select(s => s.Trim().Trim('"').Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }

        /// <summary>
        /// Inverse of <see cref="ParseSentimentLines"/>: re-wraps any chip whose text
        /// contains the line/comma separators in `"..."` so a later parse keeps it
        /// intact. Lines join with <see cref="Environment.NewLine"/>.
        /// </summary>
        public static string SerializeSentimentLines(IEnumerable<string> lines)
        {
            if (lines == null) return string.Empty;
            var sep = new[] { '\r', '\n', ',', ';', '|' };
            var encoded = lines
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l =>
                {
                    var trimmed = l.Trim().Trim('"').Trim();
                    return trimmed.IndexOfAny(sep) >= 0 ? "\"" + trimmed + "\"" : trimmed;
                })
                .Where(l => !string.IsNullOrWhiteSpace(l));
            return string.Join(Environment.NewLine, encoded);
        }

        /// <summary>
        /// Split that honors `"..."` quoted spans (their inner separators are skipped).
        /// </summary>
        private static IEnumerable<string> SplitRespectingQuotes(string text, char[] separators)
        {
            if (string.IsNullOrEmpty(text)) yield break;
            var sepSet = new HashSet<char>(separators);
            var sb = new System.Text.StringBuilder();
            bool inQuotes = false;
            foreach (var ch in text)
            {
                if (ch == '"')
                {
                    inQuotes = !inQuotes;
                    sb.Append(ch);
                    continue;
                }
                if (!inQuotes && sepSet.Contains(ch))
                {
                    if (sb.Length > 0) { yield return sb.ToString(); sb.Clear(); }
                    continue;
                }
                sb.Append(ch);
            }
            if (sb.Length > 0) yield return sb.ToString();
        }

        public static List<string> ParseSentimentsList(string sentimentsText)
        {
            if (string.IsNullOrWhiteSpace(sentimentsText))
                return new List<string>();
            
            var separators = new[] { ',', '\n', '\r', ';', '|' };
            var sentiments = sentimentsText
                .Split(separators, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => ToTitleCase(s.Trim()))
                .Where(s => !string.IsNullOrWhiteSpace(s) && s.Length > 1)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            
            return sentiments;
        }

        private static string ToTitleCase(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(input.ToLowerInvariant());
        }
        
        
        public Task<List<Item>> GetItemsWithSentimentsAsync() => GetItemsWithSentimentsAsync(insidersOnly: false);

        public async Task<List<Item>> GetItemsWithSentimentsAsync(bool insidersOnly)
        {
            try
            {
                using var context = CreateContext();
                var query = context.Items.Where(i => !string.IsNullOrEmpty(i.Sentiments));
                if (insidersOnly)
                {
                    query = query.Where(i =>
                        (i.Type.ToLower() == "cardstock" && i.Subtype != null && i.Subtype.ToLower().Contains("insider"))
                        || i.Name.ToLower().Contains("inside scoop"));
                }
                return await query.OrderBy(i => i.Name).ToListAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"GetItemsWithSentimentsAsync (insidersOnly: {insidersOnly})");
                throw;
            }
        }

        public async Task<Dictionary<int, int>> GetSentimentCountsByItemAsync()
        {
            try
            {
                using var context = CreateContext();
                var groups = await context.SentimentImages
                    .GroupBy(s => s.ItemId)
                    .Select(g => new { ItemId = g.Key, Count = g.Count() })
                    .ToListAsync();
                return groups.ToDictionary(x => x.ItemId, x => x.Count);
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "GetSentimentCountsByItemAsync");
                throw;
            }
        }
    }
}
