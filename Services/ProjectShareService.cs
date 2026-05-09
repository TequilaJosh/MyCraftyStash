using System.IO;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyCraftyStash.Data;
using MyCraftyStash.Models;

namespace MyCraftyStash.Services
{
    /// <summary>
    /// Bundles a Project (plus all its child rows — items, images, creations,
    /// card builds and steps) into a single .mcsproject zip file the user can
    /// attach to an email. Receivers reconstruct the project locally with
    /// <see cref="ProjectImportService"/>.
    ///
    /// File format (zip):
    ///   manifest.json       — full project payload (see ProjectShareManifest)
    ///   images/             — extracted image bytes if the source was a
    ///                         data: URI; HTTP image_url stays a string
    ///                         reference inside the manifest.
    ///
    /// Schema version 1 is the initial format; bump on breaking changes
    /// (renames or removed fields). Importer rejects unknown schema versions.
    /// </summary>
    public class ProjectShareService
    {
        public const int SchemaVersion = 1;
        public const string FileExtension = ".mcsproject";

        private readonly Func<InventoryDbContext> _ctxFactory;

        public ProjectShareService(Func<InventoryDbContext> ctxFactory)
        {
            _ctxFactory = ctxFactory;
        }

        /// <summary>Default share folder under the user's Documents tree.
        /// Inno installer doesn't need to create it; we mkdir on demand.</summary>
        public static string ShareFolder
        {
            get
            {
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var dir = Path.Combine(docs, "My Crafty Stash", "Shared");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        /// <summary>Builds a safe, unique filename for the given project.</summary>
        public static string BuildFileName(Project project)
        {
            var safeName = string.Join("_",
                project.Name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
            if (string.IsNullOrWhiteSpace(safeName)) safeName = "project";
            return $"{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}{FileExtension}";
        }

        /// <summary>
        /// Exports the given project to a .mcsproject zip at the supplied
        /// path. Returns the full path written.
        /// </summary>
        public async Task<string> ExportAsync(int projectId, string? destinationPath = null,
            string? exportedByName = null, CancellationToken ct = default)
        {
            using var ctx = _ctxFactory();
            var project = await ctx.Projects
                .Include(p => p.ProjectItems).ThenInclude(pi => pi.Item)
                .Include(p => p.Images)
                .Include(p => p.Creations)
                .FirstOrDefaultAsync(p => p.Id == projectId, ct)
                ?? throw new InvalidOperationException($"Project {projectId} not found.");

            var cardBuilds = await ctx.ProjectCardBuilds
                .Where(b => b.ProjectId == projectId)
                .Include(b => b.Steps)
                .ToListAsync(ct);

            destinationPath ??= Path.Combine(ShareFolder, BuildFileName(project));

            var manifest = new ProjectShareManifest
            {
                SchemaVersion = SchemaVersion,
                ExportedAt    = DateTime.UtcNow,
                ExportedBy    = exportedByName ?? Environment.UserName,
                Project = new SharedProjectPayload
                {
                    Name        = project.Name,
                    Description = project.Description,
                    Technique   = project.Technique,
                    Notes       = project.Notes,
                    CreatedAt   = project.CreatedAt,
                    Items = project.ProjectItems.Select(pi => new SharedProjectItem
                    {
                        ItemName              = pi.Item?.Name ?? string.Empty,
                        ItemNumber            = pi.Item?.ItemNumber,
                        ItemType              = pi.Item?.Type,
                        ItemSubtype           = pi.Item?.Subtype,
                        ItemTheme             = pi.Item?.Theme,
                        ItemPurchasedFrom     = pi.Item?.PurchasedFrom,
                        ItemSiteUrl           = pi.Item?.SiteUrl,
                        SortOrder             = pi.SortOrder,
                        AmountUsedPerCreation = pi.AmountUsedPerCreation,
                    }).OrderBy(i => i.SortOrder).ToList(),
                    Creations = project.Creations.Select(c => new SharedProjectCreation
                    {
                        CreatedOn      = c.CreatedOn,
                        Notes          = c.Notes,
                        MaterialsUsed  = c.MaterialsUsed,
                    }).OrderBy(c => c.CreatedOn).ToList(),
                    CardBuilds = cardBuilds.Select(b => new SharedCardBuild
                    {
                        CardBaseType   = b.CardBaseType,
                        StateSnapshot  = b.StateSnapshot,
                        CreatedAt      = b.CreatedAt,
                        Steps = b.Steps.OrderBy(s => s.StepOrder).Select(s => new SharedCardBuildStep
                        {
                            StepOrder      = s.StepOrder,
                            Section        = s.Section,
                            StepType       = s.StepType,
                            MatLayer       = s.MatLayer,
                            CuttingMethod  = s.CuttingMethod,
                            Label          = s.Label,
                        }).ToList(),
                    }).ToList(),
                },
            };

            // Materialize the zip. Image URLs that are data: URIs become
            // images/{slug}.{ext} files inside the zip and are rewritten on
            // the manifest to reference the file path; HTTP URLs stay strings.
            using var zipStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                int imgCounter = 0;
                manifest.Project.MainImageUrl = ExtractImage(archive, project.ImageUrl, ref imgCounter, "main");
                manifest.Project.Images = project.Images
                    .OrderBy(i => i.SortOrder)
                    .Select(i => new SharedProjectImage
                    {
                        SortOrder = i.SortOrder,
                        ImageUrl  = ExtractImage(archive, i.ImageUrl, ref imgCounter, "extra"),
                    })
                    .Where(i => !string.IsNullOrEmpty(i.ImageUrl))
                    .ToList();

                var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                });
                var entry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
                using var w = new StreamWriter(entry.Open());
                w.Write(manifestJson);
            }

            LoggingService.LogInfo($"ProjectShareService: exported project {projectId} → {destinationPath}");
            return destinationPath;
        }

        /// <summary>Pulls a base64 data URI out of <paramref name="src"/> and writes
        /// it as a binary entry in the zip, returning the in-zip path. HTTP
        /// URLs are returned unchanged. Empty/null returns null.</summary>
        private static string? ExtractImage(ZipArchive archive, string? src, ref int counter, string slug)
        {
            if (string.IsNullOrWhiteSpace(src)) return null;
            if (!src.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                return src; // HTTP URL — keep as reference

            var commaIdx = src.IndexOf(',');
            if (commaIdx < 0) return null;
            var meta = src.Substring(5, commaIdx - 5); // e.g. "image/png;base64"
            var b64 = src[(commaIdx + 1)..];
            byte[] bytes;
            try { bytes = Convert.FromBase64String(b64); }
            catch { return null; }

            var ext = meta.Contains("jpeg", StringComparison.OrdinalIgnoreCase) ? "jpg"
                    : meta.Contains("webp", StringComparison.OrdinalIgnoreCase) ? "webp"
                    : meta.Contains("gif",  StringComparison.OrdinalIgnoreCase) ? "gif"
                    : "png";

            counter++;
            var inZip = $"images/{slug}_{counter:D3}.{ext}";
            var entry = archive.CreateEntry(inZip, CompressionLevel.Optimal);
            using var s = entry.Open();
            s.Write(bytes, 0, bytes.Length);
            return $"mcsproject:{inZip}";
        }
    }

    // ── Manifest schema (versioned at SchemaVersion=1) ──────────────────────

    public class ProjectShareManifest
    {
        public int SchemaVersion { get; set; }
        public DateTime ExportedAt { get; set; }
        public string ExportedBy { get; set; } = string.Empty;
        public SharedProjectPayload Project { get; set; } = new();
    }

    public class SharedProjectPayload
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Technique { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? MainImageUrl { get; set; }
        public List<SharedProjectImage> Images { get; set; } = new();
        public List<SharedProjectItem> Items { get; set; } = new();
        public List<SharedProjectCreation> Creations { get; set; } = new();
        public List<SharedCardBuild> CardBuilds { get; set; } = new();
    }

    public class SharedProjectImage
    {
        public int SortOrder { get; set; }
        public string? ImageUrl { get; set; }
    }

    public class SharedProjectItem
    {
        public string ItemName { get; set; } = string.Empty;
        public string? ItemNumber { get; set; }
        public string? ItemType { get; set; }
        public string? ItemSubtype { get; set; }
        public string? ItemTheme { get; set; }
        public string? ItemPurchasedFrom { get; set; }
        public string? ItemSiteUrl { get; set; }
        public int SortOrder { get; set; }
        public decimal? AmountUsedPerCreation { get; set; }
    }

    public class SharedProjectCreation
    {
        public DateTime CreatedOn { get; set; }
        public string? Notes { get; set; }
        public string? MaterialsUsed { get; set; }
    }

    public class SharedCardBuild
    {
        public string CardBaseType { get; set; } = string.Empty;
        public string? StateSnapshot { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<SharedCardBuildStep> Steps { get; set; } = new();
    }

    public class SharedCardBuildStep
    {
        public int StepOrder { get; set; }
        public string Section { get; set; } = string.Empty;
        public string StepType { get; set; } = string.Empty;
        public int? MatLayer { get; set; }
        public string? CuttingMethod { get; set; }
        public string Label { get; set; } = string.Empty;
    }
}
