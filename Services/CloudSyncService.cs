using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ImageMagick;
using Microsoft.EntityFrameworkCore;
using MyCraftyStash.Data;
using MyCraftyStash.Models;

namespace MyCraftyStash.Services
{
    /// <summary>
    /// Pushes the local SQLite stash up to the SWA-backed cloud API so the user
    /// can view it from their phone via the read-only web UI.
    ///
    /// Design rules:
    ///   - The desktop is source of truth. The cloud is a derived read replica
    ///     for the user's own viewing; never the other way around.
    ///   - Idempotent: every sync is a full enumeration, but per-item hashing
    ///     skips unchanged rows so the steady-state cost is one HEAD-ish loop.
    ///   - Resilient to interruption: hash state is committed only after the
    ///     server confirms the upload, so a killed sync resumes cleanly.
    ///   - Image bytes are decoded, resized to 1200px-max, re-encoded JPEG q85
    ///     before upload. Keeps blob sizes small without the desktop having to
    ///     store thumbnails separately.
    ///
    /// Auth: the user pastes an mcs_... API key once in Settings. We DPAPI-
    /// encrypt it before storing in settings.db, so the raw key is only ever
    /// in process memory while a sync is running.
    /// </summary>
    public static class CloudSyncService
    {
        // Storage keys in settings.db.kv_settings.
        private const string KeyEndpoint     = "CloudSync.Endpoint";
        private const string KeyApiKeyDpapi  = "CloudSync.ApiKeyDpapi";
        private const string KeyLastSyncUtc  = "CloudSync.LastSyncUtc";
        private const string KeyLastItems    = "CloudSync.LastSyncItems";
        private const string KeyLastImages   = "CloudSync.LastSyncImages";
        private const string KeyItemHashes   = "CloudSync.ItemHashes";

        // Server cap is 200 (UploadFunction.MaxItemsPerBatch); we send half
        // that. SWA managed functions abort any request that takes longer
        // than 45 seconds, so smaller batches keep each request well clear
        // of the ceiling even when the SQL tier is cold or busy.
        private const int ItemsPerBatch = 100;

        // One retry after a short pause covers Function cold starts and
        // transient 500s without turning a genuinely broken deploy into a
        // long hang.
        private const int BatchRetryDelayMs = 3000;

        // Max edge for re-encoded image. 1200 hits a sweet spot for phone
        // viewing without bloating storage; raising this is a settings choice
        // we can expose later if users complain about detail loss.
        private const int MaxImageEdgePx = 1200;
        private const int JpegQuality = 85;

        // Process-wide HttpClient. Lazy so the very first action that touches
        // the service (usually loading the Settings tab) doesn't pay for it.
        private static readonly Lazy<HttpClient> _http = new(() =>
        {
            var h = new HttpClient
            {
                // Image uploads can be slow on residential uplink. 5 min covers
                // even an unusually large blob over a slow connection.
                Timeout = TimeSpan.FromMinutes(5),
            };
            h.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("MyCraftyStash", "1.0"));
            return h;
        });

        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        // ── Settings I/O ────────────────────────────────────────────────────

        public static CloudSyncSettings Load()
        {
            var s = new CloudSyncSettings
            {
                EndpointUrl = UserSettingsService.GetSettingValue(KeyEndpoint),
                HasApiKey = !string.IsNullOrEmpty(UserSettingsService.GetSettingValue(KeyApiKeyDpapi)),
                LastSyncItemCount = ParseIntOr(UserSettingsService.GetSettingValue(KeyLastItems), 0),
                LastSyncImageCount = ParseIntOr(UserSettingsService.GetSettingValue(KeyLastImages), 0),
            };

            var rawUtc = UserSettingsService.GetSettingValue(KeyLastSyncUtc);
            if (DateTime.TryParse(rawUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var ts))
                s.LastSyncUtc = ts;

            var rawHashes = UserSettingsService.GetSettingValue(KeyItemHashes);
            if (!string.IsNullOrEmpty(rawHashes))
            {
                try
                {
                    s.ItemHashes = JsonSerializer.Deserialize<Dictionary<int, string>>(rawHashes)
                        ?? new Dictionary<int, string>();
                }
                catch (Exception ex)
                {
                    LoggingService.LogWarning($"CloudSyncService.Load: corrupt hash json, resetting. {ex.Message}");
                    s.ItemHashes = new Dictionary<int, string>();
                }
            }

            return s;
        }

        public static void SaveEndpoint(string? endpointUrl) =>
            UserSettingsService.SetSettingValue(KeyEndpoint, NormalizeEndpoint(endpointUrl));

        /// <summary>Encrypts and persists the API key. Pass null/empty to clear.</summary>
        public static void SaveApiKey(string? plaintext)
        {
            if (string.IsNullOrEmpty(plaintext))
            {
                UserSettingsService.SetSettingValue(KeyApiKeyDpapi, null);
                return;
            }
            try
            {
                var bytes = Encoding.UTF8.GetBytes(plaintext);
                // CurrentUser scope: only this Windows account can decrypt.
                // Roaming this would defeat the point of DPAPI; the user is
                // expected to mint a fresh key on each new desktop install.
                var enc = ProtectedData.Protect(bytes, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
                UserSettingsService.SetSettingValue(KeyApiKeyDpapi, Convert.ToBase64String(enc));
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "CloudSyncService.SaveApiKey");
                throw;
            }
        }

        /// <summary>Decrypts and returns the API key, or null if none stored.
        /// Callers should hold this in a local string only as long as needed.</summary>
        public static string? LoadApiKey()
        {
            var b64 = UserSettingsService.GetSettingValue(KeyApiKeyDpapi);
            if (string.IsNullOrEmpty(b64)) return null;
            try
            {
                var enc = Convert.FromBase64String(b64);
                var dec = ProtectedData.Unprotect(enc, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(dec);
            }
            catch (Exception ex)
            {
                // If a profile changes hands or the DPAPI master key was lost
                // (e.g. user restored from a system image) the key is no longer
                // decryptable. Wipe it so the user is prompted to re-paste.
                LoggingService.LogWarning($"CloudSyncService.LoadApiKey: decrypt failed, wiping. {ex.Message}");
                UserSettingsService.SetSettingValue(KeyApiKeyDpapi, null);
                return null;
            }
        }

        public static void ClearLocalState()
        {
            UserSettingsService.SetSettingValue(KeyApiKeyDpapi, null);
            UserSettingsService.SetSettingValue(KeyLastSyncUtc, null);
            UserSettingsService.SetSettingValue(KeyLastItems, null);
            UserSettingsService.SetSettingValue(KeyLastImages, null);
            UserSettingsService.SetSettingValue(KeyItemHashes, null);
        }

        // ── Network calls ───────────────────────────────────────────────────

        public sealed record WhoAmIResult(string UserId, string? UserDetails, string? IdentityProvider);

        /// <summary>Calls GET /api/whoami with the supplied key. Returns the
        /// identity on success, throws HttpRequestException on failure with
        /// a message safe to show in the UI.</summary>
        public static async Task<WhoAmIResult> TestConnectionAsync(
            string endpointUrl, string apiKey, CancellationToken ct = default)
        {
            var url = NormalizeEndpoint(endpointUrl) + "/api/whoami";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("X-Api-Key", apiKey);

            HttpResponseMessage resp;
            try
            {
                resp = await _http.Value.SendAsync(req, ct);
            }
            catch (Exception ex)
            {
                throw new HttpRequestException(
                    $"Could not reach {url}. Check the endpoint URL and your internet connection. ({ex.Message})", ex);
            }

            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                throw new HttpRequestException(
                    "The server rejected this API key. Mint a fresh one from the website and paste it here.");
            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"Server returned {(int)resp.StatusCode} {resp.ReasonPhrase}.");

            var body = await resp.Content.ReadFromJsonAsync<WhoAmIResult>(_jsonOpts, ct);
            if (body is null || string.IsNullOrEmpty(body.UserId))
                throw new HttpRequestException("Server response was empty or malformed.");
            return body;
        }

        // ── Sync ────────────────────────────────────────────────────────────

        public sealed class SyncProgress
        {
            public string Stage { get; set; } = "";
            public int Processed { get; set; }
            public int Total { get; set; }
            public int ImagesUploaded { get; set; }

            // 0..100, for binding to a ProgressBar.
            public int Percent => Total <= 0 ? 0 : (int)Math.Min(100, Math.Round(100.0 * Processed / Total));
        }

        public sealed class SyncResult
        {
            public int ItemsScanned { get; set; }
            public int ItemsUploaded { get; set; }
            public int ItemsSkippedUnchanged { get; set; }
            public int ImagesUploaded { get; set; }
            public List<string> Errors { get; } = new();
        }

        /// <summary>
        /// Full sync: enumerate items, hash each, upload changed ones in
        /// batches, then upload images for changed items. Updates the hash
        /// dict as it goes so a re-run after interrupt resumes cleanly.
        /// </summary>
        public static async Task<SyncResult> SyncAllAsync(
            IProgress<SyncProgress>? progress = null,
            CancellationToken ct = default)
        {
            var endpoint = UserSettingsService.GetSettingValue(KeyEndpoint);
            var apiKey = LoadApiKey();
            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey))
                throw new InvalidOperationException(
                    "Cloud Sync is not configured. Set your endpoint URL and paste an API key first.");

            var result = new SyncResult();
            var settings = Load();
            var hashes = settings.ItemHashes;

            // 1. Read all items + their primary image into memory. Stash sizes
            //    we've seen so far (~hundreds to low thousands of rows) fit
            //    comfortably; if this stops being true we can stream instead.
            List<Item> items;
            using (var ctx = new InventoryDbContext())
            {
                items = await ctx.Items
                    .AsNoTracking()
                    .OrderBy(i => i.Id)
                    .ToListAsync(ct);
            }

            result.ItemsScanned = items.Count;
            progress?.Report(new SyncProgress { Stage = "Comparing", Total = items.Count });

            // 2. Diff: which items changed (or are new) since last sync?
            var changed = new List<(Item Item, string Hash)>();
            foreach (var item in items)
            {
                ct.ThrowIfCancellationRequested();
                var h = ComputeHash(item);
                if (!hashes.TryGetValue(item.Id, out var prior) || prior != h)
                    changed.Add((item, h));
                else
                    result.ItemsSkippedUnchanged++;
            }

            // 3. Upload changed items in batches. Track which ids actually
            //    made it: image uploads are gated on this, because the server
            //    404s an image whose item row doesn't exist yet. Without the
            //    gate, one failed batch of 100 items used to cascade into 100
            //    noisy image errors.
            var uploadedIds = new HashSet<int>();
            if (changed.Count > 0)
            {
                progress?.Report(new SyncProgress
                {
                    Stage = $"Uploading items (0 of {changed.Count})",
                    Total = changed.Count,
                });

                for (int i = 0; i < changed.Count; i += ItemsPerBatch)
                {
                    ct.ThrowIfCancellationRequested();
                    var slice = changed.Skip(i).Take(ItemsPerBatch).ToList();

                    try
                    {
                        await WithOneRetryAsync(
                            () => UploadItemBatchAsync(endpoint!, apiKey, slice.Select(x => x.Item).ToList(), ct),
                            $"item batch {i}", ct);
                        result.ItemsUploaded += slice.Count;

                        // Commit hashes for this batch only after the server
                        // ACKs it, and only for items with no image to send.
                        // Image-bearing items get their hash committed after
                        // the image also lands (step 4); otherwise a failed
                        // image upload would be silently skipped on the next
                        // run because the hash already matched.
                        foreach (var (item, hash) in slice)
                        {
                            uploadedIds.Add(item.Id);
                            if (!HasInlineImage(item.ImageUrl))
                                hashes[item.Id] = hash;
                        }
                        PersistHashes(hashes);
                    }
                    catch (Exception ex)
                    {
                        LoggingService.LogError(ex, $"CloudSyncService: item batch starting at {i} failed");
                        result.Errors.Add($"Item batch {i}: {ex.Message}");
                    }

                    progress?.Report(new SyncProgress
                    {
                        Stage = $"Uploading items ({Math.Min(i + ItemsPerBatch, changed.Count)} of {changed.Count})",
                        Processed = Math.Min(i + ItemsPerBatch, changed.Count),
                        Total = changed.Count,
                    });
                }
            }

            // 4. Upload images, but only for items whose row upload succeeded.
            var withImages = changed
                .Where(x => uploadedIds.Contains(x.Item.Id) && HasInlineImage(x.Item.ImageUrl))
                .ToList();
            if (withImages.Count > 0)
            {
                progress?.Report(new SyncProgress
                {
                    Stage = $"Uploading images (0 of {withImages.Count})",
                    Total = withImages.Count,
                });

                int done = 0;
                foreach (var (item, hash) in withImages)
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        var bytes = ReEncode(item.ImageUrl!);
                        if (bytes is not null)
                        {
                            await WithOneRetryAsync(
                                () => UploadImageAsync(endpoint!, apiKey, item.Id, "primary", bytes, ct),
                                $"image item {item.Id}", ct);
                            result.ImagesUploaded++;
                        }

                        // Item row + image both confirmed (or the image was
                        // undecodable and will never succeed): commit the hash
                        // so this item is skipped next run.
                        hashes[item.Id] = hash;
                        PersistHashes(hashes);
                    }
                    catch (Exception ex)
                    {
                        LoggingService.LogError(ex, $"CloudSyncService: image upload failed for item {item.Id}");
                        result.Errors.Add($"Image item {item.Id}: {ex.Message}");
                    }
                    done++;
                    progress?.Report(new SyncProgress
                    {
                        Stage = $"Uploading images ({done} of {withImages.Count})",
                        Processed = done,
                        Total = withImages.Count,
                        ImagesUploaded = result.ImagesUploaded,
                    });
                }
            }

            // 5. Stamp the last-sync metadata.
            UserSettingsService.SetSettingValue(KeyLastSyncUtc,
                DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            UserSettingsService.SetSettingValue(KeyLastItems, result.ItemsUploaded.ToString());
            UserSettingsService.SetSettingValue(KeyLastImages, result.ImagesUploaded.ToString());

            LoggingService.LogInfo(
                $"CloudSync done: scanned={result.ItemsScanned} uploaded={result.ItemsUploaded} " +
                $"skipped={result.ItemsSkippedUnchanged} images={result.ImagesUploaded} " +
                $"errors={result.Errors.Count}");

            return result;
        }

        // ── HTTP helpers ────────────────────────────────────────────────────

        /// <summary>Runs the action; on failure waits briefly and tries once
        /// more. Covers Function cold starts and transient 500s. Cancellation
        /// is never retried.</summary>
        private static async Task WithOneRetryAsync(
            Func<Task> action, string label, CancellationToken ct)
        {
            try
            {
                await action();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LoggingService.LogWarning($"CloudSyncService: {label} failed, retrying once. {ex.Message}");
                await Task.Delay(BatchRetryDelayMs, ct);
                await action();
            }
        }

        private static async Task UploadItemBatchAsync(
            string endpoint, string apiKey, List<Item> items, CancellationToken ct)
        {
            var payload = new
            {
                items = items.Select(i => new
                {
                    localId = i.Id,
                    name = i.Name,
                    type = i.Type,
                    subtype = i.Subtype,
                    theme = i.Theme,
                    sentiments = i.Sentiments,
                    itemNumber = i.ItemNumber,
                    price = i.Price,
                    datePurchased = i.DatePurchased,
                    isDiscontinued = i.IsDiscontinued,
                    stencilLayers = i.StencilLayers,
                    packSize = i.PackSize,
                    currentStock = i.CurrentStock,
                    purchasedFrom = i.PurchasedFrom,
                    location = i.Location,
                    notes = i.Notes,
                    siteUrl = i.SiteUrl,
                }).ToList(),
            };

            using var req = new HttpRequestMessage(HttpMethod.Post,
                NormalizeEndpoint(endpoint) + "/api/upload/items");
            req.Headers.Add("X-Api-Key", apiKey);
            // Serialize up front and send as StringContent so the request
            // carries a Content-Length header. JsonContent streams with
            // Transfer-Encoding: chunked, and the Static Web Apps proxy
            // rejects chunked bodies to managed functions with an instant
            // 500 "Backend call failure". Cost us a whole evening; verified
            // by an A/B test of the identical payload both ways.
            var json = JsonSerializer.Serialize(payload, _jsonOpts);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var resp = await _http.Value.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await SafeReadBodyAsync(resp, ct);
                throw new HttpRequestException(
                    $"upload/items {(int)resp.StatusCode}: {body}");
            }
        }

        private static async Task UploadImageAsync(
            string endpoint, string apiKey, int localItemId, string kind,
            byte[] bytes, CancellationToken ct)
        {
            var url = NormalizeEndpoint(endpoint) + $"/api/upload/image?localItemId={localItemId}&kind={kind}";
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Add("X-Api-Key", apiKey);
            req.Content = new ByteArrayContent(bytes);
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");

            using var resp = await _http.Value.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await SafeReadBodyAsync(resp, ct);
                throw new HttpRequestException(
                    $"upload/image item={localItemId} {(int)resp.StatusCode}: {body}");
            }
        }

        private static async Task<string> SafeReadBodyAsync(HttpResponseMessage resp, CancellationToken ct)
        {
            try
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                return body.Length > 400 ? body[..400] : body;
            }
            catch { return "(unreadable body)"; }
        }

        // ── Hashing + image re-encode ───────────────────────────────────────

        /// <summary>Stable hash of every uploadable field on an item, including
        /// the raw image data URI. Changes to either metadata or the image bump
        /// the hash and re-trigger upload.</summary>
        private static string ComputeHash(Item i)
        {
            // Pipe-delimited fields keep the input compact and unambiguous.
            // Nullables become empty strings; DateTime is ISO-8601 invariant.
            var sb = new StringBuilder(512);
            sb.Append(i.Name).Append('|').Append(i.Type).Append('|')
              .Append(i.Subtype).Append('|').Append(i.Theme).Append('|')
              .Append(i.Sentiments).Append('|').Append(i.ItemNumber).Append('|')
              .Append(i.Price?.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('|')
              .Append(i.DatePurchased?.ToString("O", System.Globalization.CultureInfo.InvariantCulture)).Append('|')
              .Append(i.IsDiscontinued).Append('|')
              .Append(i.StencilLayers).Append('|').Append(i.PackSize).Append('|')
              .Append(i.CurrentStock).Append('|').Append(i.PurchasedFrom).Append('|')
              .Append(i.Location).Append('|').Append(i.Notes).Append('|')
              .Append(i.SiteUrl).Append('|');

            // Image bytes hash separately so we don't redo huge SHA-256 work
            // on the whole base64 string for unchanged photos: just take the
            // length and a sampled prefix. Collisions here only cost an
            // unneeded re-upload, never data loss.
            if (!string.IsNullOrEmpty(i.ImageUrl))
            {
                sb.Append("img:").Append(i.ImageUrl.Length).Append(':');
                int sample = Math.Min(128, i.ImageUrl.Length);
                sb.Append(i.ImageUrl, 0, sample);
            }

            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
            return Convert.ToHexString(hash);
        }

        private static bool HasInlineImage(string? url) =>
            !string.IsNullOrEmpty(url) && url.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Decodes a "data:image/...;base64,..." URI, resizes to
        /// MaxImageEdgePx max edge, re-encodes JPEG q85, returns the bytes.
        /// Returns null and logs on any decode/encode failure.
        /// </summary>
        private static byte[]? ReEncode(string dataUri)
        {
            try
            {
                var comma = dataUri.IndexOf(',');
                if (comma < 0) return null;
                var b64 = dataUri[(comma + 1)..];
                var raw = Convert.FromBase64String(b64);

                using var img = new MagickImage(raw);
                // Greater=true means "only shrink, never enlarge." Preserves
                // aspect via the bounding box.
                img.Resize(new MagickGeometry(MaxImageEdgePx, MaxImageEdgePx) { Greater = true });
                img.Format = MagickFormat.Jpeg;
                img.Quality = JpegQuality;
                return img.ToByteArray();
            }
            catch (Exception ex)
            {
                LoggingService.LogWarning($"CloudSyncService.ReEncode: {ex.Message}");
                return null;
            }
        }

        // ── Misc helpers ────────────────────────────────────────────────────

        private static void PersistHashes(Dictionary<int, string> hashes)
        {
            try
            {
                var json = JsonSerializer.Serialize(hashes);
                UserSettingsService.SetSettingValue(KeyItemHashes, json);
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "CloudSyncService.PersistHashes");
            }
        }

        private static string NormalizeEndpoint(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;
            var trimmed = url.Trim();
            // Strip a trailing slash so we can build URLs by concatenating "/api/...".
            while (trimmed.EndsWith('/')) trimmed = trimmed[..^1];
            return trimmed;
        }

        private static int ParseIntOr(string? raw, int fallback) =>
            int.TryParse(raw, out var n) ? n : fallback;
    }
}
