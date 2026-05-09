using System.IO;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyCraftyStash.Data;
using MyCraftyStash.Models;

namespace MyCraftyStash.Services
{
    /// <summary>
    /// Reads a .mcsproject zip exported by another user (or by the same user
    /// for backup) and reconstructs the project locally. The new Project row
    /// is flagged IsShared=true with SharedFromName populated from the
    /// manifest, so the Projects page can badge it as "Shared".
    ///
    /// Item references are resolved by best-effort match on item_number then
    /// name. Items that don't exist locally are skipped from the
    /// project_items list — the receiver still sees the project's intent
    /// (notes, images, build steps) but isn't forced to create stand-in items
    /// they don't own.
    /// </summary>
    public class ProjectImportService
    {
        private readonly Func<InventoryDbContext> _ctxFactory;

        public ProjectImportService(Func<InventoryDbContext> ctxFactory)
        {
            _ctxFactory = ctxFactory;
        }

        public class ImportResult
        {
            public int ProjectId { get; set; }
            public string ProjectName { get; set; } = string.Empty;
            public int ItemsLinked { get; set; }
            public int ItemsSkipped { get; set; }
            public int ImagesImported { get; set; }
            public int CardBuildsImported { get; set; }
        }

        public async Task<ImportResult> ImportAsync(string mcsprojectPath, CancellationToken ct = default)
        {
            if (!File.Exists(mcsprojectPath))
                throw new FileNotFoundException("Shared project file not found", mcsprojectPath);

            using var zipStream = File.OpenRead(mcsprojectPath);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

            var manifestEntry = archive.GetEntry("manifest.json")
                ?? throw new InvalidDataException("manifest.json missing — not a valid .mcsproject file.");

            ProjectShareManifest manifest;
            using (var s = new StreamReader(manifestEntry.Open()))
            {
                var json = await s.ReadToEndAsync(ct);
                manifest = JsonSerializer.Deserialize<ProjectShareManifest>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? throw new InvalidDataException("manifest.json is empty or unreadable.");
            }

            if (manifest.SchemaVersion != ProjectShareService.SchemaVersion)
                throw new InvalidDataException(
                    $"Unsupported .mcsproject schema version {manifest.SchemaVersion} " +
                    $"(this build expects {ProjectShareService.SchemaVersion}).");

            // Inline any in-zip image references back to data: URIs so the
            // existing inventory / project image rendering path works as-is
            // without needing a separate extraction folder on disk.
            string? Inline(string? url)
            {
                if (string.IsNullOrEmpty(url) || !url.StartsWith("mcsproject:")) return url;
                var inZipPath = url.Substring("mcsproject:".Length);
                var imgEntry = archive.GetEntry(inZipPath);
                if (imgEntry == null) return null;
                using var ms = new MemoryStream();
                using (var es = imgEntry.Open()) es.CopyTo(ms);
                var bytes = ms.ToArray();
                var ext = Path.GetExtension(inZipPath).TrimStart('.').ToLowerInvariant();
                var mime = ext switch
                {
                    "jpg" or "jpeg" => "image/jpeg",
                    "webp" => "image/webp",
                    "gif" => "image/gif",
                    _ => "image/png",
                };
                return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
            }

            using var ctx = _ctxFactory();

            var newProject = new Project
            {
                Name           = manifest.Project.Name,
                Description    = manifest.Project.Description,
                Technique      = manifest.Project.Technique,
                Notes          = manifest.Project.Notes,
                ImageUrl       = Inline(manifest.Project.MainImageUrl),
                CreatedAt      = DateTime.Now,
                IsShared       = true,
                SharedFromName = manifest.ExportedBy,
                SharedAt       = DateTime.Now,
            };
            ctx.Projects.Add(newProject);
            await ctx.SaveChangesAsync(ct);

            var result = new ImportResult { ProjectId = newProject.Id, ProjectName = newProject.Name };

            // Images
            foreach (var img in manifest.Project.Images)
            {
                var inlined = Inline(img.ImageUrl);
                if (string.IsNullOrEmpty(inlined)) continue;
                ctx.ProjectImages.Add(new ProjectImage
                {
                    ProjectId = newProject.Id,
                    ImageUrl  = inlined,
                    SortOrder = img.SortOrder,
                });
                result.ImagesImported++;
            }
            await ctx.SaveChangesAsync(ct);

            // Items — best-effort match by item_number then by name.
            foreach (var sharedItem in manifest.Project.Items)
            {
                Item? localItem = null;
                if (!string.IsNullOrWhiteSpace(sharedItem.ItemNumber))
                {
                    localItem = ctx.Items.FirstOrDefault(i => i.ItemNumber == sharedItem.ItemNumber);
                }
                if (localItem == null && !string.IsNullOrWhiteSpace(sharedItem.ItemName))
                {
                    localItem = ctx.Items.FirstOrDefault(i => i.Name == sharedItem.ItemName);
                }
                if (localItem == null)
                {
                    result.ItemsSkipped++;
                    continue;
                }
                ctx.ProjectItems.Add(new ProjectItem
                {
                    ProjectId             = newProject.Id,
                    ItemId                = localItem.Id,
                    SortOrder             = sharedItem.SortOrder,
                    AmountUsedPerCreation = sharedItem.AmountUsedPerCreation,
                });
                result.ItemsLinked++;
            }

            // Creations
            foreach (var c in manifest.Project.Creations)
            {
                ctx.ProjectCreations.Add(new ProjectCreation
                {
                    ProjectId      = newProject.Id,
                    CreatedOn      = c.CreatedOn,
                    Notes          = c.Notes,
                    MaterialsUsed  = c.MaterialsUsed,
                });
            }

            // Card builds + steps. Item / die references in build steps are
            // intentionally not back-resolved on import — too brittle. The
            // user sees the build outline (mat layers, cutting methods,
            // labels) and can replicate locally.
            foreach (var b in manifest.Project.CardBuilds)
            {
                var build = new ProjectCardBuild
                {
                    ProjectId     = newProject.Id,
                    CardBaseType  = b.CardBaseType,
                    StateSnapshot = b.StateSnapshot,
                    CreatedAt     = b.CreatedAt,
                };
                ctx.ProjectCardBuilds.Add(build);
                await ctx.SaveChangesAsync(ct);

                foreach (var s in b.Steps)
                {
                    ctx.ProjectCardBuildSteps.Add(new ProjectCardBuildStep
                    {
                        BuildId        = build.Id,
                        StepOrder      = s.StepOrder,
                        Section        = s.Section,
                        StepType       = s.StepType,
                        MatLayer       = s.MatLayer,
                        CuttingMethod  = s.CuttingMethod,
                        Label          = s.Label,
                    });
                }
                result.CardBuildsImported++;
            }

            await ctx.SaveChangesAsync(ct);
            LoggingService.LogInfo($"ProjectImportService: imported \"{newProject.Name}\" from {mcsprojectPath} " +
                                   $"(items linked={result.ItemsLinked} skipped={result.ItemsSkipped})");
            return result;
        }
    }
}
