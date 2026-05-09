using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media.Imaging;
using MyCraftyStash.Data;

namespace MyCraftyStash.Services
{
    public static class InspirationThumbnailCacheService
    {
        private static readonly ConcurrentDictionary<int, BitmapImage?> _cache = new();
        private static readonly ConcurrentDictionary<int, long> _accessOrder = new();
        private static long _accessCounter = 0;
        private static readonly SemaphoreSlim _dbSemaphore = new(4);
        private static readonly int ThumbnailWidth = 200;
        private const int MaxCacheSize = 600;

        public static BitmapImage? GetThumbnail(int imageId)
        {
            if (_cache.TryGetValue(imageId, out var cached))
            {
                _accessOrder[imageId] = Interlocked.Increment(ref _accessCounter);
                return cached;
            }
            return null;
        }

        public static bool IsLoaded(int imageId) => _cache.ContainsKey(imageId);

        public static async Task<BitmapImage?> LoadThumbnailAsync(int imageId)
        {
            if (_cache.TryGetValue(imageId, out var cached))
            {
                _accessOrder[imageId] = Interlocked.Increment(ref _accessCounter);
                return cached;
            }

            await _dbSemaphore.WaitAsync();
            try
            {
                if (_cache.TryGetValue(imageId, out cached))
                {
                    _accessOrder[imageId] = Interlocked.Increment(ref _accessCounter);
                    return cached;
                }

                string? imageUrl = await Task.Run(() =>
                {
                    using var context = new InventoryDbContext();
                    return context.InspirationImages
                        .Where(i => i.Id == imageId)
                        .Select(i => i.ImageUrl)
                        .FirstOrDefault();
                });

                if (string.IsNullOrEmpty(imageUrl))
                {
                    _cache[imageId] = null;
                    _accessOrder[imageId] = Interlocked.Increment(ref _accessCounter);
                    return null;
                }

                EvictIfNeeded();
                var bitmap = await Task.Run(() => DecodeBase64ToBitmap(imageUrl, ThumbnailWidth));
                _cache[imageId] = bitmap;
                _accessOrder[imageId] = Interlocked.Increment(ref _accessCounter);
                return bitmap;
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, $"InspirationThumbnailCacheService.LoadThumbnailAsync (id: {imageId})");
                return null;
            }
            finally
            {
                _dbSemaphore.Release();
            }
        }

        public static BitmapImage? LoadThumbnailSync(int imageId)
        {
            if (_cache.TryGetValue(imageId, out var cached))
            {
                _accessOrder[imageId] = Interlocked.Increment(ref _accessCounter);
                return cached;
            }
            _ = LoadThumbnailAsync(imageId);
            return null;
        }

        public static void PreloadAsync(IEnumerable<int> imageIds)
        {
            var missing = imageIds.Where(id => !_cache.ContainsKey(id)).ToList();
            if (missing.Count == 0) return;

            _ = Task.Run(async () =>
            {
                List<(int Id, string? Url)> rows;
                using (var context = new InventoryDbContext())
                {
                    rows = context.InspirationImages
                        .Where(i => missing.Contains(i.Id))
                        .Select(i => new { i.Id, i.ImageUrl })
                        .AsEnumerable()
                        .Select(x => (x.Id, x.ImageUrl))
                        .ToList();
                }

                var tasks = rows.Select(async row =>
                {
                    if (_cache.ContainsKey(row.Id)) return;
                    await _dbSemaphore.WaitAsync();
                    try
                    {
                        if (_cache.ContainsKey(row.Id)) return;
                        EvictIfNeeded();
                        var bmp = string.IsNullOrEmpty(row.Url)
                            ? null
                            : DecodeBase64ToBitmap(row.Url, ThumbnailWidth);
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

        public static void InvalidateImage(int imageId)
        {
            _cache.TryRemove(imageId, out _);
            _accessOrder.TryRemove(imageId, out _);
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
            catch { return null; }
        }
    }
}
