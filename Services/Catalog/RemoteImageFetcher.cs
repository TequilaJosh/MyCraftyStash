using System.Net.Http;
using ImageMagick;

namespace MyCraftyStash.Services.Catalog
{
    /// <summary>
    /// Downloads a remote image URL and returns it as a "data:..." base64 URI
    /// that BitmapImage can decode.
    ///
    /// Why this exists: BigCommerce (and many other modern CDNs) auto-convert
    /// images to WebP based on the client's Accept header. The bytes look like
    /// PNG by URL extension but are actually WebP. WPF's BitmapImage doesn't
    /// support WebP natively, so we have to sniff the actual bytes and decode
    /// via Magick.NET when needed, re-encoding as PNG before handing back.
    /// </summary>
    public static class RemoteImageFetcher
    {
        public static async Task<string?> DownloadAsDataUriAsync(string url, CancellationToken ct = default)
        {
            try
            {
                using var resp = await CatalogHttpClient.Instance.GetAsync(url, ct);
                if (!resp.IsSuccessStatusCode) return null;
                var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
                if (bytes.Length == 0) return null;

                // Detect WebP via the RIFF/WEBP container signature (bytes 0..3
                // are "RIFF", 8..11 are "WEBP"). The CDN's labeled extension
                // and Content-Type can't be trusted here.
                if (IsWebP(bytes))
                {
                    try
                    {
                        using var img = new MagickImage(bytes);
                        img.Format = MagickFormat.Png;
                        var pngBytes = img.ToByteArray();
                        return $"data:image/png;base64,{Convert.ToBase64String(pngBytes)}";
                    }
                    catch (Exception ex)
                    {
                        LoggingService.LogError(ex, $"RemoteImageFetcher.DownloadAsDataUriAsync WebP decode ({url})");
                        return null;
                    }
                }

                // Otherwise trust the Content-Type the server returned —
                // the URL extension is unreliable.
                var mime = resp.Content.Headers.ContentType?.MediaType;
                if (string.IsNullOrEmpty(mime) || !mime.StartsWith("image/"))
                {
                    // Fall back to URL-extension sniffing as a last resort.
                    var lower = url.ToLowerInvariant();
                    mime = lower.Contains(".jpg") || lower.Contains(".jpeg") ? "image/jpeg"
                         : lower.Contains(".gif")  ? "image/gif"
                         : "image/png";
                }
                return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, $"RemoteImageFetcher.DownloadAsDataUriAsync ({url})");
                return null;
            }
        }

        private static bool IsWebP(byte[] bytes) =>
            bytes.Length >= 12
            && bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F'
            && bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P';
    }
}
