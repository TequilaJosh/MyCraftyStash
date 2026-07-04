using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MyCraftyStash.Models;
using MyCraftyStash.Services;
using MyCraftyStash.ViewModels;
using MyCraftyStash.Views;

namespace MyCraftyStash
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Bound Magick.NET's decoder so a malformed or maliciously huge image
            // (e.g. a scraped catalog picture or an imported .mcsproject) can't
            // exhaust memory while decoding. Global, set once.
            try
            {
                ImageMagick.ResourceLimits.Width = 30000;
                ImageMagick.ResourceLimits.Height = 30000;
                ImageMagick.ResourceLimits.Memory = 512UL * 1024 * 1024; // 512 MB
            }
            catch { /* older Magick.NET without one of these knobs — non-fatal */ }

            EventManager.RegisterClassHandler(typeof(ComboBox), UIElement.PreviewMouseWheelEvent,
                new MouseWheelEventHandler((s, args) =>
                {
                    if (s is ComboBox cb && !cb.IsDropDownOpen)
                        args.Handled = true;
                }));

            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            LoggingService.LogInfo("Application started");
            UserSettingsService.Load();

            // Probe the database asynchronously. If the server is offline we
            // still let the app open — DB-dependent views can show empty
            // states and the user gets a single warning toast. Fire-and-forget
            // is fine: DatabaseHealthService caches the result.
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () =>
            {
                try
                {
                    var ok = await DatabaseHealthService.CheckAsync();
                    if (!ok)
                    {
                        MessageBox.Show(
                            "The application could not connect to the database.\n\n" +
                            "You can continue using the app, but inventory, projects, and other data " +
                            "will not load until the connection is restored.\n\n" +
                            $"Details: {DatabaseHealthService.LastError}",
                            "Database offline",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.LogError(ex, "App.OnStartup - database health probe");
                }
            }));

            // Check GitHub Releases for a newer build, fire-and-forget so a
            // slow / offline connection doesn't block startup. Honours the
            // user's CheckForUpdatesOnStartup preference (Settings → Updates).
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                try
                {
                    var settings = UpdateSettings.Load();
                    if (!settings.CheckForUpdatesOnStartup) return;

                    Task.Run(async () =>
                    {
                        var check = await UpdateService.CheckForUpdatesAsync();
                        if (!check.UpdateAvailable || check.AssetUrl == null) return;

                        // Marshal back to UI thread for the prompt.
                        Dispatcher.Invoke(() =>
                        {
                            var current = UpdateService.GetCurrentVersion();
                            var sizeMb = check.AssetSizeBytes > 0
                                ? $" (~{check.AssetSizeBytes / 1048576.0:0} MB)"
                                : "";
                            var result = MessageBox.Show(
                                $"A new version of My Crafty Stash is available.\n\n" +
                                $"   Installed:  {current}\n" +
                                $"   Available:  {check.LatestVersion}\n\n" +
                                $"Download and install it now{sizeMb}? " +
                                "The app will close when the installer starts.",
                                "Update available",
                                MessageBoxButton.YesNo, MessageBoxImage.Information,
                                MessageBoxResult.Yes);

                            if (result != MessageBoxResult.Yes) return;

                            var dlg = new Views.UpdateDownloadDialog(check.AssetUrl, check.AssetName ?? "MyCraftyStash_Setup.exe")
                            {
                                Owner = Current.MainWindow,
                            };
                            if (dlg.ShowDialog() != true || dlg.InstallerPath == null)
                            {
                                if (!string.IsNullOrEmpty(dlg.ErrorMessage) && dlg.ErrorMessage != "Download cancelled.")
                                {
                                    MessageBox.Show(
                                        $"Could not download the update:\n\n{dlg.ErrorMessage}",
                                        "Update failed",
                                        MessageBoxButton.OK, MessageBoxImage.Warning);
                                }
                                return;
                            }

                            var (success, applyError) = UpdateService.ApplyUpdate(dlg.InstallerPath);
                            if (!success)
                            {
                                MessageBox.Show(
                                    $"Could not launch the installer:\n\n{applyError}",
                                    "Update failed",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                                return;
                            }

                            // Installer is launched; close so it can replace files.
                            Shutdown();
                        });
                    });
                }
                catch (Exception ex)
                {
                    LoggingService.LogError(ex, "App.OnStartup - update check");
                }
            }));

            // "What's new" popup — fires once per upgrade. We only show it when
            // UpdateSettings.LastNotesShownVersion is set AND the running build
            // is newer than that recorded version. On a brand-new install (or
            // the very first launch after this feature was added) we just stamp
            // the current version and skip the dialog so users don't get spammed
            // with the entire history.
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                try
                {
                    var settings = UpdateSettings.Load();
                    var current = UpdateService.GetCurrentVersion();
                    var hasPrior = !string.IsNullOrWhiteSpace(settings.LastNotesShownVersion)
                                   && Version.TryParse(settings.LastNotesShownVersion, out var prior);
                    Version? lastShown = hasPrior && Version.TryParse(settings.LastNotesShownVersion, out var p)
                        ? p : null;

                    if (lastShown == null || current <= lastShown)
                    {
                        // First run (or downgrade / re-run) — just record where we
                        // are so the next real upgrade is what triggers the popup.
                        if (settings.LastNotesShownVersion != current.ToString())
                        {
                            settings.LastNotesShownVersion = current.ToString();
                            settings.Save();
                        }
                        return;
                    }

                    // Genuine upgrade — pull notes from the GitHub release bodies
                    // and (only if any entries actually exist for the upgraded
                    // range) show the dialog.
                    Task.Run(async () =>
                    {
                        var entries = await UpdateService.GetReleaseNotesSinceAsync(lastShown, current);
                        if (entries.Count == 0)
                        {
                            // No notes for this range — still record the version
                            // so we don't keep checking on every launch.
                            settings.LastNotesShownVersion = current.ToString();
                            settings.Save();
                            return;
                        }

                        Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                var dlg = new ReleaseNotesDialog(entries, current)
                                {
                                    Owner = MainWindow
                                };
                                dlg.ShowDialog();
                            }
                            finally
                            {
                                settings.LastNotesShownVersion = current.ToString();
                                settings.Save();
                            }
                        });
                    });
                }
                catch (Exception ex)
                {
                    LoggingService.LogError(ex, "App.OnStartup - release notes popup");
                }
            }));

            // Check for calendar event reminders after the main window is idle.
            // Skipped silently if the database is offline — the warning toast
            // above already explains why nothing loaded.
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () =>
            {
                try
                {
                    if (!DatabaseHealthService.IsAvailable) return;
                    var calService = new CalendarService();
                    var reminders = await calService.GetUpcomingRemindersAsync();
                    if (reminders.Count > 0)
                    {
                        var vm = new EventReminderViewModel(reminders);
                        var dialog = new EventReminderDialog(vm);
                        dialog.ShowDialog();
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.LogError(ex, "App.OnStartup - calendar reminder check");
                }
            }));
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LoggingService.LogError(e.Exception, "App - DispatcherUnhandledException");
            BugReportService.ShowErrorWithBugReportOption(
                "An unexpected error occurred in My Crafty Stash.",
                e.Exception,
                "DispatcherUnhandledException");
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
                LoggingService.LogError(ex, "App - CurrentDomain_UnhandledException");
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            LoggingService.LogError(e.Exception, "App - TaskScheduler_UnobservedTaskException");
            e.SetObserved();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            LoggingService.LogInfo("Application closed");
            base.OnExit(e);
        }
    }
}
