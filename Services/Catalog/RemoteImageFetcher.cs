using System.IO;
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
        // Scraped product images should be well under this. Cap the download so a
        // hostile / accidental huge response can't OOM the app or bloat SQLite.
        private const long MaxImageBytes = 15 * 1024 * 1024; // 15 MB

        public static async Task<string?> DownloadAsDataUriAsync(string url, CancellationToken ct = default)
        {
            try
            {
                using var resp = await CatalogHttpClient.Instance.GetAsync(
                    url, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!resp.IsSuccessStatusCode) return null;
                // Fast reject when the server declares an oversized body.
                if (resp.Content.Headers.ContentLength is long declared && declared > MaxImageBytes)
                {
                    LoggingService.LogWarning($"RemoteImageFetcher: {url} exceeds {MaxImageBytes} bytes (declared {declared}); skipping.");
                    return null;
                }
                var bytes = await ReadCappedAsync(resp.Content, MaxImageBytes, ct);
                if (bytes is null)
                {
                    LoggingService.LogWarning($"RemoteImageFetcher: {url} exceeded {MaxImageBytes} bytes mid-stream; skipping.");
                    return null;
                }
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

        /// <summary>Reads the response body but returns null the moment it passes
        /// maxBytes, so a chunked (no Content-Length) response can't stream an
        /// unbounded body past the cap.</summary>
        private static async Task<byte[]?> ReadCappedAsync(HttpContent content, long maxBytes, CancellationToken ct)
        {
            await using var src = await content.ReadAsStreamAsync(ct);
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await src.ReadAsync(chunk, ct)) > 0)
            {
                if (buffer.Length + read > maxBytes) return null;
                buffer.Write(chunk, 0, read);
            }
            return buffer.ToArray();
        }

        private static bool IsWebP(byte[] bytes) =>
            bytes.Length >= 12
            && bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F'
            && bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P';
    }
}
