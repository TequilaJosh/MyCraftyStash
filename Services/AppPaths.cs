using System.IO;

namespace MyCraftyStash.Services
{
    /// <summary>
    /// Single source of truth for where the app reads and writes data.
    /// Per-user install: everything (binaries, both SQLite files, logs) lives in
    /// the install folder under %LOCALAPPDATA%\Programs\My Crafty Stash, which is
    /// fully writable without elevation.
    ///
    /// If the install folder is not writable (running from a dev bin\Debug,
    /// USB stick, etc.) we fall back to %LOCALAPPDATA%\My Crafty Stash so the
    /// app still has somewhere to put its database and logs.
    /// </summary>
    public static class AppPaths
    {
        private const string AppFolderName = "My Crafty Stash";

        private static readonly Lazy<string> _dataRoot = new(ResolveDataRoot);

        /// <summary>Folder that holds inventory.db, settings.db, and Logs\.</summary>
        public static string DataRoot => _dataRoot.Value;

        public static string InventoryDbPath => Path.Combine(DataRoot, "inventory.db");
        public static string SettingsDbPath  => Path.Combine(DataRoot, "settings.db");
        public static string LogsRoot        => Path.Combine(DataRoot, "Logs");

        public static string InventoryConnectionString => $"Data Source={InventoryDbPath}";
        public static string SettingsConnectionString  => $"Data Source={SettingsDbPath}";

        private static string ResolveDataRoot()
        {
            var installDir = AppContext.BaseDirectory;
            if (IsWritable(installDir))
                return installDir;

            var fallback = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppFolderName);
            Directory.CreateDirectory(fallback);
            return fallback;
        }

        private static bool IsWritable(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                var probe = Path.Combine(dir, $".write_probe_{Guid.NewGuid():N}");
                File.WriteAllText(probe, string.Empty);
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
