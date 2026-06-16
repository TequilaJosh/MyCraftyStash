using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using MyCraftyStash.Models;

namespace MyCraftyStash.Services
{
    /// <summary>
    /// Fetches a single product URL and best-effort builds a WishlistItem from
    /// OpenGraph / JSON-LD / page metadata. Falls back to URL-only if scraping fails.
    /// </summary>
    public class WishlistLinkImportService
    {
        private static readonly HttpClient _http = new(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            AllowAutoRedirect      = true,
        })
        {
            Timeout = TimeSpan.FromSeconds(20),
            DefaultRequestHeaders =
            {
                { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36" },
                { "Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8" },
                { "Accept-Language", "en-US,en;q=0.9" },
            },
        };

        public async Task<WishlistItem> FetchAsync(string url)
        {
            var item = new WishlistItem
            {
                Name     = string.Empty,
                Url      = url,
                Priority = 2,
            };

            try
            {
                var host = TryGetHost(url);
                if (!string.IsNullOrEmpty(host)) item.PurchasedFrom = PrettyHost(host);

                using var resp = await _http.GetAsync(url);
                resp.EnsureSuccessStatusCode();
                var html = await resp.Content.ReadAsStringAsync();

                item.Name          = ExtractMeta(html, "og:title") ?? ExtractTitle(html) ?? UrlBasename(url);
                item.ImageUrl      = ExtractMeta(html, "og:image");
                var siteName       = ExtractMeta(html, "og:site_name");
                if (!string.IsNullOrWhiteSpace(siteName)) item.PurchasedFrom = siteName;

                var price = ExtractPrice(html);
                if (price.HasValue) item.Price = price;
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, $"WishlistLinkImportService.FetchAsync ({url})");
                if (string.IsNullOrEmpty(item.Name))
                    item.Name = UrlBasename(url);
            }
            return item;
        }

        private static string? ExtractMeta(string html, string property)
        {
            var pattern = $"<meta[^>]+(?:property|name)\\s*=\\s*[\"']{Regex.Escape(property)}[\"'][^>]*content\\s*=\\s*[\"']([^\"']+)[\"']";
            var m = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
            if (m.Success) return WebUtility.HtmlDecode(m.Groups[1].Value).Trim();

            // try reversed attribute order
            pattern = $"<meta[^>]+content\\s*=\\s*[\"']([^\"']+)[\"'][^>]*(?:property|name)\\s*=\\s*[\"']{Regex.Escape(property)}[\"']";
            m = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
            return m.Success ? WebUtility.HtmlDecode(m.Groups[1].Value).Trim() : null;
        }

        private static string? ExtractTitle(string html)
        {
            var m = Regex.Match(html, "<title[^>]*>([^<]+)</title>", RegexOptions.IgnoreCase);
            return m.Success ? WebUtility.HtmlDecode(m.Groups[1].Value).Trim() : null;
        }

        private static decimal? ExtractPrice(string html)
        {
            // Try OpenGraph product price first.
            var p = ExtractMeta(html, "product:price:amount") ?? ExtractMeta(html, "og:price:amount");
            if (decimal.TryParse(p, NumberStyles.Number, CultureInfo.InvariantCulture, out var v)) return v;

            // JSON-LD price
            var m = Regex.Match(html, "\"price\"\\s*:\\s*\"?([\\d]+(?:[.,][\\d]+)?)\"?", RegexOptions.IgnoreCase);
            if (m.Success && decimal.TryParse(m.Groups[1].Value.Replace(',', '.'),
                    NumberStyles.Number, CultureInfo.InvariantCulture, out var v2)) return v2;

            return null;
        }

        private static string? TryGetHost(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host : null;
        }

        private static string PrettyHost(string host)
        {
            var h = host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
            // Capitalize first letter only — caller will likely keep as-is.
            return h;
        }

        // Last-ditch name when no og:title / <title> could be read: turn the
        // final URL path segment into a readable title (e.g.
        // ".../big-thanks-stamp-set" -> "Big Thanks Stamp Set"), falling back
        // to the host only when there's no usable path.
        private static string UrlBasename(string url)
        {
            try
            {
                var uri = new Uri(url);
                var slug = uri.Segments
                    .Select(s => s.Trim('/'))
                    .LastOrDefault(s => s.Length > 0 && !s.Contains('.'));
                if (!string.IsNullOrWhiteSpace(slug))
                {
                    var words = slug.Replace('-', ' ').Replace('_', ' ').Trim();
                    return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(words);
                }
                return uri.Host;
            }
            catch { return url; }
        }
    }
}
