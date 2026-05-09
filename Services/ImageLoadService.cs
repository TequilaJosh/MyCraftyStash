using System.IO;
using ImageMagick;

namespace MyCraftyStash.Services
{
    /// <summary>
    /// Loads image files (including HEIC) and returns a data-URI base64 string
    /// suitable for storing and displaying in WPF via the Base64ImageConverter.
    /// All images are resized and recompressed before being stored,
    /// keeping database rows small and preventing OutOfMemoryExceptions at load time.
    /// </summary>
    public static class ImageLoadService
    {
        // Images are displayed at 250 px wide in the app. Storing at 2× gives
        // crisp HiDPI rendering without carrying unnecessary pixel data.
        private const int MaxDimension = 500;

        // JPEG quality used for all stored images. 85 is visually lossless
        // for thumbnails while being dramatically smaller than full resolution.
        private const int JpegQuality = 85;

        // Sentiment/OCR images need more resolution to keep text legible.
        // PNG is used to avoid JPEG artifacts that confuse text recognition.
        private const int MaxSentimentDimension = 1200;

        private static readonly HashSet<string> HeicExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".heic", ".heif" };

        private static readonly HashSet<string> SupportedExtensions =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif", ".heic", ".heif"
            };

        public static bool IsSupported(string filePath) =>
            SupportedExtensions.Contains(Path.GetExtension(filePath));

        /// <summary>
        /// Reads the file, resizes it to fit within <see cref="MaxDimension"/> px on
        /// its longest edge, re-encodes it as JPEG, and returns a
        /// "data:image/jpeg;base64,..." string ready for database storage.
        /// All formats (including HEIC/HEIF) are handled via ImageMagick.
        /// </summary>
        public static string LoadAsDataUri(string filePath)
        {
            using var image = new MagickImage(filePath);
            return CompressToJpegDataUri(image, MaxDimension);
        }

        /// <summary>
        /// Compresses raw image bytes (any supported format) to a JPEG data URI.
        /// Used by callers that already have bytes in memory.
        /// </summary>
        public static string CompressBytesToDataUri(byte[] imageBytes)
        {
            using var image = new MagickImage(imageBytes);
            return CompressToJpegDataUri(image, MaxDimension);
        }

        /// <summary>
        /// Reads the file, resizes it to fit within <see cref="MaxSentimentDimension"/> px,
        /// and returns a lossless PNG data URI. Use this for images that will be processed
        /// by OCR or other text-sensitive operations where JPEG artifacts are harmful.
        /// </summary>
        public static string LoadAsPngDataUri(string filePath)
        {
            using var image = new MagickImage(filePath);
            ResizeInPlace(image, MaxSentimentDimension);
            image.Strip();
            image.Format = MagickFormat.Png;

            using var ms = new MemoryStream();
            image.Write(ms);
            return $"data:image/png;base64,{Convert.ToBase64String(ms.ToArray())}";
        }

        // ── Internal ─────────────────────────────────────────────────────────

        private static string CompressToJpegDataUri(MagickImage image, int maxDimension)
        {
            ResizeInPlace(image, maxDimension);

            // Strip EXIF/ICC metadata - not needed for stored thumbnails
            image.Strip();

            image.Format  = MagickFormat.Jpeg;
            image.Quality = JpegQuality;

            using var ms = new MemoryStream();
            image.Write(ms);
            return $"data:image/jpeg;base64,{Convert.ToBase64String(ms.ToArray())}";
        }

        private static void ResizeInPlace(MagickImage image, int maxDimension)
        {
            if (image.Width > maxDimension || image.Height > maxDimension)
            {
                image.Resize(new MagickGeometry((uint)maxDimension, (uint)maxDimension)
                {
                    IgnoreAspectRatio = false,
                    Greater = true,
                });
            }
        }

        /// <summary>OpenFileDialog filter string including HEIC.</summary>
        public static string OpenFileFilter =>
            "Image files (*.jpg;*.jpeg;*.png;*.webp;*.heic;*.heif)|*.jpg;*.jpeg;*.png;*.webp;*.heic;*.heif|All files (*.*)|*.*";
    }
}
