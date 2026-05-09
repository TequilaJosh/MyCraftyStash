using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace MyCraftyStash.Services
{
    /// <summary>
    /// One item scraped from a public Taylored Expressions wishlist page.
    /// </summary>
    public class TeWishlistItem : INotifyPropertyChanged
    {
        public string  Name       { get; set; } = string.Empty;
        public string? ItemNumber { get; set; }
        public string? Type       { get; set; }
        public decimal? Price     { get; set; }
        public string? ImageUrl   { get; set; }
        public string? ProductUrl { get; set; }

        private bool _selected = true;
        public bool Selected
        {
            get => _selected;
            set { _selected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected))); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>
    /// Result of <see cref="TeWishlistImportService.FetchWishlistAsync"/>.
    /// Carries the parsed items plus, when present, the public wishlist's
    /// title (e.g. "Glitters and Enamel Dots") so the UI can pre-fill a
    /// new local list with the same name.
    /// </summary>
    public class TeWishlistFetchResult
    {
        public string?              Title { get; set; }
        public List<TeWishlistItem> Items { get; set; } = new();
    }

    public class TeWishlistImportService
    {
        // AutomaticDecompression handles gzip/brotli so ReadAsStringAsync returns plain text.
        private static readonly HttpClient _http = new HttpClient(
            new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
                AllowAutoRedirect      = true,
            })
        {
            Timeout = TimeSpan.FromSeconds(30),
            DefaultRequestHeaders =
            {
                { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36" },
                { "Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8" },
                { "Accept-Language", "en-US,en;q=0.9" },
                { "Upgrade-Insecure-Requests", "1" },
            }
        };

        // Debug log written directly to AppData - guaranteed to work even if network share is down.
        private static readonly string _debugLog = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MyCraftyStash", "wishlist_debug.log");

        private static void DebugLog(string message)
        {
            try
            {
                var dir = Path.GetDirectoryName(_debugLog)!;
                Directory.CreateDirectory(dir);
                File.AppendAllText(_debugLog,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
            }
            catch { /* never throw from diagnostics */ }
        }

        /// <summary>
        /// Fetches and parses a public TE wishlist URL.
        /// Returns the items found plus (best-effort) the public wishlist's
        /// title, or throws on network/parse failure.
        /// </summary>
        public async Task<TeWishlistFetchResult> FetchWishlistAsync(string url)
        {
            url = url.Trim();
            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                url = "https://" + url;

            DebugLog($"=== FetchWishlistAsync START === URL: {url}");
            LoggingService.LogInfo($"TeWishlistImport: starting fetch for {url}");

            string html;
            try
            {
                DebugLog("Sending HTTP GET...");
                var response = await _http.GetAsync(url);
                var statusCode = (int)response.StatusCode;
                var contentType = response.Content.Headers.ContentType?.ToString() ?? "unknown";
                DebugLog($"HTTP {statusCode} | Content-Type: {contentType}");
                LoggingService.LogInfo($"TeWishlistImport: HTTP {statusCode} from {url} | CT: {contentType}");

                html = await response.Content.ReadAsStringAsync();
                DebugLog($"Response length: {html.Length} chars");
                // Log first 2000 chars (head), then a 3000-char window where products likely live
                DebugLog($"--- HEAD ---\n{(html.Length > 2000 ? html[..2000] : html)}");
                // Look for the actual wishlist table/list - try several anchor strings
                var wishlistKeywords = new[] { "wishlist-item", "wishlistItem", "wish-list-item", "wishlist_item",
                                               "class=\"wishlist", "wishlistRow", "wl-item",
                                               "wishlist-name", "wishlist-product" };
                foreach (var kw in wishlistKeywords)
                {
                    var kwIdx = html.IndexOf(kw, StringComparison.OrdinalIgnoreCase);
                    if (kwIdx >= 0)
                    {
                        var ws = Math.Max(0, kwIdx - 200);
                        DebugLog($"--- KEYWORD '{kw}' at {kwIdx} ---\n{html.Substring(ws, Math.Min(html.Length - ws, 3000))}");
                        break;
                    }
                }
                // Also log 3000 chars from 60% and 80% into document to find product section
                foreach (var pct in new[] { 0.55, 0.70, 0.85 })
                {
                    var offset = (int)(html.Length * pct);
                    DebugLog($"--- {pct*100:0}% into HTML (index {offset}) ---\n{html.Substring(offset, Math.Min(html.Length - offset, 2000))}");
                }
                LoggingService.LogInfo($"TeWishlistImport: {html.Length} chars received");
            }
            catch (Exception ex)
            {
                DebugLog($"HTTP EXCEPTION: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                LoggingService.LogError(ex, "TeWishlistImportService.FetchWishlistAsync - HTTP");
                throw new InvalidOperationException($"Could not load the wishlist page. Check the URL and your internet connection.\n\n({ex.Message})", ex);
            }

            var items = ParseWishlistHtml(html);
            var title = ParseWishlistTitle(html);
            DebugLog($"ParseWishlistHtml returned {items.Count} items, title='{title ?? "(none)"}'");
            LoggingService.LogInfo($"TeWishlistImport: parsed {items.Count} items from {html.Length} chars of HTML; title='{title ?? "(none)"}'");

            if (items.Count == 0)
            {
                DebugLog("No items parsed - throwing exception back to UI");
                throw new InvalidOperationException("No items were found on that page. Make sure the wishlist is public and the URL is correct.");
            }

            DebugLog($"=== FetchWishlistAsync DONE === {items.Count} items returned");
            return new TeWishlistFetchResult { Title = title, Items = items };
        }

        // BigCommerce public wishlist pages render the list name as
        // "Wish List: My List Name" inside an <h1> (sometimes <h2>). We
        // also fall back to the document <title> if the heading is absent.
        // Returns the title with the "Wish List:" prefix stripped, or
        // null when no usable name can be extracted.
        private static string? ParseWishlistTitle(string html)
        {
            // Prefer a heading (h1/h2/h3) — that's where the human-visible
            // "Wish List: …" label lives.
            var headingRx = new Regex(
                @"<h[1-3][^>]*>\s*(?:<[^>]+>\s*)*Wish\s*List\s*:\s*([^<\r\n]+?)\s*(?:<|$)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var hm = headingRx.Match(html);
            if (hm.Success)
            {
                var t = HtmlDecode(hm.Groups[1].Value);
                if (!string.IsNullOrWhiteSpace(t)) return t;
            }

            // <title> fallback — typically "Wish List: Foo - Site Name".
            // Strip the trailing site suffix and any "Wish List:" prefix.
            var titleRx = new Regex(@"<title[^>]*>([^<]+)</title>",
                                    RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var tm = titleRx.Match(html);
            if (tm.Success)
            {
                var raw = HtmlDecode(tm.Groups[1].Value);
                // Drop "Wish List:" / "Wishlist:" prefix if present.
                raw = Regex.Replace(raw, @"^\s*Wish\s*list\s*:\s*", "", RegexOptions.IgnoreCase).Trim();
                // Drop trailing "- Taylored Expressions" / "| Taylored Expressions".
                raw = Regex.Replace(raw, @"\s*[-|·–—]\s*Taylored\s*Expressions.*$", "",
                                    RegexOptions.IgnoreCase).Trim();
                if (!string.IsNullOrWhiteSpace(raw) &&
                    !raw.Equals("Wish List", StringComparison.OrdinalIgnoreCase))
                    return raw;
            }

            return null;
        }

        // Actual BigCommerce wishlist.php card structure (confirmed from live page):
        //   <h2 class="card-title card-ellipsis">
        //     <a href="https://www.tayloredexpressions.com/product-url/" ...>
        //       <span>Product Name</span>
        //     </a>
        //   </h2>
        //   <span data-product-price-without-tax="" class="price price--withoutTax">$22.00</span>
        //   <img src="...">
        //
        // Name text is inside a <span> within the <a>, NOT direct text content.

        // Matches <a ...><span>Name</span></a> OR <a ...>Name</a> (both forms)
        private const string AnchorNamePattern =
            @"<a\s+[^>]*href=""([^""]+)""[^>]*>\s*(?:<span[^>]*>)?\s*([^<]{2,200})\s*(?:</span>)?\s*</a>";

        private static List<TeWishlistItem> ParseWishlistHtml(string html)
        {
            var items = new List<TeWishlistItem>();

            // ── Strategy 1: <h2 class="card-title ..."> with span-wrapped anchor ──
            // This is the confirmed BigCommerce wishlist card structure.
            // Skip cards where href contains "javascript:" or "#" (sample/nav cards).
            var cardTitleMatches = Regex.Matches(html,
                @"<h[1-6][^>]*class=""[^""]*card-title[^""]*""[^>]*>\s*" + AnchorNamePattern,
                RegexOptions.Singleline | RegexOptions.IgnoreCase);

            DebugLog($"Strategy 1 (card-title heading): found {cardTitleMatches.Count} candidates");
            foreach (Match m in cardTitleMatches)
            {
                var item = ParseFromNameMatch(html, m, m.Groups[1].Value, m.Groups[2].Value);
                if (item != null) items.Add(item);
            }

            // ── Strategy 2: wishlist-item-name headings ────────────────────────────
            if (items.Count == 0)
            {
                var nameMatches = Regex.Matches(html,
                    @"<h[1-6][^>]*class=""[^""]*wishlist-item-name[^""]*""[^>]*>\s*" + AnchorNamePattern,
                    RegexOptions.Singleline | RegexOptions.IgnoreCase);

                DebugLog($"Strategy 2 (wishlist-item-name): found {nameMatches.Count} candidates");
                foreach (Match m in nameMatches)
                {
                    var item = ParseFromNameMatch(html, m, m.Groups[1].Value, m.Groups[2].Value);
                    if (item != null) items.Add(item);
                }
            }

            // ── Strategy 3: any heading + absolute TE URL in body ─────────────────
            if (items.Count == 0)
            {
                var bodyHtml   = html.Length > 90_000 ? html[90_000..] : html;
                var bodyOffset = html.Length > 90_000 ? 90_000 : 0;

                var nameMatches = Regex.Matches(bodyHtml,
                    @"<h[1-6][^>]*>\s*" + AnchorNamePattern + @"\s*</h[1-6]>",
                    RegexOptions.Singleline | RegexOptions.IgnoreCase);

                DebugLog($"Strategy 3 (any heading in body): found {nameMatches.Count} candidates");
                foreach (Match m in nameMatches)
                {
                    var item = ParseFromNameMatch(html, m, m.Groups[1].Value, m.Groups[2].Value,
                                                  htmlOffset: bodyOffset);
                    if (item != null) items.Add(item);
                }
            }

            DebugLog($"ParseWishlistHtml final: {items.Count} unique items");

            // Deduplicate by name
            return items
                .GroupBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
        }

        private static TeWishlistItem? ParseFromNameMatch(string fullHtml, Match m,
            string rawUrl, string rawNameText, int htmlOffset = 0)
        {
            var rawName    = HtmlDecode(rawNameText.Trim());
            var productUrl = rawUrl.Trim();

            if (string.IsNullOrWhiteSpace(rawName) || rawName.Length < 2) return null;
            if (IsJunkLink(rawName, productUrl)) return null;

            // Ensure absolute URL
            if (productUrl.StartsWith("/"))
                productUrl = "https://www.tayloredexpressions.com" + productUrl;
            else if (!productUrl.StartsWith("http"))
                return null;

            // Context window around this heading: 600 chars before, 2000 chars after
            var absIdx   = m.Index + htmlOffset;
            var ctxStart = Math.Max(0, absIdx - 600);
            var ctxLen   = Math.Min(fullHtml.Length - ctxStart, m.Length + 2000);
            var ctx      = fullHtml.Substring(ctxStart, ctxLen);

            var item = new TeWishlistItem { Name = rawName, ProductUrl = productUrl };
            ExtractPriceAndImage(ctx, item);
            item.Type = GuessType(rawName);
            return item;
        }

        private static void ExtractPriceAndImage(string ctx, TeWishlistItem item)
        {
            // BigCommerce price: <span class="price price--withoutTax">$22.00</span>
            // Also handles sale prices, "Now: $X", and bare "$X.XX"
            var priceMatch = Regex.Match(ctx,
                @"<span[^>]*class=""[^""]*price[^""]*""[^>]*>\s*\$\s*([\d,]+\.[\d]{2})\s*</span>",
                RegexOptions.IgnoreCase);
            if (!priceMatch.Success)
                priceMatch = Regex.Match(ctx,
                    @"(?:Now|Was|Sale|Price)\s*:?\s*\$\s*([\d,]+\.[\d]{2})",
                    RegexOptions.IgnoreCase);
            if (!priceMatch.Success)
                priceMatch = Regex.Match(ctx, @"\$\s*([\d,]+\.[\d]{2})");
            if (priceMatch.Success &&
                decimal.TryParse(priceMatch.Groups[1].Value.Replace(",", ""),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var price))
                item.Price = price;

            // Image
            var imgMatch = Regex.Match(ctx,
                @"<img\s+[^>]*src=""([^""]+(?:jpg|jpeg|png|webp|PNG|JPG)[^""]*)""",
                RegexOptions.IgnoreCase);
            if (imgMatch.Success)
            {
                var src = imgMatch.Groups[1].Value;
                item.ImageUrl = src.StartsWith("http") ? src
                              : src.StartsWith("//")   ? "https:" + src
                              : src.StartsWith("/")    ? "https://www.tayloredexpressions.com" + src
                              : null;
            }
        }

        private static bool IsJunkLink(string name, string url) =>
            Regex.IsMatch(name,
                @"^\s*(add to cart|quick view|view details|login|register|wishlist|account|home|back|sample card|brands|sign in)\s*$",
                RegexOptions.IgnoreCase)
            || url.Contains("javascript:", StringComparison.OrdinalIgnoreCase)
            || url == "#";

        private static string? GuessType(string name)
        {
            var lower = name.ToLowerInvariant();
            if (lower.Contains("stamp set") || lower.Contains("clear stamp") || lower.Contains("stamp"))  return "Stamp";
            if (lower.Contains("stencil"))                                                                  return "Stencil";
            if (lower.Contains("die set") || lower.Contains("cutting die") || lower.Contains(" die"))      return "Die";
            if (lower.Contains("ink pad") || lower.Contains("ink cube") || lower.Contains("ink"))          return "Ink";
            if (lower.Contains("cardstock") || lower.Contains("card stock"))                               return "Cardstock";
            if (lower.Contains("embossing"))                                                                return "Embossing";
            if (lower.Contains("tool") || lower.Contains("trimmer"))                                       return "Tool";
            return null;
        }

        private static string HtmlDecode(string html) =>
            System.Net.WebUtility.HtmlDecode(html)
                .Replace("\r", " ").Replace("\n", " ")
                .Replace("  ", " ").Trim();

        /// <summary>
        /// Visits each item's product page in parallel and fills in SKU (ItemNumber),
        /// a proper category Type, and a high-resolution image URL. Best-effort —
        /// failures are logged and silently skipped.
        /// </summary>
        public async Task EnrichItemsAsync(IEnumerable<TeWishlistItem> items, int maxConcurrency = 4)
        {
            using var gate = new System.Threading.SemaphoreSlim(maxConcurrency);
            var tasks = items
                .Where(i => !string.IsNullOrWhiteSpace(i.ProductUrl))
                .Select(async i =>
                {
                    await gate.WaitAsync();
                    try { await EnrichItemAsync(i); }
                    finally { gate.Release(); }
                });
            await Task.WhenAll(tasks);
        }

        public async Task EnrichItemAsync(TeWishlistItem item)
        {
            if (string.IsNullOrWhiteSpace(item.ProductUrl)) return;
            try
            {
                using var resp = await _http.GetAsync(item.ProductUrl);
                if (!resp.IsSuccessStatusCode) return;
                var html = await resp.Content.ReadAsStringAsync();

                // SKU: <dt>SKU:</dt><dd>TE1841</dd> on this BigCommerce theme.
                var skuMatch = Regex.Match(html,
                    @"<dt[^>]*>\s*SKU:?\s*</dt>\s*<dd[^>]*>\s*([A-Z0-9][A-Z0-9\-]*)\s*</dd>",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (!skuMatch.Success)
                {
                    skuMatch = Regex.Match(html,
                        @"data-product-sku[^>]*>\s*([A-Z0-9][A-Z0-9\-]*)",
                        RegexOptions.IgnoreCase);
                }
                if (skuMatch.Success)
                    item.ItemNumber = skuMatch.Groups[1].Value.Trim();

                // Category from BreadcrumbList JSON-LD: take the second-to-last entry
                // (last is the product itself).
                var bcType = ExtractCategoryFromBreadcrumb(html);
                if (!string.IsNullOrWhiteSpace(bcType))
                    item.Type = NormalizeCategory(bcType);
                else
                    item.Type ??= GuessType(item.Name);

                // Higher-quality image from og:image.
                var ogImage = Regex.Match(html,
                    @"<meta\s+[^>]*property=""og:image""[^>]*content=""([^""]+)""",
                    RegexOptions.IgnoreCase);
                if (ogImage.Success)
                    item.ImageUrl = ogImage.Groups[1].Value;
            }
            catch (Exception ex)
            {
                LoggingService.LogWarning($"TeWishlistImport: enrichment failed for {item.ProductUrl}: {ex.Message}");
            }
        }

        private static string? ExtractCategoryFromBreadcrumb(string html)
        {
            // Match the BreadcrumbList block then pull "name" entries in order.
            var bc = Regex.Match(html,
                "\"@type\"\\s*:\\s*\"BreadcrumbList\"[^\\[]*?\"itemListElement\"\\s*:\\s*\\[(.*?)\\]\\s*\\}",
                RegexOptions.Singleline);
            if (!bc.Success) return null;

            var nameMatches = Regex.Matches(bc.Groups[1].Value, "\"name\"\\s*:\\s*\"([^\"]+)\"");
            if (nameMatches.Count < 2) return null;

            // Skip leading "Home" / "Shop"; pick the last entry that isn't the product.
            // Conventionally that's the second-to-last entry.
            var idx = nameMatches.Count - 2;
            if (idx < 0) return null;
            var raw = nameMatches[idx].Groups[1].Value;
            if (string.Equals(raw, "Home", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(raw, "Shop", StringComparison.OrdinalIgnoreCase))
                return null;
            return System.Net.WebUtility.HtmlDecode(raw);
        }

        private static string NormalizeCategory(string category)
        {
            var c = category.Trim();
            if (Regex.IsMatch(c, "stamp", RegexOptions.IgnoreCase))   return "Stamp";
            if (Regex.IsMatch(c, "stencil", RegexOptions.IgnoreCase)) return "Stencil";
            if (Regex.IsMatch(c, "die",  RegexOptions.IgnoreCase))    return "Die";
            if (Regex.IsMatch(c, "ink",  RegexOptions.IgnoreCase))    return "Ink";
            if (Regex.IsMatch(c, "card",  RegexOptions.IgnoreCase))   return "Cardstock";
            if (Regex.IsMatch(c, "paper", RegexOptions.IgnoreCase))   return "Paper";
            if (Regex.IsMatch(c, "embell", RegexOptions.IgnoreCase))  return "Embellishment";
            if (Regex.IsMatch(c, "tool|trim", RegexOptions.IgnoreCase)) return "Tool";
            return c;
        }
    }
}
