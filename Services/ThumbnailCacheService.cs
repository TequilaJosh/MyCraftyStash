using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media.Imaging;
using MyCraftyStash.Data;

namespace MyCraftyStash.Services
{
    public static class ThumbnailCacheService
    {
        private static readonly ConcurrentDictionary<int, BitmapImage?> _cache = new();
        private static readonly ConcurrentDictionary<int, long> _accessOrder = new();
        private static long _accessCounter = 0;
        // Concurrency cap for parallel decodes / disk reads. Bumped to 16 — most of the
        // work is I/O on the disk cache or short JPEG decodes, and the wizard preloads
        // hundreds of items at startup; lower values bottleneck the warm-up.
        private static readonly SemaphoreSlim _dbSemaphore = new(16);
        // Decode resolution. 150 is a compromise: still sharp on inventory cards (max
        // ~200px tall) and the wizard's 44px dropdown rows render plenty of detail,
        // while halving the per-item decode work vs the old 250.
        private static readonly int ThumbnailWidth = 150;
        // Bumped from 600 to 2000 — the wizard has dropdowns spanning Stamps, Dies,
        // Embellishments, Stacklets, Stencils, Embossing Folders, Cardstock variants,
        // OLO Markers, Watercolor, Foils. Total items can exceed 1000, and the old
        // cap caused the LRU evictions to fire mid-session, forcing re-decodes.
        private const int MaxCacheSize = 2000;

        // ── Disk cache ────────────────────────────────────────────────────────
        private static readonly string DiskCacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MyCraftyStash", "thumbcache");

        private static string DiskCachePath(int itemId, string urlHash) =>
            Path.Combine(DiskCacheDir, $"{itemId}_{urlHash}.jpg");

        /// <summary>
        /// Fast 8-char hash of the image URL used to detect stale disk cache entries.
        /// We only use the last 32 chars of the base64 payload to avoid hashing megabytes.
        /// </summary>
        private static string QuickHash(string imageUrl)
        {
            // Grab up to 64 chars from the end - unique enough to detect changes
            var tail = imageUrl.Length > 64 ? imageUrl[^64..] : imageUrl;
            return Math.Abs(tail.GetHashCode()).ToString("X8");
        }

        private static BitmapImage? TryLoadFromDisk(int itemId, string imageUrl)
        {
            try
            {
                // Try current .jpg path first, then fall back to legacy .png
                var hash = QuickHash(imageUrl);
                var jpgPath = DiskCachePath(itemId, hash);
                var pngPath = Path.Combine(DiskCacheDir, $"{itemId}_{hash}.png");
                var path = File.Exists(jpgPath) ? jpgPath : File.Exists(pngPath) ? pngPath : null;
                if (path == null) return null;
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.DecodePixelWidth = ThumbnailWidth;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, $"ThumbnailCacheService.TryLoadFromDisk (id: {itemId})");
                return null;
            }
        }

        private static void SaveToDisk(int itemId, string imageUrl, BitmapImage bmp)
        {
            try
            {
                Directory.CreateDirectory(DiskCacheDir);
                var path = DiskCachePath(itemId, QuickHash(imageUrl));
                if (File.Exists(path)) return; // already saved

                // Delete any stale variants for this item (old hashes, old .png files).
                // Per-file failures here are normal (file in use by another decode) — swallow
                // them rather than abandoning the whole save.
                foreach (var stale in Directory.GetFiles(DiskCacheDir, $"{itemId}_*"))
                    try { File.Delete(stale); } catch { }

                var encoder = new JpegBitmapEncoder { QualityLevel = 85 };
                encoder.Frames.Add(BitmapFrame.Create(bmp));
                using var fs = File.OpenWrite(path);
                encoder.Save(fs);
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, $"ThumbnailCacheService.SaveToDisk (id: {itemId})");
            }
        }

        // ── Synchronous get (cache-hit only, no DB) ─────────────────────────
        public static BitmapImage? GetThumbnail(int itemId)
        {
            if (_cache.TryGetValue(itemId, out var cached))
            {
                _accessOrder[itemId] = Interlocked.Increment(ref _accessCounter);
                return cached;
            }
            return null;
        }

        /// <summary>
        /// Returns true if this item has already been processed (image loaded or confirmed absent).
        /// Callers can use this to avoid re-triggering a load for items known to have no image.
        /// </summary>
        public static bool IsLoaded(int itemId) => _cache.ContainsKey(itemId);

        // ── Async load (used by converter, never blocks UI thread) ───────────
        public static async Task<BitmapImage?> LoadThumbnailAsync(int itemId)
        {
            if (_cache.TryGetValue(itemId, out var cached))
            {
                _accessOrder[itemId] = Interlocked.Increment(ref _accessCounter);
                return cached;
            }

            await _dbSemaphore.WaitAsync();
            try
            {
                // Double-check after acquiring semaphore
                if (_cache.TryGetValue(itemId, out cached))
                {
                    _accessOrder[itemId] = Interlocked.Increment(ref _accessCounter);
                    return cached;
                }

                string? imageUrl = await Task.Run(() =>
                {
                    using var context = new InventoryDbContext();
                    return context.Items
                        .Where(i => i.Id == itemId)
                        .Select(i => i.ImageUrl)
                        .FirstOrDefault();
                });

                if (string.IsNullOrEmpty(imageUrl))
                {
                    _cache[itemId] = null;
                    _accessOrder[itemId] = Interlocked.Increment(ref _accessCounter);
                    return null;
                }

                EvictIfNeeded();

                // Try disk cache first - avoids re-decoding base64 on every launch
                var bitmap = await Task.Run(() => TryLoadFromDisk(itemId, imageUrl))
                             ?? await Task.Run(() => DecodeBase64ToBitmap(imageUrl, ThumbnailWidth));

                // Persist to disk if we had to decode from base64. SaveToDisk catches its
                // own exceptions and logs, so the fire-and-forget here can't escape unobserved.
                if (bitmap != null)
                    _ = Task.Run(() => SaveToDisk(itemId, imageUrl, bitmap));

                _cache[itemId] = bitmap;
                _accessOrder[itemId] = Interlocked.Increment(ref _accessCounter);
                return bitmap;
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, $"ThumbnailCacheService.LoadThumbnailAsync (id: {itemId})");
                return null;
            }
            finally
            {
                _dbSemaphore.Release();
            }
        }

        // ── Kept for back-compat; prefer LoadThumbnailAsync ─────────────────
        public static BitmapImage? LoadThumbnailSync(int itemId)
        {
            // Return cache hit immediately
            if (_cache.TryGetValue(itemId, out var cached))
            {
                _accessOrder[itemId] = Interlocked.Increment(ref _accessCounter);
                return cached;
            }
            // Miss: kick off async load and return null; converter IsAsync=True will retry
            _ = LoadThumbnailAsync(itemId);
            return null;
        }

        // ── Background preloader: warm cache from (id, url) tuples ───────────
        // Faster path used by the wizard when callers already have ImageUrl on hand
        // (every WizardItemOption carries it). Skips the per-batch DB round-trip
        // entirely so the only work is disk-cache lookup + base64 decode.
        public static void PreloadAsync(IEnumerable<(int Id, string? ImageUrl)> items)
        {
            var missing = items.Where(t => t.Id > 0 && !_cache.ContainsKey(t.Id)).ToList();
            if (missing.Count == 0) return;

            _ = Task.Run(async () =>
            {
                var tasks = missing.Select(async row =>
                {
                    if (_cache.ContainsKey(row.Id)) return;
                    await _dbSemaphore.WaitAsync();
                    try
                    {
                        if (_cache.ContainsKey(row.Id)) return;
                        EvictIfNeeded();

                        BitmapImage? bmp = null;
                        if (!string.IsNullOrEmpty(row.ImageUrl))
                        {
                            bmp = TryLoadFromDisk(row.Id, row.ImageUrl)
                                  ?? DecodeBase64ToBitmap(row.ImageUrl, ThumbnailWidth);
                            if (bmp != null)
                                SaveToDisk(row.Id, row.ImageUrl, bmp);
                        }

                        _cache[row.Id] = bmp;
                        _accessOrder[row.Id] = Interlocked.Increment(ref _accessCounter);
                    }
                    finally { _dbSemaphore.Release(); }
                });
                await Task.WhenAll(tasks);
            });
        }

        // ── Background preloader: warm cache for a list of ids ───────────────
        public static void PreloadAsync(IEnumerable<int> itemIds)
        {
            var missing = itemIds.Where(id => !_cache.ContainsKey(id)).ToList();
            if (missing.Count == 0) return;

            _ = Task.Run(async () =>
            {
                // Fetch all image URLs in one DB round-trip
                List<(int Id, string? Url)> rows;
                using (var context = new InventoryDbContext())
                {
                    rows = context.Items
                        .Where(i => missing.Contains(i.Id))
                        .Select(i => new { i.Id, i.ImageUrl })
                        .AsEnumerable()
                        .Select(x => (x.Id, x.ImageUrl))
                        .ToList();
                }

                // Decode in parallel (bounded) - try disk cache first
                var tasks = rows.Select(async row =>
                {
                    if (_cache.ContainsKey(row.Id)) return;
                    await _dbSemaphore.WaitAsync();
                    try
                    {
                        if (_cache.ContainsKey(row.Id)) return;
                        EvictIfNeeded();

                        BitmapImage? bmp = null;
                        if (!string.IsNullOrEmpty(row.Url))
                        {
                            bmp = TryLoadFromDisk(row.Id, row.Url)
                                  ?? DecodeBase64ToBitmap(row.Url, ThumbnailWidth);

                            if (bmp != null)
                                SaveToDisk(row.Id, row.Url, bmp);
                        }

                        _cache[row.Id] = bmp;
                        _accessOrder[row.Id] = Interlocked.Increment(ref _accessCounter);
                    }
                    finally { _dbSemaphore.Release(); }
                });

                await Task.WhenAll(tasks);
            });
        }

        private static void EvictIfNeeded()
        {
            if (_cache.Count < MaxCacheSize) return;
            var toEvict = _accessOrder
                .OrderBy(kvp => kvp.Value)
                .Take(_cache.Count - MaxCacheSize + 50)
                .Select(kvp => kvp.Key)
                .ToList();
            foreach (var key in toEvict)
            {
                _cache.TryRemove(key, out _);
                _accessOrder.TryRemove(key, out _);
            }
        }

        public static void InvalidateItem(int itemId)
        {
            _cache.TryRemove(itemId, out _);
            _accessOrder.TryRemove(itemId, out _);
            // Remove all disk cache files for this item (any hash variant)
            try
            {
                if (Directory.Exists(DiskCacheDir))
                {
                    // Cache files are written as .jpg (see DiskCachePath); legacy entries may be .png.
                    foreach (var f in Directory.GetFiles(DiskCacheDir, $"{itemId}_*"))
                        File.Delete(f);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, $"ThumbnailCacheService.InvalidateItem disk-cleanup (id: {itemId})");
            }
        }

        public static void ClearCache()
        {
            _cache.Clear();
            _accessOrder.Clear();
        }

        private static BitmapImage? DecodeBase64ToBitmap(string imageSource, int decodePixelWidth)
        {
            try
            {
                if (imageSource.StartsWith("data:image"))
                {
                    int commaIndex = imageSource.IndexOf(',');
                    if (commaIndex > 0)
                    {
                        byte[] imageBytes = Convert.FromBase64String(imageSource.Substring(commaIndex + 1));
                        using var ms = new MemoryStream(imageBytes);
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = ms;
                        bitmap.DecodePixelWidth = decodePixelWidth;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        return bitmap;
                    }
                }

                var uriBitmap = new BitmapImage();
                uriBitmap.BeginInit();
                uriBitmap.UriSource = new Uri(imageSource, UriKind.RelativeOrAbsolute);
                uriBitmap.CacheOption = BitmapCacheOption.OnLoad;
                uriBitmap.DecodePixelWidth = decodePixelWidth;
                uriBitmap.EndInit();
                uriBitmap.Freeze();
                return uriBitmap;
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "ThumbnailCacheService.DecodeBase64ToBitmap");
                return null;
            }
        }
    }
}

