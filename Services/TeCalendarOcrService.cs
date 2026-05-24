using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using JandH.Core.Models;
using JandH.Core.Data;
using MyCraftyStash.Data;
using JandH.Core.Models;
using JandH.Core.Data;
using MyCraftyStash.Models;
using JandH.Core.Models;
using JandH.Core.Data;
using JandH.Core.Services.Catalog;
using Tesseract;

using JandH.Core.Services;
using JandH.Core.ViewModels;

namespace MyCraftyStash.Services
{
    /// <summary>
    /// OCR pipeline that reads TE's published monthly-calendar JPEGs (a gallery
    /// of MONTH YYYY.jpg images on the storefront homepage) and extracts per-day
    /// store hours, special status banners (TE OFFICE CLOSED / OPEN TODAY), and
    /// events that have dates baked into the calendar image but not into the
    /// /classes page text.
    ///
    /// Strategy: download each gallery image, run Tesseract once per image with
    /// the word-level iterator, then map each detected word to a calendar cell
    /// using calibrated row/column boundaries. Day numbers in the image are
    /// unreliable when isolated (e.g. "17" reads as "7", "11" as "nN") so we
    /// compute the date from (month + row + column) arithmetic instead.
    /// </summary>
    public class TeCalendarOcrService
    {
        // Reads stale-after window matches the other TE scraper.
        public static readonly TimeSpan FreshFor = TimeSpan.FromHours(20);

        // Cropping calibrated against 2000x1414 source images. If TE changes the
        // template these will need to be re-derived (or auto-detected).
        private static readonly int[] ColEdges = { 88, 348, 607, 867, 1127, 1387, 1648, 1909 };
        private static readonly int[] RowEdges = { 248, 430, 612, 794, 976, 1158, 1340 };

        // The "weekday" + month label rows sit above the grid.
        private const int GridTopExclusion = 200;

        private SettingsDbContext CreateContext() => new SettingsDbContext();

        // ── Public API ───────────────────────────────────────────────────────

        public List<TeDailyCalendarCache> GetCachedForRange(DateTime from, DateTime to)
        {
            try
            {
                using var ctx = CreateContext();
                var fromD = from.Date;
                var toD = to.Date;
                return ctx.TeDailyCalendar.AsNoTracking()
                    .Where(d => d.Date >= fromD && d.Date <= toD)
                    .OrderBy(d => d.Date)
                    .ToList();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "TeCalendarOcrService.GetCachedForRange");
                return new List<TeDailyCalendarCache>();
            }
        }

        public bool IsStale()
        {
            try
            {
                using var ctx = CreateContext();
                var newest = ctx.TeDailyCalendar.AsNoTracking()
                    .OrderByDescending(d => d.FetchedAt)
                    .Select(d => (DateTime?)d.FetchedAt)
                    .FirstOrDefault();
                if (newest == null) return true;
                return DateTime.UtcNow - newest.Value > FreshFor;
            }
            catch { return true; }
        }

        public Task RefreshIfStaleAsync(CancellationToken ct = default)
            => Task.Run(async () =>
            {
                try
                {
                    if (!IsStale()) return;
                    await RefreshAsync(ct);
                }
                catch (Exception ex)
                {
                    LoggingService.LogError(ex, "TeCalendarOcrService.RefreshIfStaleAsync");
                }
            }, ct);

        public async Task RefreshAsync(CancellationToken ct = default)
        {
            // 1. Pull the homepage and find the monthly calendar image URLs.
            List<(string Name, string Url)> images;
            try
            {
                using var resp = await CatalogHttpClient.Instance.GetAsync(TeEventScraperService.SiteUrl, ct);
                resp.EnsureSuccessStatusCode();
                var html = await resp.Content.ReadAsStringAsync(ct);
                images = ExtractCalendarImageUrls(html);
                LoggingService.LogInfo($"TeCalendarOcrService: found {images.Count} calendar images on homepage");
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "TeCalendarOcrService.RefreshAsync.fetchHomepage");
                return;
            }
            if (images.Count == 0) return;

            // 2. For each month image, download, OCR, parse, and accumulate rows.
            var allEntries = new List<TeDailyCalendarCache>();
            foreach (var (name, url) in images)
            {
                try
                {
                    var entries = await ProcessOneMonthAsync(name, url, ct);
                    allEntries.AddRange(entries);
                    LoggingService.LogInfo($"TeCalendarOcrService: parsed {entries.Count} day entries from {name}");
                }
                catch (Exception ex)
                {
                    LoggingService.LogError(ex, $"TeCalendarOcrService.RefreshAsync.process({name})");
                }
            }
            if (allEntries.Count == 0)
            {
                LoggingService.LogWarning("TeCalendarOcrService: no entries parsed — leaving cache untouched");
                return;
            }

            // 3. Upsert into the cache table. Older months are not purged here —
            // they get cleaned up when their date passes (see ApplyCutoff below).
            try
            {
                using var ctx = CreateContext();
                var cutoff = DateTime.Today.AddMonths(-2);
                var stale = ctx.TeDailyCalendar.Where(d => d.Date < cutoff);
                ctx.TeDailyCalendar.RemoveRange(stale);

                var now = DateTime.UtcNow;
                foreach (var entry in allEntries)
                {
                    var existing = ctx.TeDailyCalendar.FirstOrDefault(d => d.Date == entry.Date);
                    if (existing == null)
                    {
                        entry.FetchedAt = now;
                        ctx.TeDailyCalendar.Add(entry);
                    }
                    else
                    {
                        existing.Hours      = entry.Hours;
                        existing.Status     = entry.Status;
                        existing.EventsJson = entry.EventsJson;
                        existing.FetchedAt  = now;
                    }
                }
                await ctx.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "TeCalendarOcrService.RefreshAsync.save");
            }
        }

        // ── Image discovery ──────────────────────────────────────────────────

        private const string ImageOrigin = "https://taylored-expressions-inc.square.site";

        /// <summary>Find every gallery image whose `name` looks like "MONTH YYYY.jpg".</summary>
        internal static List<(string Name, string Url)> ExtractCalendarImageUrls(string html)
        {
            var root = TeEventScraperService.ExtractBootstrapState(html);
            if (root == null) return new();
            var found = new List<(string, string)>();
            CollectCalendarImages(root, found);
            return found
                .Where(t => Regex.IsMatch(t.Item1,
                    @"^(?:JAN(?:UARY)?|FEB(?:RUARY)?|MAR(?:CH)?|APR(?:IL)?|MAY|JUN(?:E)?|JUL(?:Y)?|AUG(?:UST)?|SEP(?:TEMBER)?|OCT(?:OBER)?|NOV(?:EMBER)?|DEC(?:EMBER)?)\b",
                    RegexOptions.IgnoreCase))
                .ToList();
        }

        private static void CollectCalendarImages(JsonNode node, List<(string, string)> sink)
        {
            switch (node)
            {
                case JsonArray arr:
                    foreach (var c in arr) if (c != null) CollectCalendarImages(c, sink);
                    break;
                case JsonObject obj:
                    var name = obj["name"]?.GetValue<string?>();
                    var src  = obj["source"]?.GetValue<string?>();
                    if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(src))
                    {
                        var url = src!.StartsWith("http") ? src : ImageOrigin + (src.StartsWith("/") ? src : "/" + src);
                        sink.Add((name!, url));
                    }
                    foreach (var p in obj) if (p.Value != null) CollectCalendarImages(p.Value, sink);
                    break;
            }
        }

        // ── Per-month OCR ────────────────────────────────────────────────────

        private async Task<List<TeDailyCalendarCache>> ProcessOneMonthAsync(string name, string url, CancellationToken ct)
        {
            // Download to a temp file — Tesseract works with file paths most reliably.
            var tmp = Path.Combine(Path.GetTempPath(), $"te_cal_{Guid.NewGuid():N}.jpg");
            try
            {
                using (var resp = await CatalogHttpClient.Instance.GetAsync(url, ct))
                {
                    resp.EnsureSuccessStatusCode();
                    await using var fs = File.Create(tmp);
                    await resp.Content.CopyToAsync(fs, ct);
                }
                return OcrOneImage(name, tmp);
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* ignored */ }
            }
        }

        /// <summary>
        /// Locates the tessdata directory at runtime. Tries:
        ///   1. Next to the entry exe (AppContext.BaseDirectory) — the WPF case.
        ///   2. AppContext.BaseDirectory walking up; useful when running tests
        ///      from a tool/script bin that doesn't sit alongside our copies.
        ///   3. The MyCraftyStash assembly's own folder.
        /// Returns the first match, or null if none exist (callers log + abort).
        /// </summary>
        internal static string? ResolveTessdataDir()
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "tessdata"),
                Path.Combine(Path.GetDirectoryName(typeof(TeCalendarOcrService).Assembly.Location) ?? "", "tessdata"),
            };
            foreach (var p in candidates.Distinct())
            {
                if (!string.IsNullOrWhiteSpace(p) && File.Exists(Path.Combine(p, "eng.traineddata"))) return p;
            }
            return null;
        }

        private List<TeDailyCalendarCache> OcrOneImage(string imageName, string imagePath)
        {
            // First pass: derive year/month from the image name. The name is more
            // reliable than the OCR'd header — the header sometimes loses its
            // first letter to font kerning quirks.
            if (!TryParseMonthYearFromName(imageName, out var year, out var month))
            {
                LoggingService.LogWarning($"TeCalendarOcrService: couldn't parse month/year from '{imageName}' — skipping");
                return new();
            }

            // Bucket words into cells.
            var bucket = new Dictionary<(int r, int c), List<(int x, int y, string text)>>();
            var tessdataDir = ResolveTessdataDir();
            if (tessdataDir == null)
            {
                LoggingService.LogError(new FileNotFoundException("tessdata/eng.traineddata not found"), "TeCalendarOcrService.OcrOneImage");
                return new();
            }
            using (var engine = new TesseractEngine(tessdataDir, "eng", EngineMode.Default))
            using (var img = Pix.LoadFromFile(imagePath))
            using (var page = engine.Process(img))
            using (var iter = page.GetIterator())
            {
                iter.Begin();
                do
                {
                    if (!iter.TryGetBoundingBox(PageIteratorLevel.Word, out var rect)) continue;
                    var text = iter.GetText(PageIteratorLevel.Word)?.Trim() ?? string.Empty;
                    if (text.Length == 0) continue;
                    int cx = (rect.X1 + rect.X2) / 2;
                    int cy = (rect.Y1 + rect.Y2) / 2;
                    if (cy < GridTopExclusion) continue;
                    int c = IndexFromBounds(ColEdges, cx);
                    int r = IndexFromBounds(RowEdges, cy);
                    if (c < 0 || r < 0) continue;
                    if (!bucket.TryGetValue((r, c), out var list))
                        bucket[(r, c)] = list = new();
                    list.Add((cx, cy, text));
                } while (iter.Next(PageIteratorLevel.Word));
            }

            // Compute the date for each cell: row 0 contains the weekday columns
            // before May 1 (filled with previous month). Day 1's column is its
            // weekday, and dates increase by 1 across columns and by 7 down rows.
            var firstOfMonth = new DateTime(year, month, 1);
            int firstCol = (int)firstOfMonth.DayOfWeek;  // Sunday=0
            int daysInMonth = DateTime.DaysInMonth(year, month);

            var entries = new List<TeDailyCalendarCache>();
            for (int r = 0; r < 6; r++)
            for (int c = 0; c < 7; c++)
            {
                int dayNum = r * 7 + c - firstCol + 1;
                if (dayNum < 1 || dayNum > daysInMonth) continue;
                if (!bucket.TryGetValue((r, c), out var words)) continue;
                var entry = ParseCell(new DateTime(year, month, dayNum), words);
                if (entry != null) entries.Add(entry);
            }
            return entries;
        }

        // ── Cell parsing ─────────────────────────────────────────────────────

        // "10:00 AM - 4:00 PM" / "10:00 AM - 7:00 PM"
        private static readonly Regex HoursRx = new(
            @"(?<a>\d{1,2}:\d{2})\s*(?<ap>[AP]M)\b.*?(?<b>\d{1,2}:\d{2})\s*(?<bp>[AP]M)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // "1pm - 3pm" / "6pm-8pm" (event time). LOWERCASE am/pm only — the
        // operating hours use uppercase "AM/PM" with colon-separated minutes,
        // so this disambiguates events from store hours sharing the same cell.
        // "l" as a stand-in for digit 1 is OCR noise we accept.
        private static readonly Regex EventTimeRx = new(
            @"(?<a>[\dl]{1,2})(?::[\dl]{2})?\s*(?<ap>am|pm)\s*[-–]\s*(?<b>[\dl]{1,2})(?::[\dl]{2})?\s*(?<bp>am|pm)",
            RegexOptions.Compiled);

        // "OPEN TODAY 10AM - 3PM" (special-Saturday banner). Some renderings
        // out of OCR put TODAY before OPEN if the two words share a baseline,
        // so the trigger phrase is matched permissively in either order.
        private static readonly Regex OpenTodayRx = new(
            @"(?:OPEN\s+TODAY|TODAY\s+OPEN).*?(?<a>\d{1,2})\s*(?<ap>[AP]M)\s*[-–]\s*(?<b>\d{1,2})\s*(?<bp>[AP]M)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Sort words into stable reading order. Tesseract sometimes returns
        /// words with the same Y differing by 1-2px depending on glyph
        /// ascenders; raw ThenBy(y) then ThenBy(x) flips them. Bucket Ys to
        /// 20px lines first so adjacent words stay in left-to-right order.
        /// </summary>
        private static List<(int x, int y, string text)> SortReadingOrder(List<(int x, int y, string text)> words)
            => words.OrderBy(w => w.y / 20).ThenBy(w => w.x).ToList();

        private static TeDailyCalendarCache? ParseCell(DateTime date, List<(int x, int y, string text)> words)
        {
            var sorted = SortReadingOrder(words);
            var joined = string.Join(" ", sorted.Select(w => w.text));

            string? hours = null;
            string? status = null;
            var events = new List<TeDailyEventLine>();

            // 1. Special-open banner takes priority over generic hours, since
            // it both signals OPEN status and supplies its own hours.
            var openMatch = OpenTodayRx.Match(joined);
            if (openMatch.Success)
            {
                status = $"Open: {openMatch.Groups["a"].Value}{openMatch.Groups["ap"].Value.ToLowerInvariant()} - {openMatch.Groups["b"].Value}{openMatch.Groups["bp"].Value.ToLowerInvariant()}";
            }
            else if (Regex.IsMatch(joined, @"\bTE\s+OFFICE\s+CLOSE", RegexOptions.IgnoreCase))
            {
                status = "Closed";
            }

            // 2. Generic operating hours (only when there isn't a special status).
            if (status == null)
            {
                var hMatch = HoursRx.Match(joined);
                if (hMatch.Success)
                {
                    hours = $"{hMatch.Groups["a"].Value} {hMatch.Groups["ap"].Value.ToUpperInvariant()} - {hMatch.Groups["b"].Value} {hMatch.Groups["bp"].Value.ToUpperInvariant()}";
                }
            }

            // 3. Events: each event time pattern indicates a separate event.
            // The descriptive title is whatever non-hours, non-status text shows
            // above the time. Keep it simple: collect words on lines BEFORE the
            // event-time line, joined as the title.
            foreach (Match evMatch in EventTimeRx.Matches(joined))
            {
                // Skip the special-open banner pattern (already consumed)
                if (openMatch.Success && evMatch.Index >= openMatch.Index && evMatch.Index < openMatch.Index + openMatch.Length)
                    continue;

                var timeStr = NormalizeEventTime(evMatch);
                // Pull a title from the words preceding this match's first word.
                var title = ExtractEventTitle(sorted, evMatch);
                if (string.IsNullOrWhiteSpace(title) && timeStr == null) continue;
                events.Add(new TeDailyEventLine { Title = title ?? "TE event", Time = timeStr });
            }

            if (hours == null && status == null && events.Count == 0) return null;

            return new TeDailyCalendarCache
            {
                Date = date,
                Hours = hours,
                Status = status,
                EventsJson = events.Count > 0 ? JsonSerializer.Serialize(events) : null,
            };
        }

        private static string? NormalizeEventTime(Match m)
        {
            if (!m.Success) return null;
            // OCR confuses "1" with lowercase "l" in body text. Normalise.
            var a  = m.Groups["a"].Value.Replace('l', '1').Replace('L', '1');
            var ap = m.Groups["ap"].Value.ToLowerInvariant();
            var b  = m.Groups["b"].Value.Replace('l', '1').Replace('L', '1');
            var bp = m.Groups["bp"].Value.ToLowerInvariant();
            return $"{a}{ap} - {b}{bp}";
        }

        /// <summary>
        /// Pull a short title for an event from the words that sit ABOVE the
        /// time line. Filters out obvious junk (single underscores, ornament
        /// glyphs the OCR mistakes for letters) and capitalises sensibly.
        /// </summary>
        private static string? ExtractEventTitle(List<(int x, int y, string text)> sorted, Match timeMatch)
        {
            // Find the y of the first word in this match by string position.
            // Easier: just take words whose .text is upper-case multi-letter
            // tokens (e.g. "INTERACTIVE", "CARD", "SERIES") or known script
            // fragments ("Shaker", "Cards"). Join in reading order.
            var titleWords = new List<string>();
            foreach (var w in sorted)
            {
                var t = w.text;
                if (t.Length < 2) continue;
                if (Regex.IsMatch(t, @"^\d")) continue;                 // skip pure-number/time fragments
                if (Regex.IsMatch(t, @"^[AP]M$", RegexOptions.IgnoreCase)) continue;
                if (Regex.IsMatch(t, @"^(am|pm)$", RegexOptions.IgnoreCase)) continue;
                if (Regex.IsMatch(t, @"^[-:.]+$")) continue;
                // Drop time-leftover tokens like "Lpm", "lpm", "6pm" (when the
                // hour and suffix glued into one token in the OCR).
                if (Regex.IsMatch(t, @"^[lL\d]{1,2}(am|pm)$", RegexOptions.IgnoreCase)) continue;
                // Drop OPEN TODAY banner tokens — they're status, not titles.
                if (Regex.IsMatch(t, @"^(OPEN|TODAY|CLOSED|OFFICE|TE)$", RegexOptions.IgnoreCase)) continue;
                // Heuristic: keep alpha words 2+ chars. Drop OCR-noise tokens
                // like "ae", "oe", "B" by requiring length>=3 OR all-caps.
                bool keep = (t.Length >= 3 && Regex.IsMatch(t, "[A-Za-z]"))
                            || Regex.IsMatch(t, @"^[A-Z]{2,}$");
                if (!keep) continue;
                titleWords.Add(t.TrimEnd('.', ','));
            }
            if (titleWords.Count == 0) return null;

            // Normalise: lower-case the words then title-case (preserves a
            // pleasing "Interactive Card Series — Shaker Cards" rather than
            // shouty all-caps).
            var joined = string.Join(" ", titleWords);
            joined = Regex.Replace(joined, @"\s+", " ").Trim();
            // Drop dangling single-letter junk.
            joined = Regex.Replace(joined, @"\b[A-Za-z]\b", "").Trim();
            joined = Regex.Replace(joined, @"\s+", " ");
            if (joined.Length == 0) return null;
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(joined.ToLowerInvariant());
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static int IndexFromBounds(int[] bounds, int v)
        {
            if (v < bounds[0] || v >= bounds[^1]) return -1;
            for (int i = 0; i < bounds.Length - 1; i++)
                if (v >= bounds[i] && v < bounds[i + 1]) return i;
            return -1;
        }

        private static readonly Regex MonthNameRx = new(
            @"^(?<m>JAN(?:UARY)?|FEB(?:RUARY)?|MAR(?:CH)?|APR(?:IL)?|MAY|JUN(?:E)?|JUL(?:Y)?|AUG(?:UST)?|SEP(?:TEMBER)?|OCT(?:OBER)?|NOV(?:EMBER)?|DEC(?:EMBER)?)\b\s+(?<y>\d{4})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        internal static bool TryParseMonthYearFromName(string name, out int year, out int month)
        {
            year = 0; month = 0;
            var m = MonthNameRx.Match(name);
            if (!m.Success) return false;
            try
            {
                month = DateTime.ParseExact(m.Groups["m"].Value, m.Groups["m"].Value.Length <= 3 ? "MMM" : "MMMM", CultureInfo.InvariantCulture).Month;
                year = int.Parse(m.Groups["y"].Value);
                return true;
            }
            catch { return false; }
        }
    }
}
