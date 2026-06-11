using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace MyCraftyStash.Services
{
    /// <summary>
    /// One release's notes, shown in the "what's new" popup that fires once
    /// after an upgrade. Bullets come from the GitHub release body.
    /// </summary>
    public class ReleaseNotesEntry
    {
        public Version Version { get; set; } = new Version(0, 0, 0, 0);
        // Plain-text bullet lines for this version, leading "- " stripped.
        public List<string> Bullets { get; set; } = new();
    }

    /// <summary>Outcome of a GitHub update check.</summary>
    public class UpdateCheckResult
    {
        public bool UpdateAvailable { get; init; }
        public Version? LatestVersion { get; init; }
        /// <summary>browser_download_url of the installer asset.</summary>
        public string? AssetUrl { get; init; }
        public string? AssetName { get; init; }
        public long AssetSizeBytes { get; init; }
        public string? Error { get; init; }
    }

    /// <summary>
    /// GitHub-Releases-based updater. Replaces the old network-share flow
    /// (which pointed at J and H Inventory's install share and could offer
    /// the wrong app's installer): the app now checks the public repo's
    /// latest release, downloads the Inno Setup installer asset to %TEMP%,
    /// and launches it after the user says yes.
    ///
    /// Releases are created by Installer\publish.ps1 (gh release create) with
    /// tag vX.Y.Z.W and a MyCraftyStash_Setup_X.Y.Z.W.exe asset. Release-note
    /// bullets for the what's-new popup come from the release bodies, so the
    /// changelog only needs to be written once, in the release.
    /// </summary>
    public class UpdateService
    {
        private const string RepoOwner = "TequilaJosh";
        private const string RepoName = "MyCraftyStash";
        private const string ApiBase = $"https://api.github.com/repos/{RepoOwner}/{RepoName}";

        private static readonly Lazy<HttpClient> _http = new(() =>
        {
            var h = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            // GitHub's API rejects requests without a User-Agent.
            h.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("MyCraftyStash-Updater", GetCurrentVersion().ToString()));
            h.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            return h;
        });

        public static Version GetCurrentVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);
        }

        public static string GetInstallationPath() => $"https://github.com/{RepoOwner}/{RepoName}/releases";

        /// <summary>
        /// Asks GitHub for the latest release and compares its tag against the
        /// running assembly version. Never throws; failures land in Error.
        /// </summary>
        public static async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken ct = default)
        {
            try
            {
                using var resp = await _http.Value.GetAsync($"{ApiBase}/releases/latest", ct);

                if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return new UpdateCheckResult { Error = "No releases have been published yet." };
                if (resp.StatusCode == (System.Net.HttpStatusCode)403)
                    return new UpdateCheckResult { Error = "GitHub rate limit reached. Try again in a few minutes." };
                if (!resp.IsSuccessStatusCode)
                    return new UpdateCheckResult { Error = $"GitHub returned {(int)resp.StatusCode} {resp.ReasonPhrase}." };

                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
                var root = doc.RootElement;

                var latest = ResolveReleaseVersion(root);
                if (latest is null)
                    return new UpdateCheckResult { Error = $"Could not determine a version for release tagged '{root.GetProperty("tag_name").GetString()}'." };

                var current = GetCurrentVersion();
                if (latest <= current)
                    return new UpdateCheckResult { LatestVersion = latest };

                // Find the installer asset. publish.ps1 uploads exactly one
                // .exe (MyCraftyStash_Setup_X.Y.Z.W.exe); prefer a "setup"
                // match so a stray extra asset can't get picked by accident.
                string? assetUrl = null, assetName = null;
                long assetSize = 0;
                if (root.TryGetProperty("assets", out var assets))
                {
                    var exes = assets.EnumerateArray()
                        .Where(a => (a.GetProperty("name").GetString() ?? "")
                            .EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    var pick = exes.FirstOrDefault(a => (a.GetProperty("name").GetString() ?? "")
                            .Contains("setup", StringComparison.OrdinalIgnoreCase));
                    if (pick.ValueKind == JsonValueKind.Undefined && exes.Count > 0) pick = exes[0];
                    if (pick.ValueKind == JsonValueKind.Object)
                    {
                        assetName = pick.GetProperty("name").GetString();
                        assetUrl = pick.GetProperty("browser_download_url").GetString();
                        assetSize = pick.TryGetProperty("size", out var s) ? s.GetInt64() : 0;
                    }
                }

                if (assetUrl is null)
                    return new UpdateCheckResult
                    {
                        LatestVersion = latest,
                        Error = $"Release v{latest} exists but has no installer attached.",
                    };

                return new UpdateCheckResult
                {
                    UpdateAvailable = true,
                    LatestVersion = latest,
                    AssetUrl = assetUrl,
                    AssetName = assetName,
                    AssetSizeBytes = assetSize,
                };
            }
            catch (OperationCanceledException)
            {
                return new UpdateCheckResult { Error = "Update check was cancelled." };
            }
            catch (HttpRequestException ex)
            {
                return new UpdateCheckResult { Error = $"Could not reach GitHub: {ex.Message}" };
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "UpdateService.CheckForUpdatesAsync");
                return new UpdateCheckResult { Error = $"Error checking for updates: {ex.Message}" };
            }
        }

        /// <summary>Folder downloaded installers land in. Old downloads are
        /// cleaned opportunistically on the next download.</summary>
        private static string DownloadFolder =>
            Path.Combine(Path.GetTempPath(), "MyCraftyStash", "Updates");

        /// <summary>
        /// Streams the installer asset to %TEMP%. Progress reports
        /// (bytesSoFar, totalBytes); total is -1 when GitHub omits the length.
        /// Returns the downloaded file path. Throws on failure (callers show
        /// the message) and on cancellation.
        /// </summary>
        public static async Task<string> DownloadUpdateAsync(
            string assetUrl, string assetName,
            IProgress<(long bytes, long total)>? progress = null,
            CancellationToken ct = default)
        {
            Directory.CreateDirectory(DownloadFolder);

            // Drop leftovers from previous update attempts so the temp folder
            // doesn't accumulate 80 MB installers forever.
            foreach (var old in Directory.GetFiles(DownloadFolder, "*.exe"))
            {
                try { File.Delete(old); } catch { /* in use or locked: ignore */ }
            }

            var destPath = Path.Combine(DownloadFolder, SanitizeFileName(assetName));
            using var resp = await _http.Value.GetAsync(
                assetUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();

            var total = resp.Content.Headers.ContentLength ?? -1;
            await using (var src = await resp.Content.ReadAsStreamAsync(ct))
            await using (var dst = File.Create(destPath))
            {
                var buffer = new byte[81920];
                long done = 0;
                int read;
                while ((read = await src.ReadAsync(buffer, ct)) > 0)
                {
                    await dst.WriteAsync(buffer.AsMemory(0, read), ct);
                    done += read;
                    progress?.Report((done, total));
                }
            }

            LoggingService.LogInfo($"UpdateService: downloaded {assetName} to {destPath}");
            return destPath;
        }

        /// <summary>
        /// Pulls release bodies from GitHub and returns the notes for every
        /// version in (lastShown, current]. Returns an empty list (never
        /// throws) when offline or nothing matches.
        /// Bullet lines start with "- ", "* " or "•"; markdown headers and
        /// blank lines are skipped.
        /// </summary>
        public static async Task<List<ReleaseNotesEntry>> GetReleaseNotesSinceAsync(
            Version? lastShown, Version current, CancellationToken ct = default)
        {
            var entries = new List<ReleaseNotesEntry>();
            try
            {
                using var resp = await _http.Value.GetAsync($"{ApiBase}/releases?per_page=30", ct);
                if (!resp.IsSuccessStatusCode) return entries;

                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
                foreach (var rel in doc.RootElement.EnumerateArray())
                {
                    var v = ResolveReleaseVersion(rel);
                    if (v is null) continue;

                    var body = rel.TryGetProperty("body", out var b) ? b.GetString() : null;
                    if (string.IsNullOrWhiteSpace(body)) continue;

                    var entry = new ReleaseNotesEntry { Version = v };
                    foreach (var raw in body.Split('\n'))
                    {
                        var line = raw.Trim();
                        if (line.Length == 0 || line.StartsWith('#')) continue;
                        if (line.StartsWith("- ")) line = line[2..].TrimStart();
                        else if (line.StartsWith("* ")) line = line[2..].TrimStart();
                        else if (line.StartsWith('•')) line = line.TrimStart('•').TrimStart();
                        entry.Bullets.Add(line);
                    }
                    if (entry.Bullets.Count > 0) entries.Add(entry);
                }
            }
            catch
            {
                // Release notes are a nice-to-have; never let a network or
                // parse failure block startup. The dialog just won't appear.
                return new List<ReleaseNotesEntry>();
            }

            return entries
                .Where(e => e.Version <= current && (lastShown == null || e.Version > lastShown))
                .OrderByDescending(e => e.Version)
                .ToList();
        }

        /// <summary>
        /// Launches a previously downloaded installer. The path must be inside
        /// our own temp download folder: defense against a caller being talked
        /// into launching an arbitrary binary.
        /// </summary>
        public static (bool success, string? error) ApplyUpdate(string installerPath)
        {
            try
            {
                if (string.IsNullOrEmpty(installerPath) || !File.Exists(installerPath))
                    return (false, "Downloaded installer not found.");

                var resolved = Path.GetFullPath(installerPath);
                var baseDir = Path.GetFullPath(DownloadFolder)
                    .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!resolved.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
                    return (false, "Installer path resolved outside the update download folder.");

                Process.Start(new ProcessStartInfo
                {
                    FileName = resolved,
                    UseShellExecute = true,
                });
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, $"Error launching update: {ex.Message}");
            }
        }

        // ── helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Works out a release's version: the tag first (publish.ps1 tags
        /// vX.Y.Z.W), falling back to a version embedded in an asset filename
        /// (MyCraftyStash_Setup_1.0.2.10.exe). The fallback keeps hand-made
        /// releases working — the first release was tagged "latest" with no
        /// version anywhere except the asset name.
        /// </summary>
        private static Version? ResolveReleaseVersion(JsonElement release)
        {
            var fromTag = ParseTagVersion(release.GetProperty("tag_name").GetString());
            if (fromTag is not null) return fromTag;

            if (release.TryGetProperty("assets", out var assets))
            {
                foreach (var a in assets.EnumerateArray())
                {
                    var name = a.GetProperty("name").GetString() ?? "";
                    // Require >= 3 segments so e.g. "Setup_1.02.exe" can't be
                    // misread as Version 1.2.
                    var m = System.Text.RegularExpressions.Regex.Match(
                        name, @"(\d+\.\d+\.\d+(?:\.\d+)?)");
                    if (m.Success && Version.TryParse(m.Groups[1].Value, out var v))
                        return v.Revision < 0 ? new Version(v.Major, v.Minor, v.Build, 0) : v;
                }
            }
            return null;
        }

        /// <summary>"v1.0.3" / "1.0.3.2" → 4-segment Version, or null.</summary>
        private static Version? ParseTagVersion(string? tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return null;
            var text = tag.Trim().TrimStart('v', 'V');
            if (!Version.TryParse(text, out var v)) return null;
            // Pad to 4 segments so comparisons against the assembly version
            // (always 4 segments) line up: 1.0.3 == 1.0.3.0.
            if (v.Build < 0) return new Version(v.Major, v.Minor, 0, 0);
            if (v.Revision < 0) return new Version(v.Major, v.Minor, v.Build, 0);
            return v;
        }

        private static string SanitizeFileName(string? name)
        {
            var fallback = "MyCraftyStash_Setup.exe";
            if (string.IsNullOrWhiteSpace(name)) return fallback;
            var clean = new string(name.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray());
            return string.IsNullOrWhiteSpace(clean) ? fallback : clean;
        }
    }
}
