using System.IO;
using System.IO.Compression;
using System.Text;
using MyCraftyStash.Data;
using MyCraftyStash.Models;
using Microsoft.EntityFrameworkCore;

namespace MyCraftyStash.Services
{
    public class ExportService
    {
        private readonly Func<InventoryDbContext> _createContext;

        public ExportService(Func<InventoryDbContext> createContext)
        {
            _createContext = createContext;
        }

        // ── Purchase report CSV ──────────────────────────────────────────────

        public Task<string> ExportPurchaseReportCsvAsync(
            List<PurchaseReportRow> rows, DateTime from, DateTime to)
        {
            var desktop  = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var fileName = $"PurchaseReport_{from:yyyyMMdd}_{to:yyyyMMdd}.csv";
            var path     = Path.Combine(desktop, fileName);

            var sb = new StringBuilder();
            sb.AppendLine("Item Name,Type,Item Number,Store,Date,Qty,Price Each,Line Total");
            foreach (var r in rows)
            {
                sb.AppendLine(string.Join(",",
                    CsvEscape(r.ItemName),
                    CsvEscape(r.ItemType),
                    CsvEscape(r.ItemNumber ?? string.Empty),
                    CsvEscape(r.PurchasedFrom ?? string.Empty),
                    r.Date.HasValue ? r.Date.Value.ToString("yyyy-MM-dd") : string.Empty,
                    r.Quantity.ToString(),
                    r.PricePerItem.ToString("F2"),
                    r.LineTotal.ToString("F2")));
            }
            // Totals row
            sb.AppendLine(string.Join(",",
                "TOTAL", "", "", "", "",
                rows.Sum(r => r.Quantity).ToString(),
                "",
                rows.Sum(r => r.LineTotal).ToString("F2")));

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            LoggingService.LogInfo($"ExportService: purchase CSV written to {path}");
            return Task.FromResult(path);
        }

        // ── Sales report CSV ─────────────────────────────────────────────────

        public Task<string> ExportSalesReportCsvAsync(
            List<SalesReportRow> rows, DateTime from, DateTime to)
        {
            var desktop  = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var fileName = $"SalesReport_{from:yyyyMMdd}_{to:yyyyMMdd}.csv";
            var path     = Path.Combine(desktop, fileName);

            var sb = new StringBuilder();
            sb.AppendLine("Item Name,Type,Item Number,Date Sold,Qty,Sale Price,Line Total");
            foreach (var r in rows)
            {
                sb.AppendLine(string.Join(",",
                    CsvEscape(r.ItemName),
                    CsvEscape(r.ItemType),
                    CsvEscape(r.ItemNumber ?? string.Empty),
                    r.Date.HasValue ? r.Date.Value.ToString("yyyy-MM-dd") : string.Empty,
                    r.Quantity.ToString(),
                    r.SalePrice.ToString("F2"),
                    r.LineTotal.ToString("F2")));
            }
            // Totals row
            sb.AppendLine(string.Join(",",
                "TOTAL", "", "", "",
                rows.Sum(r => r.Quantity).ToString(),
                "",
                rows.Sum(r => r.LineTotal).ToString("F2")));

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            LoggingService.LogInfo($"ExportService: sales CSV written to {path}");
            return Task.FromResult(path);
        }

        // ── Inventory CSV + optional images ZIP ─────────────────────────────

        public async Task<string> ExportInventoryCsvAsync(
            bool includeImages,
            IProgress<(int current, int total, string status)>? progress = null)
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var stamp   = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            // Load all items with their images
            List<Item> items;
            Dictionary<int, List<ItemImage>> imageMap = new();
            using (var ctx = _createContext())
            {
                items = await ctx.Items.OrderBy(i => i.Type).ThenBy(i => i.Name).ToListAsync();
                if (includeImages)
                {
                    var allImages = await ctx.ItemImages.ToListAsync();
                    imageMap = allImages.GroupBy(img => img.ItemId)
                                       .ToDictionary(g => g.Key, g => g.OrderBy(img => img.SortOrder).ToList());
                }
            }

            progress?.Report((0, items.Count, "Building CSV..."));

            // Build CSV
            var csvPath = Path.Combine(Path.GetTempPath(), $"JandH_Inventory_{stamp}.csv");
            var sb = new StringBuilder();
            sb.AppendLine("ID,Name,Type,Subtype,Item Number,Theme,Location,Price,Date Purchased,Purchased From,Current Stock,Stencil Layers,Pack Size,Is Discontinued,Sentiments,Notes,Created At");
            foreach (var item in items)
            {
                sb.AppendLine(string.Join(",",
                    item.Id.ToString(),
                    CsvEscape(item.Name),
                    CsvEscape(item.Type),
                    CsvEscape(item.Subtype ?? string.Empty),
                    CsvEscape(item.ItemNumber ?? string.Empty),
                    CsvEscape(item.Theme ?? string.Empty),
                    CsvEscape(item.Location ?? string.Empty),
                    item.Price.HasValue ? item.Price.Value.ToString("F2") : string.Empty,
                    item.DatePurchased.HasValue ? item.DatePurchased.Value.ToString("yyyy-MM-dd") : string.Empty,
                    CsvEscape(item.PurchasedFrom ?? string.Empty),
                    item.CurrentStock.HasValue ? item.CurrentStock.Value.ToString() : string.Empty,
                    item.StencilLayers.HasValue ? item.StencilLayers.Value.ToString() : string.Empty,
                    item.PackSize.HasValue ? item.PackSize.Value.ToString() : string.Empty,
                    item.IsDiscontinued ? "Yes" : "No",
                    CsvEscape(item.Sentiments ?? string.Empty),
                    CsvEscape(item.Notes ?? string.Empty),
                    item.CreatedAt.ToString("yyyy-MM-dd")));
            }
            File.WriteAllText(csvPath, sb.ToString(), Encoding.UTF8);

            if (!includeImages)
            {
                var destCsv = Path.Combine(desktop, $"JandH_Inventory_{stamp}.csv");
                File.Copy(csvPath, destCsv, overwrite: true);
                File.Delete(csvPath);
                LoggingService.LogInfo($"ExportService: inventory CSV written to {destCsv}");
                return destCsv;
            }

            // Build ZIP with CSV + sorted image folders
            var zipPath = Path.Combine(desktop, $"JandH_Inventory_{stamp}.zip");
            progress?.Report((0, items.Count, "Creating ZIP..."));

            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                // Add CSV
                zip.CreateEntryFromFile(csvPath, "inventory.csv");

                // Add images sorted by Type/ItemName/
                int done = 0;
                foreach (var item in items)
                {
                    done++;
                    progress?.Report((done, items.Count, $"Exporting images: {item.Name}"));

                    var allImages = new List<string>();
                    if (!string.IsNullOrEmpty(item.ImageUrl)) allImages.Add(item.ImageUrl);
                    if (imageMap.TryGetValue(item.Id, out var extras))
                        allImages.AddRange(extras.Select(img => img.ImageUrl));

                    if (allImages.Count == 0) continue;

                    var folderName = SanitizePath($"{item.Type}/{item.Name}");
                    int imgIndex = 1;
                    foreach (var base64 in allImages)
                    {
                        try
                        {
                            var commaIdx = base64.IndexOf(',');
                            var data     = commaIdx >= 0 ? base64[(commaIdx + 1)..] : base64;
                            var ext      = DetectImageExtension(base64);
                            var bytes    = Convert.FromBase64String(data);
                            var entryName = $"{folderName}/{imgIndex:D2}{ext}";
                            var entry    = zip.CreateEntry(entryName);
                            using var stream = entry.Open();
                            stream.Write(bytes, 0, bytes.Length);
                            imgIndex++;
                        }
                        catch (Exception ex)
                        {
                            LoggingService.LogWarning($"ExportService: could not write image for item {item.Id}: {ex.Message}");
                        }
                    }
                }
            }

            File.Delete(csvPath);
            LoggingService.LogInfo($"ExportService: inventory ZIP written to {zipPath}");
            return zipPath;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static string CsvEscape(string value)
        {
            // CSV-injection defense: cells starting with =, +, -, @, or tab/CR are treated
            // as formulas by Excel. Prefix with a single quote so they render as text.
            if (value.Length > 0)
            {
                var first = value[0];
                if (first == '=' || first == '+' || first == '-' || first == '@' || first == '\t' || first == '\r')
                    value = "'" + value;
            }
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }

        private static string SanitizePath(string path)
        {
            var invalid = Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\' }).ToArray();
            // Keep slashes but sanitize each segment
            return string.Join("/", path.Split('/').Select(seg =>
                new string(seg.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim()));
        }

        private static string DetectImageExtension(string base64)
        {
            if (base64.StartsWith("data:image/png"))  return ".png";
            if (base64.StartsWith("data:image/webp")) return ".webp";
            if (base64.StartsWith("data:image/gif"))  return ".gif";
            return ".jpg"; // default
        }
    }
}
