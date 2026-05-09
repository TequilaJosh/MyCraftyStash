using System.Net.Http;
using System.Text.RegularExpressions;
using MyCraftyStash.Models;

namespace MyCraftyStash.Services.Catalog
{
    /// <summary>
    /// BigCommerce (Stencil theme) scraper for tayloredexpressions.com.
    /// The site exposes product data via data-* attributes on each card —
    /// data-name, data-product-sku, data-product-price, data-product-category —
    /// and the detail page surfaces the full image gallery via
    /// data-image-gallery-zoom-image-url. No credentials required.
    /// </summary>
    public class TaylorExpressionsCatalogProvider : ICatalogProvider
    {
        public string Id              => "te";
        public string DisplayName     => "Taylored Expressions";
        public string Domain          => "tayloredexpressions.com";
        public bool   DefaultEnabled  => true;

        private const string Site = "https://www.tayloredexpressions.com";

        public async Task<List<CatalogLookupResult>> SearchAsync(string query, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<CatalogLookupResult>();

            try
            {
                var url = $"{Site}/search.php?search_query={Uri.EscapeDataString(query.Trim())}&section=product";
                using var resp = await CatalogHttpClient.Instance.GetAsync(url, ct);
                resp.EnsureSuccessStatusCode();
                var html = await resp.Content.ReadAsStringAsync(ct);

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
                    var price = ParsePrice(Attr(body, "data-product-price") ?? MetaProp(body, "price"));

                    results.Add(new CatalogLookupResult
                    {
                        Name       = name.Trim(),
                        Type       = ExtractType(category) ?? string.Empty,
                        Price      = price,
                        ImageUrl   = imageUrl,
                        Url        = productUrl,
                        Handle     = ExtractHandle(productUrl),
                        ItemNumber = string.IsNullOrWhiteSpace(sku) ? null : sku.Trim(),
                        Source     = DisplayName,
                        SourceId   = Id,
                    });

                    if (results.Count >= 25) break;
                }

                return results;
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, $"TaylorExpressionsCatalogProvider.SearchAsync (query: {query})");
                return new List<CatalogLookupResult>();
            }
        }

        public async Task EnrichResultAsync(CatalogLookupResult result, CancellationToken ct = default)
        {
            if (result == null || string.IsNullOrEmpty(result.Url)) return;

            try
            {
                using var resp = await CatalogHttpClient.Instance.GetAsync(result.Url, ct);
                resp.EnsureSuccessStatusCode();
                var html = await resp.Content.ReadAsStringAsync(ct);

                var httpImages = new List<string>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (Match m in GalleryZoomRx.Matches(html))
                {
                    var src = System.Net.WebUtility.HtmlDecode(m.Groups[1].Value);
                    if (string.IsNullOrEmpty(src)) continue;
                    if (src.StartsWith("//")) src = "https:" + src;
                    if (seen.Add(src)) httpImages.Add(src);
                }

                if (httpImages.Count == 0 && !string.IsNullOrEmpty(result.ImageUrl)
                    && result.ImageUrl.StartsWith("http"))
                    httpImages.Add(result.ImageUrl);

                var base64Images = new List<string>();
                foreach (var imgUrl in httpImages.Take(8))
                {
                    var b64 = await DownloadAsBase64Async(imgUrl, ct);
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
                LoggingService.LogError(ex, $"TaylorExpressionsCatalogProvider.EnrichResultAsync (url: {result.Url})");
            }
        }

        // ── parsing helpers (carried over from the old static service) ─────

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
                System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;
        }

        private static string? ExtractHandle(string? productUrl)
        {
            if (string.IsNullOrEmpty(productUrl)) return null;
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

        private static async Task<string?> DownloadAsBase64Async(string url, CancellationToken ct)
        {
            try
            {
                using var resp = await CatalogHttpClient.Instance.GetAsync(url, ct);
                resp.EnsureSuccessStatusCode();
                var bytes = await resp.Content.ReadAsByteArrayAsync(ct);

                var mime = "image/png";
                var lower = url.ToLowerInvariant();
                if (lower.Contains(".jpg") || lower.Contains(".jpeg")) mime = "image/jpeg";
                else if (lower.Contains(".webp")) mime = "image/webp";
                else if (lower.Contains(".gif"))  mime = "image/gif";

                return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
            }
            catch (Exception ex)
            {
                LoggingService.LogWarning($"TE EnrichResultAsync image download failed for {url}: {ex.Message}");
                return null;
            }
        }
    }
}
