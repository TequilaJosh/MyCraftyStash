using System.Diagnostics;
using System.IO;
using System.Windows;

namespace MyCraftyStash.Services
{
    /// <summary>
    /// Shows an error popup that offers to send a bug report. On Yes, opens the
    /// user's default mail client with a pre-filled message to
    /// Silosoftwarecreations@gmail.com and opens Explorer to the log folder with
    /// today's ERROR log selected so the user can attach it.
    ///
    /// Why Explorer + mailto instead of a real attachment: the mailto: URL scheme
    /// does NOT support attachments. Gmail, Outlook, Thunderbird, and Windows Mail
    /// all silently ignore any "attachment" parameter. The only way to attach a
    /// file across arbitrary mail clients is MAPI, which isn't reliably available
    /// on modern Windows. The user-drags-it-in workaround is the most compatible.
    /// </summary>
    public static class BugReportService
    {
        public const string SupportEmail = "Silosoftwarecreations@gmail.com";

        /// <summary>
        /// Show the error dialog with an optional "Send bug report?" Yes/No prompt.
        /// </summary>
        /// <param name="userMessage">Friendly summary shown to the user (one or two sentences).</param>
        /// <param name="ex">The exception (used to enrich the bug report body). May be null.</param>
        /// <param name="context">Optional short context string (e.g. "Saving item"). Used in the email subject/body.</param>
        public static void ShowErrorWithBugReportOption(string userMessage, Exception? ex, string? context = null)
        {
            var dialogBody =
                $"{userMessage}\n\n" +
                (ex != null ? $"Details: {ex.Message}\n\n" : string.Empty) +
                "Would you like to send a bug report? This will open your default email app " +
                "with the error details pre-filled, and open the log folder so you can attach " +
                "today's log file to the email.";

            var result = MessageBox.Show(
                dialogBody,
                "Something went wrong",
                MessageBoxButton.YesNo,
                MessageBoxImage.Error,
                MessageBoxResult.Yes);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                LaunchBugReport(ex, context);
            }
            catch (Exception launchEx)
            {
                LoggingService.LogError(launchEx, "BugReportService.LaunchBugReport");
                MessageBox.Show(
                    "Could not open your email app automatically.\n\n" +
                    $"Please email {SupportEmail} and attach today's log file from:\n" +
                    LoggingService.GetLogFolderPath(),
                    "Bug report",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private static void LaunchBugReport(Exception? ex, string? context)
        {
            var now = DateTime.Now;
            var dateStr = now.ToString("yyyy-MM-dd");
            var subject = string.IsNullOrWhiteSpace(context)
                ? $"Bug Report - {dateStr}"
                : $"Bug Report - {dateStr} - {context}";

            string version;
            try { version = UpdateService.GetCurrentVersion().ToString(); }
            catch { version = "(unknown)"; }

            var logFolder = LoggingService.GetLogFolderPath();
            var errorLogPath = FindTodayErrorLog(logFolder);

            var body =
                "Hello Silo Software Creations team,\n\n" +
                "I encountered an error while using My Crafty Stash.\n\n" +
                "----- What I was doing when it happened -----\n" +
                "(please describe in your own words)\n\n" +
                "----- App info -----\n" +
                $"Date/time: {now:yyyy-MM-dd HH:mm:ss}\n" +
                $"App version: {version}\n" +
                $"Windows user: {Environment.UserName}\n" +
                $"Machine: {Environment.MachineName}\n" +
                (string.IsNullOrWhiteSpace(context) ? string.Empty : $"Context: {context}\n") +
                "\n----- Error details -----\n" +
                (ex != null
                    ? $"{ex.GetType().FullName}: {ex.Message}\n" +
                      (ex.InnerException != null ? $"Inner: {ex.InnerException.Message}\n" : string.Empty) +
                      "\n(Stack trace is in the attached log file.)\n"
                    : "(no exception captured)\n") +
                "\n----- Attaching the log -----\n" +
                "Today's log file has been opened in File Explorer for you. " +
                "Please drag it into this email (or use your mail app's Attach button) " +
                "before sending. The relevant file is:\n" +
                (errorLogPath ?? $"(latest .log file in {logFolder})") +
                "\n\nThank you!\n";

            var mailto = $"mailto:{SupportEmail}" +
                         $"?subject={Uri.EscapeDataString(subject)}" +
                         $"&body={Uri.EscapeDataString(body)}";

            // Open the default email client.
            Process.Start(new ProcessStartInfo
            {
                FileName = mailto,
                UseShellExecute = true
            });

            // Open Explorer with the log file selected so the user can drag/attach it.
            // Fall back to opening the folder if the specific file can't be located.
            try
            {
                if (!string.IsNullOrEmpty(errorLogPath) && File.Exists(errorLogPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{errorLogPath}\"",
                        UseShellExecute = true
                    });
                }
                else if (Directory.Exists(logFolder))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = logFolder,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception explorerEx)
            {
                LoggingService.LogError(explorerEx, "BugReportService - open Explorer");
            }
        }

        /// <summary>
        /// Returns the path to today's ERROR_*.log file in the given folder, or
        /// falls back to the most recently modified .log file in that folder,
        /// or null if none exist.
        /// </summary>
        private static string? FindTodayErrorLog(string logFolder)
        {
            try
            {
                if (!Directory.Exists(logFolder)) return null;

                var todayErrorName = $"ERROR_{DateTime.Now:yyyy-MM-dd}.log";
                var direct = Path.Combine(logFolder, todayErrorName);
                if (File.Exists(direct)) return direct;

                var newest = new DirectoryInfo(logFolder)
                    .EnumerateFiles("*.log")
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .FirstOrDefault();
                return newest?.FullName;
            }
            catch
            {
                return null;
            }
        }
    }
}
