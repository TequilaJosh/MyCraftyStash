using System.Net.Http;
using System.Text.Json;
using MyCraftyStash.Models;

namespace MyCraftyStash.Services.Catalog
{
    /// <summary>
    /// Generic Shopify storefront provider. Uses Shopify's public
    /// /search/suggest.json endpoint (Online Store 2.0 standard) which returns
    /// a clean product list and avoids brittle HTML parsing for the search
    /// step. Falls back to /products.json on enrichment for the full image
    /// gallery + SKU.
    ///
    /// Most modern cardmaking retailers (Altenew, Hero Arts, Concord &amp; 9th,
    /// Lawn Fawn, Ranger, Catherine Pooler, Trinity Stamps, Pink &amp; Main,
    /// Pinkfresh) ship on Shopify, so one adapter covers all of them.
    /// </summary>
    public class ShopifyCatalogProvider : ICatalogProvider
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string Domain { get; }
        public bool DefaultEnabled { get; }

        private readonly string _baseUrl;

        public ShopifyCatalogProvider(string id, string displayName, string baseUrl, bool defaultEnabled = true)
        {
            Id = id;
            DisplayName = displayName;
            _baseUrl = baseUrl.TrimEnd('/');
            Domain = new Uri(_baseUrl).Host;
            DefaultEnabled = defaultEnabled;
        }

        public async Task<List<CatalogLookupResult>> SearchAsync(string query, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<CatalogLookupResult>();

            var url = $"{_baseUrl}/search/suggest.json" +
                      $"?q={Uri.EscapeDataString(query.Trim())}" +
                      "&resources[type]=product" +
                      "&resources[limit]=15" +
                      "&resources[options][unavailable_products]=last";

            try
            {
                using var resp = await CatalogHttpClient.Instance.GetAsync(url, ct);
                resp.EnsureSuccessStatusCode();
                using var stream = await resp.Content.ReadAsStreamAsync(ct);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

                var results = new List<CatalogLookupResult>();
                if (!doc.RootElement.TryGetProperty("resources", out var resources)) return results;
                if (!resources.TryGetProperty("results", out var resultsNode)) return results;
                if (!resultsNode.TryGetProperty("products", out var products)) return results;

                foreach (var p in products.EnumerateArray())
                {
                    var title = SafeStr(p, "title");
                    if (string.IsNullOrWhiteSpace(title)) continue;

                    var handle    = SafeStr(p, "handle");
                    var productUrl = $"{_baseUrl}{SafeStr(p, "url")?.Split('?')[0] ?? $"/products/{handle}"}";
                    var image     = SafeStr(p, "image") ?? SafeNestedStr(p, "featured_image", "url");
                    var sku       = SafeStr(p, "sku");
                    var priceStr  = SafeStr(p, "price");

                    decimal? price = null;
                    if (decimal.TryParse(priceStr, System.Globalization.NumberStyles.Number,
                            System.Globalization.CultureInfo.InvariantCulture, out var d))
                        price = d;

                    results.Add(new CatalogLookupResult
                    {
                        Name       = title.Trim(),
                        Type       = SafeStr(p, "product_type") ?? string.Empty,
                        Price      = price,
                        ImageUrl   = NormalizeImageUrl(image),
                        Url        = productUrl,
                        Handle     = handle,
                        ItemNumber = string.IsNullOrWhiteSpace(sku) ? null : sku,
                        Source     = DisplayName,
                        SourceId   = Id,
                    });
                }
                return results;
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, $"ShopifyCatalogProvider.SearchAsync({Id}, q={query})");
                return new List<CatalogLookupResult>();
            }
        }

        public async Task EnrichResultAsync(CatalogLookupResult result, CancellationToken ct = default)
        {
            if (result == null || string.IsNullOrEmpty(result.Handle)) return;

            // Shopify's product JSON endpoint: /products/{handle}.json — returns
            // images + variants without needing to parse HTML.
            var url = $"{_baseUrl}/products/{result.Handle}.json";
            try
            {
                using var resp = await CatalogHttpClient.Instance.GetAsync(url, ct);
                if (!resp.IsSuccessStatusCode) return;

                using var stream = await resp.Content.ReadAsStreamAsync(ct);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                if (!doc.RootElement.TryGetProperty("product", out var product)) return;

                // Pull all images + download as base64 to match existing inventory flow.
                var imageUrls = new List<string>();
                if (product.TryGetProperty("images", out var images))
                {
                    foreach (var img in images.EnumerateArray())
                    {
                        var src = SafeStr(img, "src");
                        if (!string.IsNullOrEmpty(src)) imageUrls.Add(NormalizeImageUrl(src) ?? src);
                    }
                }
                if (imageUrls.Count == 0 && !string.IsNullOrEmpty(result.ImageUrl)
                    && result.ImageUrl.StartsWith("http"))
                    imageUrls.Add(result.ImageUrl);

                var base64Images = new List<string>();
                foreach (var imgUrl in imageUrls.Take(8))
                {
                    var b64 = await DownloadAsBase64Async(imgUrl, ct);
                    if (!string.IsNullOrEmpty(b64)) base64Images.Add(b64);
                }
                if (base64Images.Count > 0)
                {
                    result.ImageUrl = base64Images[0];
                    result.ExtraImages.Clear();
                    if (base64Images.Count > 1) result.ExtraImages.AddRange(base64Images.Skip(1));
                }

                // Backfill SKU from first variant if search didn't have it.
                if (string.IsNullOrWhiteSpace(result.ItemNumber)
                    && product.TryGetProperty("variants", out var variants))
                {
                    foreach (var v in variants.EnumerateArray())
                    {
                        var sku = SafeStr(v, "sku");
                        if (!string.IsNullOrWhiteSpace(sku)) { result.ItemNumber = sku; break; }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, $"ShopifyCatalogProvider.EnrichResultAsync({Id}, url={result.Url})");
            }
        }

        // ── helpers ─────────────────────────────────────────────────────────

        private static string? SafeStr(JsonElement el, string prop)
            => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        private static string? SafeNestedStr(JsonElement el, string a, string b)
            => el.TryGetProperty(a, out var v1) && v1.ValueKind == JsonValueKind.Object
               && v1.TryGetProperty(b, out var v2) && v2.ValueKind == JsonValueKind.String
               ? v2.GetString() : null;

        private static string? NormalizeImageUrl(string? src)
        {
            if (string.IsNullOrEmpty(src)) return null;
            // Shopify CDN URLs sometimes ship protocol-relative.
            if (src.StartsWith("//")) return "https:" + src;
            return src;
        }

        private static async Task<string?> DownloadAsBase64Async(string url, CancellationToken ct)
        {
            try
            {
                using var resp = await CatalogHttpClient.Instance.GetAsync(url, ct);
                if (!resp.IsSuccessStatusCode) return null;
                var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
                var lower = url.ToLowerInvariant();
                var mime = lower.Contains(".jpg") || lower.Contains(".jpeg") ? "image/jpeg"
                         : lower.Contains(".webp") ? "image/webp"
                         : lower.Contains(".gif")  ? "image/gif"
                         : "image/png";
                return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
            }
            catch
            {
                return null;
            }
        }
    }
}
