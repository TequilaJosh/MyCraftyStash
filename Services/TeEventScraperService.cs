using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MyCraftyStash.Data;
using MyCraftyStash.Models;
using MyCraftyStash.Services.Catalog;

namespace MyCraftyStash.Services
{
    /// <summary>One row of the shop's weekly hours, as displayed on the calendar.</summary>
    public class TeShopHoursLine
    {
        public string Day  { get; set; } = string.Empty;   // "Monday"
        public string Text { get; set; } = string.Empty;   // "09:00 am - 05:00 pm" or "Closed"
    }

    /// <summary>
    /// Scrapes the Taylored Expressions Square Online site for:
    ///   1) class events (the /classes page) — cached in settings.db.te_events_cache
    ///   2) shop weekly hours (the homepage) — cached in settings.db.kv_settings
    /// so the calendar overlay and the hours panel keep working offline.
    ///
    /// Square Online ships its content as a single big JSON state blob under
    /// `window.__BOOTSTRAP_STATE__ = {...};` in an inline &lt;script&gt;. We
    /// extract that, parse it with System.Text.Json, and walk the tree for
    /// the shapes we care about. The old JSON-LD / heuristic-regex strategies
    /// have been removed — Square doesn't emit JSON-LD events, and the regex
    /// fallback produced too many false positives.
    /// </summary>
    public class TeEventScraperService
    {
        public const string SiteUrl    = "https://taylored-expressions-inc.square.site/";
        public const string ClassesUrl = "https://taylored-expressions-inc.square.site/classes";

        // Where image src paths in figures are relative — we have to prefix
        // them so the WPF image loader can resolve them.
        private const string ImageOrigin = "https://taylored-expressions-inc.square.site";

        // KvSettings keys for the shop-hours cache.
        private const string HoursJsonKey      = "te.shop.hours.v1";
        private const string HoursFetchedAtKey = "te.shop.hours.fetched_at.v1";

        /// <summary>How long a successful fetch is considered fresh. Shorter
        /// windows mean more HTTP traffic; the calendar refreshes once a day
        /// regardless when the user opens the app.</summary>
        public static readonly TimeSpan FreshFor = TimeSpan.FromHours(20);

        private SettingsDbContext CreateContext() => new SettingsDbContext();

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Returns cached events overlapping the given date range
        /// (inclusive). Always cheap — no network — so safe to call from
        /// the calendar render path.</summary>
        public List<TeEventCache> GetCached(DateTime from, DateTime to)
        {
            try
            {
                using var ctx = CreateContext();
                var fromD = from.Date;
                var toD = to.Date;
                return ctx.TeEventsCache.AsNoTracking()
                    .Where(e => e.EventDate >= fromD && e.EventDate <= toD)
                    .OrderBy(e => e.EventDate)
                    .ToList();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "TeEventScraperService.GetCached");
                return new List<TeEventCache>();
            }
        }

        /// <summary>Cached shop weekly hours in Sunday→Saturday order. Empty list
        /// when nothing has been fetched yet. No network. Safe on the UI thread.</summary>
        public List<TeShopHoursLine> GetCachedShopHours()
        {
            try
            {
                using var ctx = CreateContext();
                var row = ctx.KvSettings.AsNoTracking().FirstOrDefault(k => k.Key == HoursJsonKey);
                if (row == null || string.IsNullOrWhiteSpace(row.Value)) return new List<TeShopHoursLine>();
                var lines = JsonSerializer.Deserialize<List<TeShopHoursLine>>(row.Value);
                return lines ?? new List<TeShopHoursLine>();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "TeEventScraperService.GetCachedShopHours");
                return new List<TeShopHoursLine>();
            }
        }

        /// <summary>Fire-and-forget background refresh from the app shell. Logs
        /// failures and leaves the existing cache untouched so the calendar
        /// degrades gracefully when the network is down.</summary>
        public Task RefreshIfStaleAsync(CancellationToken ct = default)
            => Task.Run(async () =>
            {
                try
                {
                    if (!IsStale()) return;
                    await FetchAndCacheAsync(ct);
                }
                catch (Exception ex)
                {
                    LoggingService.LogError(ex, "TeEventScraperService.RefreshIfStaleAsync");
                }
            }, ct);

        public bool IsStale()
        {
            try
            {
                using var ctx = CreateContext();
                var newestEvent = ctx.TeEventsCache.AsNoTracking()
                    .OrderByDescending(e => e.FetchedAt)
                    .Select(e => (DateTime?)e.FetchedAt)
                    .FirstOrDefault();
                var hoursFetched = ParseHoursFetchedAt(ctx);

                // Stale if either cache is empty or older than FreshFor. We
                // refresh both together, so use the older of the two as the
                // staleness gate.
                var newest = (newestEvent, hoursFetched) switch
                {
                    (null, null) => (DateTime?)null,
                    (DateTime a, null) => a,
                    (null, DateTime b) => b,
                    (DateTime a, DateTime b) => a < b ? a : b,
                };
                if (newest == null) return true;
                return DateTime.UtcNow - newest.Value > FreshFor;
            }
            catch
            {
                return true;
            }
        }

        private static DateTime? ParseHoursFetchedAt(SettingsDbContext ctx)
        {
            var row = ctx.KvSettings.AsNoTracking().FirstOrDefault(k => k.Key == HoursFetchedAtKey);
            if (row == null || string.IsNullOrWhiteSpace(row.Value)) return null;
            return DateTime.TryParse(row.Value, null, DateTimeStyles.RoundtripKind, out var d) ? d : null;
        }

        public async Task FetchAndCacheAsync(CancellationToken ct = default)
        {
            // Events and hours are fetched independently so one failure can't
            // prevent the other from succeeding.
            await TryRefreshEventsAsync(ct).ConfigureAwait(false);
            await TryRefreshHoursAsync(ct).ConfigureAwait(false);
        }

        // ── Events refresh ───────────────────────────────────────────────────

        private async Task TryRefreshEventsAsync(CancellationToken ct)
        {
            List<TeEventCache> events;
            try
            {
                using var resp = await CatalogHttpClient.Instance.GetAsync(ClassesUrl, ct);
                resp.EnsureSuccessStatusCode();
                var html = await resp.Content.ReadAsStringAsync(ct);
                events = ParseEventsFromBootstrap(html);
                LoggingService.LogInfo($"TeEventScraperService: parsed {events.Count} events from {ClassesUrl}");
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "TeEventScraperService.TryRefreshEventsAsync.fetch");
                return;
            }

            if (events.Count == 0)
            {
                LoggingService.LogWarning("TeEventScraperService: event parser found no events — leaving cache untouched");
                return;
            }

            try
            {
                using var ctx = CreateContext();
                var now = DateTime.UtcNow;

                // Replace the whole cache on every successful fetch. The
                // parser already dedupes by ExternalId, so a clean reload
                // also clears out any stale rows from earlier app versions
                // (e.g. duplicates from when ExternalId used a non-stable
                // hash).
                ctx.TeEventsCache.RemoveRange(ctx.TeEventsCache);

                foreach (var fetched in events)
                {
                    fetched.FetchedAt = now;
                    ctx.TeEventsCache.Add(fetched);
                }
                await ctx.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "TeEventScraperService.TryRefreshEventsAsync.save");
            }
        }

        // ── Shop-hours refresh ───────────────────────────────────────────────

        private async Task TryRefreshHoursAsync(CancellationToken ct)
        {
            List<TeShopHoursLine> hours;
            try
            {
                using var resp = await CatalogHttpClient.Instance.GetAsync(SiteUrl, ct);
                resp.EnsureSuccessStatusCode();
                var html = await resp.Content.ReadAsStringAsync(ct);
                hours = ParseShopHoursFromBootstrap(html);
                LoggingService.LogInfo($"TeEventScraperService: parsed {hours.Count} shop-hour lines from {SiteUrl}");
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "TeEventScraperService.TryRefreshHoursAsync.fetch");
                return;
            }

            if (hours.Count == 0)
            {
                LoggingService.LogWarning("TeEventScraperService: hours parser found nothing — leaving cache untouched");
                return;
            }

            try
            {
                using var ctx = CreateContext();
                var json = JsonSerializer.Serialize(hours);
                Upsert(ctx, HoursJsonKey, json);
                Upsert(ctx, HoursFetchedAtKey, DateTime.UtcNow.ToString("o"));
                await ctx.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "TeEventScraperService.TryRefreshHoursAsync.save");
            }
        }

        private static void Upsert(SettingsDbContext ctx, string key, string value)
        {
            var row = ctx.KvSettings.FirstOrDefault(k => k.Key == key);
            if (row == null) ctx.KvSettings.Add(new KvSetting { Key = key, Value = value });
            else row.Value = value;
        }

        // ── Bootstrap-state extraction ───────────────────────────────────────

        private const string BootstrapMarker = "window.__BOOTSTRAP_STATE__";

        /// <summary>
        /// Carves out the JSON object assigned to window.__BOOTSTRAP_STATE__,
        /// honoring string boundaries and escapes so braces inside string
        /// literals don't confuse the depth counter.
        /// </summary>
        internal static JsonNode? ExtractBootstrapState(string html)
        {
            var idx = html.IndexOf(BootstrapMarker, StringComparison.Ordinal);
            if (idx < 0) return null;
            var braceIdx = html.IndexOf('{', idx);
            if (braceIdx < 0) return null;

            int depth = 0;
            bool inString = false;
            bool escape = false;
            int end = -1;
            for (int i = braceIdx; i < html.Length; i++)
            {
                char c = html[i];
                if (escape) { escape = false; continue; }
                if (c == '\\') { escape = true; continue; }
                if (c == '"') { inString = !inString; continue; }
                if (inString) continue;
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) { end = i; break; }
                }
            }
            if (end < 0) return null;
            var json = html.Substring(braceIdx, end - braceIdx + 1);
            try { return JsonNode.Parse(json); }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "TeEventScraperService.ExtractBootstrapState.JsonParse");
                return null;
            }
        }

        // ── Event parser ─────────────────────────────────────────────────────

        // "Thursday, June 18th from 1:00pm - 3:00pm" or "June 18 from 1pm-3pm"
        // The day-of-week prefix is optional; the time range is optional.
        private static readonly Regex EventDateTimeRx = new(
            @"(?:(?<dow>Mon|Tue|Wed|Thu|Fri|Sat|Sun)[a-z]*,?\s+)?" +
            @"(?<month>January|February|March|April|May|June|July|August|September|October|November|December)" +
            @"\s+(?<day>\d{1,2})(?:st|nd|rd|th)?" +
            @"(?:,?\s+(?<year>\d{4}))?" +
            @"(?:\s+from\s+(?<start>\d{1,2}(?::\d{2})?\s*[ap]m)" +
            @"(?:\s*[-–]\s*(?<end>\d{1,2}(?::\d{2})?\s*[ap]m))?)?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        internal static List<TeEventCache> ParseEventsFromBootstrap(string html)
        {
            var root = ExtractBootstrapState(html);
            if (root == null) return new List<TeEventCache>();

            var events = new List<TeEventCache>();
            CollectEventBlocks(root, events);

            // Deduplicate by ExternalId in case the tree walk visits the same
            // node twice (Square's state can be shared across page variants).
            return events
                .GroupBy(e => e.ExternalId)
                .Select(g => g.First())
                .OrderBy(e => e.EventDate)
                .ToList();
        }

        private static void CollectEventBlocks(JsonNode node, List<TeEventCache> sink)
        {
            switch (node)
            {
                case JsonArray arr:
                    foreach (var child in arr) if (child != null) CollectEventBlocks(child, sink);
                    break;
                case JsonObject obj:
                    // Square content blocks look like:
                    //   { id, text:{content:{quill:{ops:[...]}}}, image:{figure:{source,...}}, button:{link:{link:{external}}, label}, title:{content}, ... }
                    // We treat a block as an "event" if its quill text contains
                    // at least one date pattern. That filters newsletter-signup
                    // and decorative blocks out cleanly.
                    if (TryParseEventBlock(obj, sink))
                    {
                        // Still descend — some pages nest blocks inside blocks
                        // and we don't want to miss inner ones, though in
                        // practice the events sit at the same level.
                    }
                    foreach (var prop in obj)
                        if (prop.Value != null) CollectEventBlocks(prop.Value, sink);
                    break;
            }
        }

        private static bool TryParseEventBlock(JsonObject obj, List<TeEventCache> sink)
        {
            var quillText = ExtractQuillText(obj["text"]);
            if (string.IsNullOrWhiteSpace(quillText)) return false;

            var matches = EventDateTimeRx.Matches(quillText);
            if (matches.Count == 0) return false;

            var url = ExtractRegistrationUrl(obj);
            var imageUrl = ExtractImageUrl(obj);
            var title = DeriveTitle(url, quillText);

            bool added = false;
            foreach (Match m in matches)
            {
                if (!TryBuildEventDate(m, out var date, out var time)) continue;

                var idBase = url ?? title;
                var externalId = $"{idBase}|{date:yyyy-MM-dd}";
                if (time.HasValue) externalId += $"|{time.Value:hh\\:mm}";
                // Stable hash across processes — String.GetHashCode is randomized
                // per process in .NET, which caused duplicate cache rows on every
                // app start.
                externalId = StableHash(externalId);

                var displayTitle = title;
                if (time.HasValue)
                {
                    displayTitle = $"{title} ({FormatTime(time.Value)})";
                }

                sink.Add(new TeEventCache
                {
                    ExternalId  = externalId,
                    EventDate   = date,
                    Title       = displayTitle,
                    Description = quillText.Length > 500 ? quillText.Substring(0, 500) + "…" : quillText,
                    Url         = url,
                    ImageUrl    = imageUrl,
                });
                added = true;
            }
            return added;
        }

        private static string StableHash(string input)
        {
            var bytes = System.Security.Cryptography.SHA1.HashData(
                System.Text.Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes);
        }

        private static string ExtractQuillText(JsonNode? textNode)
        {
            // text.content.quill.ops[*].insert (string) — concatenate them all.
            var ops = textNode?["content"]?["quill"]?["ops"] as JsonArray;
            if (ops == null) return string.Empty;
            var sb = new System.Text.StringBuilder();
            foreach (var op in ops)
            {
                var ins = op?["insert"];
                if (ins is JsonValue v && v.TryGetValue<string>(out var s))
                    sb.Append(s);
            }
            return sb.ToString();
        }

        private static string? ExtractRegistrationUrl(JsonObject obj)
        {
            // Prefer the primary button's external link; fall back to image link.
            var btnUrl = obj["button"]?["link"]?["link"]?["external"]?.GetValue<string?>();
            if (!string.IsNullOrWhiteSpace(btnUrl)) return btnUrl;
            var imgUrl = obj["image"]?["link"]?["link"]?["external"]?.GetValue<string?>();
            return string.IsNullOrWhiteSpace(imgUrl) ? null : imgUrl;
        }

        private static string? ExtractImageUrl(JsonObject obj)
        {
            var src = obj["image"]?["figure"]?["source"]?.GetValue<string?>();
            if (string.IsNullOrWhiteSpace(src)) return null;
            if (src.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                src.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return src;
            return ImageOrigin + (src.StartsWith("/") ? src : "/" + src);
        }

        /// <summary>
        /// Pick a human title for the event. Square uses generic block labels
        /// ("Quality Craftsmanship", "Distinctive and Bold") for layout, so
        /// the registration URL slug is the most reliable signal. Falls back
        /// to the first sentence of the description.
        /// </summary>
        private static string DeriveTitle(string? url, string description)
        {
            if (!string.IsNullOrWhiteSpace(url))
            {
                var slug = url.TrimEnd('/');
                var lastSlash = slug.LastIndexOf('/');
                if (lastSlash >= 0 && lastSlash < slug.Length - 1)
                    slug = slug.Substring(lastSlash + 1);
                slug = slug.Replace('-', ' ').Replace('_', ' ').Trim();
                if (slug.Length > 0)
                {
                    var titled = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(slug.ToLowerInvariant());
                    return titled.Length > 80 ? titled.Substring(0, 80) + "…" : titled;
                }
            }
            // Fallback: first non-empty line of the description, trimmed.
            var firstLine = description
                .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .FirstOrDefault(s => s.Length > 0) ?? "TE Class";
            return firstLine.Length > 80 ? firstLine.Substring(0, 80) + "…" : firstLine;
        }

        private static bool TryBuildEventDate(Match m, out DateTime date, out TimeSpan? time)
        {
            date = default;
            time = null;
            var month = m.Groups["month"].Value;
            if (!int.TryParse(m.Groups["day"].Value, out var day)) return false;

            int monthNum;
            try { monthNum = DateTime.ParseExact(month, "MMMM", CultureInfo.InvariantCulture).Month; }
            catch { return false; }

            int year;
            if (m.Groups["year"].Success && int.TryParse(m.Groups["year"].Value, out var explicitYear))
            {
                year = explicitYear;
            }
            else
            {
                // No year in source — assume the nearest future occurrence.
                var today = DateTime.Today;
                year = today.Year;
                var candidate = new DateTime(year, monthNum, Math.Clamp(day, 1, DateTime.DaysInMonth(year, monthNum)));
                if (candidate < today.AddDays(-30)) year++;
            }

            try { date = new DateTime(year, monthNum, day); }
            catch { return false; }

            // Optional start time.
            if (m.Groups["start"].Success)
            {
                var raw = m.Groups["start"].Value.Trim().ToUpperInvariant().Replace(" ", "");
                // Accept "1PM", "1:00PM", "10AM", "10:30AM".
                if (DateTime.TryParseExact(raw, new[] { "htt", "h:mmtt", "hhtt", "hh:mmtt" },
                                           CultureInfo.InvariantCulture, DateTimeStyles.None, out var t))
                    time = t.TimeOfDay;
            }

            return true;
        }

        private static string FormatTime(TimeSpan t)
        {
            var dt = DateTime.Today + t;
            return dt.ToString("h:mmtt", CultureInfo.InvariantCulture).ToLowerInvariant();
        }

        // ── Shop-hours parser ────────────────────────────────────────────────

        // "Monday   09:00 am - 05:00 pm" or "Sunday   Closed"
        private static readonly Regex HoursLineRx = new(
            @"^\s*(?<day>Sunday|Monday|Tuesday|Wednesday|Thursday|Friday|Saturday)\s+(?<rest>.+?)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly string[] WeekOrder =
            { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };

        internal static List<TeShopHoursLine> ParseShopHoursFromBootstrap(string html)
        {
            var root = ExtractBootstrapState(html);
            if (root == null) return new List<TeShopHoursLine>();

            // Walk the tree for any object with a "hoursConfig" property whose
            // shape matches { content: { quill: { ops: [...] } } }.
            var collected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            CollectHoursBlocks(root, collected);

            // Return in Sunday-first weekday order so the UI doesn't have to sort.
            return WeekOrder
                .Where(d => collected.ContainsKey(d))
                .Select(d => new TeShopHoursLine { Day = d, Text = collected[d] })
                .ToList();
        }

        private static void CollectHoursBlocks(JsonNode node, Dictionary<string, string> sink)
        {
            switch (node)
            {
                case JsonArray arr:
                    foreach (var c in arr) if (c != null) CollectHoursBlocks(c, sink);
                    break;
                case JsonObject obj:
                    if (obj["hoursConfig"] is JsonNode hours)
                    {
                        var text = ExtractQuillText(hours);
                        if (!string.IsNullOrWhiteSpace(text)) AbsorbHoursText(text, sink);
                    }
                    foreach (var p in obj) if (p.Value != null) CollectHoursBlocks(p.Value, sink);
                    break;
            }
        }

        private static void AbsorbHoursText(string blob, Dictionary<string, string> sink)
        {
            // Quill ops join into newline-delimited lines like
            //   "Monday   09:00 am - 05:00 pm"
            //   "Sunday   Closed"
            foreach (var rawLine in blob.Split('\n'))
            {
                var m = HoursLineRx.Match(rawLine);
                if (!m.Success) continue;
                var day = char.ToUpperInvariant(m.Groups["day"].Value[0]) + m.Groups["day"].Value.Substring(1).ToLowerInvariant();
                var text = Regex.Replace(m.Groups["rest"].Value, @"\s+", " ").Trim();
                sink[day] = text;
            }
        }
    }
}
