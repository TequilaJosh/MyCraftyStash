using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

using JandH.Core.Models;
using JandH.Core.Services;
using JandH.Core.ViewModels;

namespace MyCraftyStash.Converters
{
    public class Base64ImageConverter : IValueConverter
    {
        private static readonly ConcurrentDictionary<string, WeakReference<BitmapImage>> _cache = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string imageSource || string.IsNullOrEmpty(imageSource))
                return null;

            try
            {
                int decodePixelWidth = 0;
                if (parameter is string paramStr && int.TryParse(paramStr, out int pw))
                {
                    decodePixelWidth = pw;
                }
                else if (parameter is int pi)
                {
                    decodePixelWidth = pi;
                }
                else if (parameter is double pd)
                {
                    decodePixelWidth = (int)pd;
                }

                const int MAX_DECODE_WIDTH = 800;
                int effectiveWidth = decodePixelWidth > 0 ? decodePixelWidth : MAX_DECODE_WIDTH;

                var cacheKey = GetCacheKey(imageSource, effectiveWidth);
                if (_cache.TryGetValue(cacheKey, out var weakRef) && weakRef.TryGetTarget(out var cachedBitmap))
                {
                    return cachedBitmap;
                }

                BitmapImage? bitmap = null;

                if (imageSource.StartsWith("data:image"))
                {
                    int commaIndex = imageSource.IndexOf(',');
                    if (commaIndex > 0)
                    {
                        string base64Data = imageSource.Substring(commaIndex + 1);
                        byte[] imageBytes = System.Convert.FromBase64String(base64Data);
                        
                        using var ms = new MemoryStream(imageBytes);
                        bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = ms;
                        bitmap.DecodePixelWidth = effectiveWidth;
                        bitmap.EndInit();
                        bitmap.Freeze();
                    }
                }
                else
                {
                    bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(imageSource, UriKind.RelativeOrAbsolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelWidth = effectiveWidth;
                    bitmap.EndInit();
                    bitmap.Freeze();
                }

                if (bitmap != null)
                {
                    _cache[cacheKey] = new WeakReference<BitmapImage>(bitmap);
                }

                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        private static string GetCacheKey(string imageSource, int width)
        {
            var keyLength = Math.Min(imageSource.Length, 64);
            return $"{width}_{imageSource.Length}_{imageSource.Substring(0, keyLength).GetHashCode()}";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
