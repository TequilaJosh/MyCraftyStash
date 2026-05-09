using System.Globalization;
using System.IO;
using System.Text;
using MyCraftyStash.Models;

namespace MyCraftyStash.Services
{
    /// <summary>
    /// Reads a CSV of wishlist items. Recognized column headers (case-insensitive):
    /// name, type, item number / item_number / itemnumber, price / est price / estimated price,
    /// priority (Low/Medium/High or 1/2/3), store / purchased from / purchased_from,
    /// url / link, theme, note / notes.
    /// </summary>
    public static class WishlistCsvImportService
    {
        public static List<WishlistItem> Parse(string path)
        {
            var lines = ReadAllLinesPreserveQuotes(path);
            if (lines.Count == 0) return new();

            var header = SplitCsvLine(lines[0]).Select(NormalizeHeader).ToList();
            var items = new List<WishlistItem>();
            for (int r = 1; r < lines.Count; r++)
            {
                if (string.IsNullOrWhiteSpace(lines[r])) continue;
                var fields = SplitCsvLine(lines[r]);
                if (fields.Count == 0) continue;

                var dict = new Dictionary<string, string>();
                for (int c = 0; c < fields.Count && c < header.Count; c++)
                    dict[header[c]] = fields[c];

                var name = Get(dict, "name");
                if (string.IsNullOrWhiteSpace(name)) continue;

                items.Add(new WishlistItem
                {
                    Name          = name.Trim(),
                    Type          = NullIfEmpty(Get(dict, "type")),
                    ItemNumber    = NullIfEmpty(Get(dict, "itemnumber")),
                    Theme         = NullIfEmpty(Get(dict, "theme")),
                    Price         = ParseDecimal(Get(dict, "price")),
                    PurchasedFrom = NullIfEmpty(Get(dict, "store")),
                    Url           = NullIfEmpty(Get(dict, "url")),
                    Notes         = NullIfEmpty(Get(dict, "notes")),
                    Priority      = ParsePriority(Get(dict, "priority")),
                });
            }
            return items;
        }

        private static string NormalizeHeader(string h)
        {
            var s = (h ?? string.Empty).Trim().ToLowerInvariant();
            return s switch
            {
                "item number" or "item_number" => "itemnumber",
                "estimated price" or "est price" or "est. price" or "est_price" => "price",
                "purchased from" or "purchased_from" => "store",
                "link" => "url",
                "note" => "notes",
                _ => s,
            };
        }

        private static string Get(Dictionary<string, string> d, string key) =>
            d.TryGetValue(key, out var v) ? v : string.Empty;

        private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        private static decimal? ParseDecimal(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var clean = s.Trim().TrimStart('$');
            if (decimal.TryParse(clean, NumberStyles.Number, CultureInfo.CurrentCulture, out var v)) return v;
            if (decimal.TryParse(clean, NumberStyles.Number, CultureInfo.InvariantCulture, out v)) return v;
            return null;
        }

        private static int ParsePriority(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 2;
            var t = s.Trim().ToLowerInvariant();
            return t switch
            {
                "high" or "3" => 3,
                "medium" or "med" or "2" => 2,
                "low" or "1" => 1,
                _ => int.TryParse(t, out var n) && n is >= 1 and <= 3 ? n : 2,
            };
        }

        private static List<string> ReadAllLinesPreserveQuotes(string path)
        {
            // Robust read that handles newlines inside quoted fields.
            using var reader = new StreamReader(path, Encoding.UTF8, true);
            var content = reader.ReadToEnd();
            var lines = new List<string>();
            var cur = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < content.Length; i++)
            {
                char ch = content[i];
                if (ch == '"') { inQuotes = !inQuotes; cur.Append(ch); continue; }
                if (!inQuotes && (ch == '\n' || ch == '\r'))
                {
                    if (ch == '\r' && i + 1 < content.Length && content[i + 1] == '\n') i++;
                    lines.Add(cur.ToString());
                    cur.Clear();
                    continue;
                }
                cur.Append(ch);
            }
            if (cur.Length > 0) lines.Add(cur.ToString());
            return lines;
        }

        private static List<string> SplitCsvLine(string line)
        {
            var fields = new List<string>();
            var cur = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else cur.Append(ch);
                }
                else
                {
                    if (ch == ',') { fields.Add(cur.ToString()); cur.Clear(); }
                    else if (ch == '"') inQuotes = true;
                    else cur.Append(ch);
                }
            }
            fields.Add(cur.ToString());
            return fields;
        }
    }
}
