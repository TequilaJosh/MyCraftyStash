using System.IO;
using System.Runtime.CompilerServices;

namespace MyCraftyStash.Services
{
    /// <summary>
    /// Appends to one file per day per log level under
    /// &lt;install&gt;\Logs\YYYY-MM-DD\LEVEL_YYYY-MM-DD.log. Auto-prunes date
    /// folders older than RetentionDays on first use per process.
    /// </summary>
    public static class LoggingService
    {
        private const int RetentionDays = 30;

        private static readonly object LockObject = new();
        private static bool _retentionSwept;

        private static string GetLogFolder()
        {
            var folder = Path.Combine(AppPaths.LogsRoot, DateTime.Now.ToString("yyyy-MM-dd"));
            Directory.CreateDirectory(folder);

            if (!_retentionSwept)
            {
                _retentionSwept = true;
                SweepOldLogs();
            }

            return folder;
        }

        private static void SweepOldLogs()
        {
            try
            {
                if (!Directory.Exists(AppPaths.LogsRoot)) return;
                var cutoff = DateTime.Now.Date.AddDays(-RetentionDays);
                foreach (var dir in Directory.EnumerateDirectories(AppPaths.LogsRoot))
                {
                    var name = Path.GetFileName(dir);
                    if (DateTime.TryParseExact(name, "yyyy-MM-dd",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None,
                            out var folderDate)
                        && folderDate < cutoff)
                    {
                        try { Directory.Delete(dir, recursive: true); } catch { }
                    }
                }
            }
            catch
            {
                // Logging cleanup must never crash the app.
            }
        }

        private static void AppendToLog(string level, string message,
            string callerName, string callerFile)
        {
            try
            {
                var folder = GetLogFolder();
                var fileName = $"{level}_{DateTime.Now:yyyy-MM-dd}.log";
                var filePath = Path.Combine(folder, fileName);

                var line = $"[{DateTime.Now:HH:mm:ss.fff}] [{Path.GetFileNameWithoutExtension(callerFile)}.{callerName}] {message}{Environment.NewLine}";

                lock (LockObject)
                {
                    File.AppendAllText(filePath, line);
                }
            }
            catch (Exception ex)
            {
                try { System.Diagnostics.Debug.WriteLine($"LOGGING FAILURE: {ex.Message}"); } catch { }
            }
        }

        public static void LogInfo(string message,
            [CallerMemberName] string callerName = "",
            [CallerFilePath] string callerFile = "")
            => AppendToLog("INFO", message, callerName, callerFile);

        public static void LogWarning(string message,
            [CallerMemberName] string callerName = "",
            [CallerFilePath] string callerFile = "")
            => AppendToLog("WARNING", message, callerName, callerFile);

        // Note: callerName accepts an explicit context string from call sites
        // that pass one (e.g. LogError(ex, "loading color order")). The
        // [CallerMemberName] default kicks in only when nothing is passed.
        public static void LogError(Exception ex,
            [CallerMemberName] string callerName = "",
            [CallerFilePath] string callerFile = "")
        {
            var message = $"{ex.GetType().Name}: {ex.Message}" +
                          $"{Environment.NewLine}Stack: {ex.StackTrace}" +
                          (ex.InnerException != null
                              ? $"{Environment.NewLine}Inner: {ex.InnerException.Message}"
                              : string.Empty);
            AppendToLog("ERROR", message, callerName, callerFile);
        }

        public static void LogDatabaseError(Exception ex, string operation,
            [CallerMemberName] string callerName = "",
            [CallerFilePath] string callerFile = "")
        {
            var message = $"[DB:{operation}] {ex.GetType().Name}: {ex.Message}" +
                          $"{Environment.NewLine}Stack: {ex.StackTrace}" +
                          (ex.InnerException != null
                              ? $"{Environment.NewLine}Inner: {ex.InnerException.Message}"
                              : string.Empty);
            AppendToLog("DB_ERROR", message, callerName, callerFile);
        }

        public static string GetLogFolderPath() => GetLogFolder();
    }
}
