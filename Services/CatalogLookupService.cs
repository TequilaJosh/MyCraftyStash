using System.Net.Http;
using System.Text.RegularExpressions;
using MyCraftyStash.Models;

namespace MyCraftyStash.Services
{
    /// <summary>
    /// Looks up Taylored Expressions catalog items by querying the public store
    /// at https://www.tayloredexpressions.com. The site runs on BigCommerce
    /// (Stencil theme); product cards expose name/sku/price/image as data-*
    /// attributes, and product detail pages expose the full image gallery as
    /// data-image-gallery-zoom-image-url attributes.
    ///
    /// No credentials needed; the only requirement is internet access.
    /// </summary>
    public static class CatalogLookupService
    {
        private const string Site = "https://www.tayloredexpressions.com";

        private static readonly HttpClient _http;

        static CatalogLookupService()
        {
            _http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true })
            {
                Timeout = TimeSpan.FromSeconds(20)
            };
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _http.DefaultRequestHeaders.Accept.ParseAdd(
                "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        }

        /// <summary>Always true — the service hits a public website.</summary>
        public static bool IsAvailable => true;

        /// <summary>Kept for compatibility with the old DB-based flow; no-op.</summary>
        public static void ResetAvailabilityCache() { }

        /// <summary>
        /// Search Taylored Expressions for products matching <paramref name="query"/>.
        /// Returns lightweight results parsed straight from the search page's
        /// product cards. Full image gallery is deferred to EnrichResultAsync.
        /// </summary>
        public static async Task<List<CatalogLookupResult>> SearchAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<CatalogLookupResult>();

            try
            {
                var url = $"{Site}/search.php?search_query={Uri.EscapeDataString(query.Trim())}&section=product";
                using var resp = await _http.GetAsync(url);
                resp.EnsureSuccessStatusCode();
                var html = await resp.Content.ReadAsStringAsync();

                var results = new List<CatalogLookupResult>();
                var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (Match cardMatch in CardRx.Matches(html))
                {
                    var body = cardMatch.Groups["body"].Value;

                    var name = Attr(body, "data-name") ?? MetaProp(body, "name");
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    var productUrl = StripQuery(MetaProp(body, "url") ?? CardLinkHref(body));
                    if (string.IsNullOrEmpty(productUrl) || !seenUrls.Add(productUrl)) continue;

                    var sku = Attr(body, "data-product-sku") ?? MetaProp(body, "sku");
                    var imageUrl = MetaProp(body, "image");
                    var category = Attr(body, "data-product-category");
                    var price = ParsePrice(
                        Attr(body, "data-product-price") ?? MetaProp(body, "price"));

                    results.Add(new CatalogLookupResult
                    {
                        Name       = name.Trim(),
                        Type       = ExtractType(category) ?? string.Empty,
                        Price      = price,
                        ImageUrl   = imageUrl,
                        Url        = productUrl,
                        Handle     = ExtractHandle(productUrl),
                        ItemNumber = string.IsNullOrWhiteSpace(sku) ? null : sku.Trim()
                    });

                    if (results.Count >= 25) break;
                }

                return results;
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, $"CatalogLookupService.SearchAsync (query: {query})");
                return new List<CatalogLookupResult>();
            }
        }

        /// <summary>
        /// Fetches the product detail page and pulls down the full image gallery
        /// as base64 (so the existing inventory flow, which stores images as
        /// base64 data URIs, doesn't have to change). Failures degrade gracefully
        /// — the caller still has the search-card image.
        /// </summary>
        public static async Task EnrichResultAsync(CatalogLookupResult? result)
        {
            if (result == null || string.IsNullOrEmpty(result.Url)) return;

            try
            {
                using var resp = await _http.GetAsync(result.Url);
                resp.EnsureSuccessStatusCode();
                var html = await resp.Content.ReadAsStringAsync();

                // Gallery: zoom image is the highest-quality version.
                var httpImages = new List<string>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (Match m in GalleryZoomRx.Matches(html))
                {
                    var src = System.Net.WebUtility.HtmlDecode(m.Groups[1].Value);
                    if (string.IsNullOrEmpty(src)) continue;
                    if (src.StartsWith("//")) src = "https:" + src;
                    if (seen.Add(src)) httpImages.Add(src);
                }

                // Fallback to the search-card image if the detail page parse
                // turned up nothing.
                if (httpImages.Count == 0 && !string.IsNullOrEmpty(result.ImageUrl)
                    && result.ImageUrl.StartsWith("http"))
                    httpImages.Add(result.ImageUrl);

                var base64Images = new List<string>();
                foreach (var imgUrl in httpImages.Take(8))
                {
                    var b64 = await DownloadImageAsBase64Async(imgUrl);
                    if (!string.IsNullOrEmpty(b64)) base64Images.Add(b64);
                }

                if (base64Images.Count > 0)
                {
                    result.ImageUrl = base64Images[0];
                    result.ExtraImages.Clear();
                    if (base64Images.Count > 1)
                        result.ExtraImages.AddRange(base64Images.Skip(1));
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, $"CatalogLookupService.EnrichResultAsync (url: {result.Url})");
            }
        }

        // ── parsing helpers ────────────────────────────────────────────────

        private static readonly Regex CardRx = new(
            @"<li[^>]*\bclass\s*=\s*""[^""]*\bproduct\b[^""]*""[^>]*>(?<body>.*?)</li>",
            RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex GalleryZoomRx = new(
            @"data-image-gallery-zoom-image-url\s*=\s*""([^""]+)""",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex CardLinkRx = new(
            @"<a[^>]*\bclass\s*=\s*""[^""]*\bcard-figure__link\b[^""]*""[^>]*\bhref\s*=\s*""([^""]+)""",
            RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static string? Attr(string body, string name)
        {
            var rx = new Regex(@"\b" + Regex.Escape(name) + @"\s*=\s*""(?<v>[^""]*)""",
                RegexOptions.IgnoreCase);
            var m = rx.Match(body);
            return m.Success ? System.Net.WebUtility.HtmlDecode(m.Groups["v"].Value) : null;
        }

        private static string? MetaProp(string body, string itemprop)
        {
            var rx = new Regex(
                @"<meta[^>]*\bitemprop\s*=\s*""" + Regex.Escape(itemprop) +
                @"""[^>]*\bcontent\s*=\s*""(?<v>[^""]*)""",
                RegexOptions.IgnoreCase);
            var m = rx.Match(body);
            if (!m.Success)
            {
                rx = new Regex(
                    @"<meta[^>]*\bcontent\s*=\s*""(?<v>[^""]*)""[^>]*\bitemprop\s*=\s*""" +
                    Regex.Escape(itemprop) + @"""",
                    RegexOptions.IgnoreCase);
                m = rx.Match(body);
            }
            return m.Success ? System.Net.WebUtility.HtmlDecode(m.Groups["v"].Value) : null;
        }

        private static string? CardLinkHref(string body)
        {
            var m = CardLinkRx.Match(body);
            return m.Success ? System.Net.WebUtility.HtmlDecode(m.Groups[1].Value) : null;
        }

        private static string? StripQuery(string? url)
        {
            if (string.IsNullOrEmpty(url)) return url;
            var q = url.IndexOfAny(new[] { '?', '#' });
            return q >= 0 ? url[..q] : url;
        }

        /// <summary>
        /// data-product-category looks like:
        ///   "Filter/by Color/Cupcake, Shop/Stamps, Shop/Stamps/Birthday, ..."
        /// We want the first "Shop/X" segment's leaf, or the level-1 if that's
        /// all there is — that's the closest match to the app's ItemTypes list.
        /// </summary>
        private static string? ExtractType(string? category)
        {
            if (string.IsNullOrWhiteSpace(category)) return null;
            foreach (var raw in category.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var seg = raw.Trim();
                if (!seg.StartsWith("Shop/", StringComparison.OrdinalIgnoreCase)) continue;
                var parts = seg[5..].Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;
                return parts[0].Trim();
            }
            return null;
        }

        private static decimal? ParsePrice(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var s = raw.Replace("$", "").Replace(",", "").Trim();
            return decimal.TryParse(s,
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var d)
                ? d : null;
        }

        private static string? ExtractHandle(string? productUrl)
        {
            if (string.IsNullOrEmpty(productUrl)) return null;
            // BigCommerce product URLs are https://site/{handle}/ — pull the last
            // non-empty path segment.
            try
            {
                var uri = new Uri(productUrl);
                var path = uri.AbsolutePath.Trim('/');
                if (string.IsNullOrEmpty(path)) return null;
                var lastSlash = path.LastIndexOf('/');
                return lastSlash < 0 ? path : path[(lastSlash + 1)..];
            }
            catch { return null; }
        }

        private static async Task<string?> DownloadImageAsBase64Async(string url)
        {
            try
            {
                using var resp = await _http.GetAsync(url);
                resp.EnsureSuccessStatusCode();
                var bytes = await resp.Content.ReadAsByteArrayAsync();

                var mime = "image/png";
                var lower = url.ToLowerInvariant();
                if (lower.Contains(".jpg") || lower.Contains(".jpeg")) mime = "image/jpeg";
                else if (lower.Contains(".webp")) mime = "image/webp";
                else if (lower.Contains(".gif"))  mime = "image/gif";

                return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
            }
            catch (Exception ex)
            {
                LoggingService.LogWarning($"CatalogLookupService.DownloadImageAsBase64Async failed for {url}: {ex.Message}");
                return null;
            }
        }
    }
}
