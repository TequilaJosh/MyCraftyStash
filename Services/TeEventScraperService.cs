using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MyCraftyStash.Data;
using MyCraftyStash.Models;
using MyCraftyStash.Services.Catalog;

namespace MyCraftyStash.Services
{
    /// <summary>
    /// Scrapes the Taylored Expressions Square Online site for the published
    /// monthly calendar (current + next month per the user's request) and
    /// caches results in settings.db.te_events_cache so the calendar overlay
    /// keeps working offline.
    ///
    /// Square Online sites embed their content as a JSON blob in a
    /// &lt;script&gt; tag (the "site-state" payload) — much more stable than
    /// scraping the rendered DOM. We try the JSON-LD Event schema first
    /// (cleanest), then fall back to that site-state payload, then to
    /// regex'd headings as a last resort.
    /// </summary>
    public class TeEventScraperService
    {
        public const string SiteUrl = "https://taylored-expressions-inc.square.site/";

        /// <summary>How long a successful fetch is considered fresh. Shorter
        /// windows mean more HTTP traffic; the calendar refreshes once a day
        /// regardless when the user opens the app.</summary>
        public static readonly TimeSpan FreshFor = TimeSpan.FromHours(20);

        private SettingsDbContext CreateContext() => new SettingsDbContext();

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
                var newest = ctx.TeEventsCache.AsNoTracking()
                    .OrderByDescending(e => e.FetchedAt)
                    .Select(e => (DateTime?)e.FetchedAt)
                    .FirstOrDefault();
                if (newest == null) return true;
                return DateTime.UtcNow - newest.Value > FreshFor;
            }
            catch
            {
                return true;
            }
        }

        public async Task FetchAndCacheAsync(CancellationToken ct = default)
        {
            List<TeEventCache> events;
            try
            {
                using var resp = await CatalogHttpClient.Instance.GetAsync(SiteUrl, ct);
                resp.EnsureSuccessStatusCode();
                var html = await resp.Content.ReadAsStringAsync(ct);
                events = ParseEvents(html);
                LoggingService.LogInfo($"TeEventScraperService: parsed {events.Count} events from {SiteUrl}");
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "TeEventScraperService.FetchAndCacheAsync");
                return;
            }

            if (events.Count == 0)
            {
                LoggingService.LogWarning("TeEventScraperService: parser found no events — leaving cache untouched");
                return;
            }

            try
            {
                using var ctx = CreateContext();
                var now = DateTime.UtcNow;

                // Upsert by ExternalId. Drop anything in the cache that's
                // older than yesterday (already-passed events).
                var cutoff = DateTime.Now.Date.AddDays(-1);
                var stale = ctx.TeEventsCache.Where(e => e.EventDate < cutoff);
                ctx.TeEventsCache.RemoveRange(stale);

                foreach (var fetched in events)
                {
                    var existing = ctx.TeEventsCache
                        .FirstOrDefault(e => e.ExternalId == fetched.ExternalId);
                    if (existing == null)
                    {
                        fetched.FetchedAt = now;
                        ctx.TeEventsCache.Add(fetched);
                    }
                    else
                    {
                        existing.EventDate    = fetched.EventDate;
                        existing.Title        = fetched.Title;
                        existing.Description  = fetched.Description;
                        existing.Url          = fetched.Url;
                        existing.ImageUrl     = fetched.ImageUrl;
                        existing.FetchedAt    = now;
                    }
                }
                await ctx.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "TeEventScraperService.FetchAndCacheAsync save");
            }
        }

        // ── Parsing ──────────────────────────────────────────────────────────
        // Square Online doesn't expose a public events API, so this is a
        // best-effort multi-strategy parser. Each strategy is independent;
        // first hit with results wins.

        private static List<TeEventCache> ParseEvents(string html)
        {
            var fromJsonLd = ParseFromJsonLd(html);
            if (fromJsonLd.Count > 0) return fromJsonLd;

            var fromState = ParseFromSiteState(html);
            if (fromState.Count > 0) return fromState;

            return ParseFromHeadings(html);
        }

        private static readonly Regex JsonLdRx = new(
            @"<script[^>]*type\s*=\s*[""']application/ld\+json[""'][^>]*>(?<json>.*?)</script>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static List<TeEventCache> ParseFromJsonLd(string html)
        {
            var result = new List<TeEventCache>();
            foreach (Match m in JsonLdRx.Matches(html))
            {
                var json = m.Groups["json"].Value.Trim();
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    CollectEventsFromJsonLd(doc.RootElement, result);
                }
                catch
                {
                    // ignore invalid JSON-LD blocks; fall through to next strategy
                }
            }
            return result;
        }

        private static void CollectEventsFromJsonLd(JsonElement el, List<TeEventCache> sink)
        {
            if (el.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in el.EnumerateArray()) CollectEventsFromJsonLd(item, sink);
                return;
            }
            if (el.ValueKind != JsonValueKind.Object) return;

            // Recurse into @graph if present.
            if (el.TryGetProperty("@graph", out var graph))
                CollectEventsFromJsonLd(graph, sink);

            var typeStr = el.TryGetProperty("@type", out var t)
                          ? t.ValueKind == JsonValueKind.String ? t.GetString() : null
                          : null;
            if (typeStr != null && typeStr.Contains("Event", StringComparison.OrdinalIgnoreCase))
            {
                var name  = el.TryGetProperty("name", out var n) ? n.GetString() : null;
                var date  = el.TryGetProperty("startDate", out var d) ? d.GetString() : null;
                var url   = el.TryGetProperty("url", out var u) ? u.GetString() : null;
                var desc  = el.TryGetProperty("description", out var dd) ? dd.GetString() : null;
                var image = el.TryGetProperty("image", out var im)
                            ? im.ValueKind == JsonValueKind.String ? im.GetString()
                              : im.ValueKind == JsonValueKind.Object && im.TryGetProperty("url", out var iu)
                                ? iu.GetString() : null
                            : null;

                if (!string.IsNullOrWhiteSpace(name) &&
                    !string.IsNullOrWhiteSpace(date) &&
                    DateTime.TryParse(date, out var parsed))
                {
                    sink.Add(new TeEventCache
                    {
                        ExternalId  = (url ?? $"{name}|{parsed:yyyy-MM-dd}").GetHashCode().ToString("X"),
                        EventDate   = parsed.Date,
                        Title       = name!.Trim(),
                        Description = desc,
                        Url         = url,
                        ImageUrl    = image,
                    });
                }
            }
        }

        // Square Online ships an inline JSON state blob; the exact key path is
        // theme-dependent so we look for a generic "events" array containing
        // objects with "title" + "date"/"startDate" fields.
        private static List<TeEventCache> ParseFromSiteState(string html)
        {
            var result = new List<TeEventCache>();
            var stateRx = new Regex(
                @"<script[^>]*>(?<j>\s*\{.*?""events""\s*:\s*\[.*?\].*?\})\s*</script>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            foreach (Match m in stateRx.Matches(html))
            {
                try
                {
                    using var doc = JsonDocument.Parse(m.Groups["j"].Value);
                    WalkForEvents(doc.RootElement, result);
                }
                catch { }
            }
            return result;
        }

        private static void WalkForEvents(JsonElement el, List<TeEventCache> sink)
        {
            if (el.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in el.EnumerateObject())
                {
                    if (p.NameEquals("events") && p.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var ev in p.Value.EnumerateArray())
                        {
                            var title = TryStr(ev, "title", "name", "label");
                            var when  = TryStr(ev, "date", "startDate", "start", "starts_at");
                            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(when)) continue;
                            if (!DateTime.TryParse(when, out var d)) continue;
                            var url   = TryStr(ev, "url", "permalink");
                            var image = TryStr(ev, "image", "imageUrl", "image_url", "photo");
                            sink.Add(new TeEventCache
                            {
                                ExternalId = (url ?? $"{title}|{d:yyyy-MM-dd}").GetHashCode().ToString("X"),
                                EventDate  = d.Date,
                                Title      = title!.Trim(),
                                Url        = url,
                                ImageUrl   = image,
                            });
                        }
                    }
                    else
                    {
                        WalkForEvents(p.Value, sink);
                    }
                }
            }
            else if (el.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in el.EnumerateArray()) WalkForEvents(item, sink);
            }
        }

        private static string? TryStr(JsonElement obj, params string[] names)
        {
            if (obj.ValueKind != JsonValueKind.Object) return null;
            foreach (var n in names)
                if (obj.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String)
                    return v.GetString();
            return null;
        }

        // Last resort: pull headings that look like dated calendar entries from
        // the rendered HTML. Best-effort; the JSON paths above should catch
        // anything Square actually publishes.
        private static List<TeEventCache> ParseFromHeadings(string html)
        {
            var result = new List<TeEventCache>();
            var dateRx = new Regex(
                @"\b(?<m>Jan(?:uary)?|Feb(?:ruary)?|Mar(?:ch)?|Apr(?:il)?|May|Jun(?:e)?|Jul(?:y)?|Aug(?:ust)?|Sep(?:tember)?|Oct(?:ober)?|Nov(?:ember)?|Dec(?:ember)?)\s+(?<d>\d{1,2})(?:st|nd|rd|th)?(?:,?\s+(?<y>\d{4}))?\b",
                RegexOptions.IgnoreCase);
            foreach (Match m in dateRx.Matches(html))
            {
                var raw = m.Value;
                if (DateTime.TryParse(raw, out var d))
                {
                    if (d.Date < DateTime.Today.AddDays(-1)) continue;
                    result.Add(new TeEventCache
                    {
                        ExternalId = $"heur|{d:yyyy-MM-dd}|{raw.GetHashCode():X}",
                        EventDate  = d.Date,
                        Title      = "TE Calendar",
                    });
                }
            }
            return result.DistinctBy(e => e.EventDate).ToList();
        }
    }
}
