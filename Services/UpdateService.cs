using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace MyCraftyStash.Services
{
    /// <summary>
    /// One section of the changelog, parsed out of <c>changelog.txt</c> on the
    /// install share. Surfaced by <see cref="UpdateService.GetReleaseNotesSince"/>
    /// for the "what's new" popup that fires once after an update.
    /// </summary>
    public class ReleaseNotesEntry
    {
        public Version Version { get; set; } = new Version(0, 0, 0, 0);
        // Plain-text bullet lines for this version, leading "- " stripped.
        public List<string> Bullets { get; set; } = new();
    }

    public class UpdateService
    {
        private const string NetworkInstallPath = @"\\Win-u5iq2hisnh3\e\Installation";

        public static Version GetCurrentVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);
        }

        public static string GetInstallationPath() => NetworkInstallPath;

        public static (bool updateAvailable, Version? latestVersion, string? installerPath, string? error) CheckForUpdates()
        {
            try
            {
                if (!Directory.Exists(NetworkInstallPath))
                {
                    return (false, null, null, "Installation folder is not accessible. Make sure you are connected to the network.");
                }

                var versionFile = Path.Combine(NetworkInstallPath, "version.txt");
                if (!File.Exists(versionFile))
                {
                    return (false, null, null, "Version file not found at the installation location.");
                }

                var versionText = File.ReadAllText(versionFile).Trim();
                if (!Version.TryParse(versionText, out var latestVersion))
                {
                    return (false, null, null, $"Could not parse version from file: '{versionText}'");
                }

                var currentVersion = GetCurrentVersion();
                var updateAvailable = latestVersion > currentVersion;

                string? installerPath = null;
                if (updateAvailable)
                {
                    installerPath = FindInstaller();
                }

                return (updateAvailable, latestVersion, installerPath, null);
            }
            catch (UnauthorizedAccessException)
            {
                return (false, null, null, "Access denied to the installation folder. Check your network permissions.");
            }
            catch (IOException ex)
            {
                return (false, null, null, $"Could not reach the installation folder: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, null, null, $"Error checking for updates: {ex.Message}");
            }
        }

        private static string? FindInstaller()
        {
            var setupExe = Path.Combine(NetworkInstallPath, "setup.exe");
            if (File.Exists(setupExe)) return setupExe;

            var msiFiles = Directory.GetFiles(NetworkInstallPath, "*.msi");
            if (msiFiles.Length > 0)
                return msiFiles.OrderByDescending(f => new FileInfo(f).LastWriteTime).First();

            var exeFiles = Directory.GetFiles(NetworkInstallPath, "*.exe")
                .Where(f => Path.GetFileName(f).Contains("setup", StringComparison.OrdinalIgnoreCase) ||
                            Path.GetFileName(f).Contains("install", StringComparison.OrdinalIgnoreCase) ||
                            !Path.GetFileName(f).Equals("MyCraftyStash.exe", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (exeFiles.Length > 0)
                return exeFiles.OrderByDescending(f => new FileInfo(f).LastWriteTime).First();

            var appExe = Path.Combine(NetworkInstallPath, "MyCraftyStash.exe");
            if (File.Exists(appExe)) return appExe;

            return null;
        }

        /// <summary>
        /// Reads <c>changelog.txt</c> from the install share and returns every
        /// section whose header version is greater than <paramref name="lastShown"/>
        /// and less than or equal to <paramref name="current"/>. Returns an empty
        /// list (never throws) when the file is missing, the share is offline, or
        /// nothing matches — callers can just decide not to show the popup.
        ///
        /// Expected format (kept dead-simple so anyone can edit the file by hand):
        ///   ## 1.0.5.0
        ///   - First bullet
        ///   - Second bullet
        ///
        ///   ## 1.0.4.0
        ///   - …
        ///
        /// Header markers also accept "== X.Y.Z.W ==" or a bare version line.
        /// </summary>
        public static List<ReleaseNotesEntry> GetReleaseNotesSince(Version? lastShown, Version current)
        {
            var entries = new List<ReleaseNotesEntry>();
            try
            {
                var path = Path.Combine(NetworkInstallPath, "changelog.txt");
                if (!File.Exists(path)) return entries;

                var lines = File.ReadAllLines(path);
                ReleaseNotesEntry? open = null;
                var headerRx = new Regex(@"^\s*(?:#{1,6}\s*|=+\s*)?v?(\d+\.\d+\.\d+(?:\.\d+)?)\s*=*\s*$",
                                          RegexOptions.IgnoreCase);

                void Commit()
                {
                    if (open != null && open.Bullets.Count > 0) entries.Add(open);
                    open = null;
                }

                foreach (var raw in lines)
                {
                    var line = raw.TrimEnd();
                    var hm = headerRx.Match(line);
                    if (hm.Success && Version.TryParse(hm.Groups[1].Value, out var v))
                    {
                        Commit();
                        // 3-segment versions: pad to 4 so comparisons against
                        // assembly Version (always 4 segments) line up.
                        if (v.Build < 0)      v = new Version(v.Major, v.Minor, 0, 0);
                        else if (v.Revision < 0) v = new Version(v.Major, v.Minor, v.Build, 0);
                        open = new ReleaseNotesEntry { Version = v };
                        continue;
                    }
                    if (open == null) continue;
                    var trimmed = line.TrimStart();
                    if (trimmed.Length == 0) continue;
                    if (trimmed.StartsWith("- ")) trimmed = trimmed.Substring(2).TrimStart();
                    else if (trimmed.StartsWith("* ")) trimmed = trimmed.Substring(2).TrimStart();
                    else if (trimmed.StartsWith("•")) trimmed = trimmed.TrimStart('•').TrimStart();
                    open.Bullets.Add(trimmed);
                }
                Commit();
            }
            catch
            {
                // Release-notes are a "nice to have" — never let a parse / IO
                // failure block startup. The dialog just won't appear.
                return new List<ReleaseNotesEntry>();
            }

            return entries
                .Where(e => e.Version <= current && (lastShown == null || e.Version > lastShown))
                .OrderByDescending(e => e.Version)
                .ToList();
        }

        public static (bool success, string? error) ApplyUpdate(string? installerPath = null)
        {
            try
            {
                installerPath ??= FindInstaller();

                if (string.IsNullOrEmpty(installerPath) || !File.Exists(installerPath))
                {
                    return (false, "No installer found at the installation location.");
                }

                // Defense-in-depth: confirm the resolved installer is inside the configured network share.
                // Prevents a symlink/junction inside the share from causing us to launch a binary elsewhere.
                var resolvedInstaller = Path.GetFullPath(installerPath);
                var resolvedBase = Path.GetFullPath(NetworkInstallPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!resolvedInstaller.StartsWith(resolvedBase, StringComparison.OrdinalIgnoreCase))
                {
                    return (false, "Installer path resolved outside the trusted installation folder.");
                }

                var extension = Path.GetExtension(installerPath).ToLowerInvariant();
                var startInfo = new ProcessStartInfo { UseShellExecute = true };

                if (extension == ".msi")
                {
                    startInfo.FileName = "msiexec";
                    startInfo.Arguments = $"/i \"{installerPath}\"";
                }
                else
                {
                    startInfo.FileName = installerPath;
                }

                Process.Start(startInfo);
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, $"Error launching update: {ex.Message}");
            }
        }
    }
}
