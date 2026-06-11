using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using MyCraftyStash.Services;

namespace MyCraftyStash.Views
{
    /// <summary>
    /// Themed About dialog. Shows the installed version + a Check for updates
    /// button (moved here from the Settings → Version tab) plus the Silo
    /// Software Creations contact info.
    /// </summary>
    public partial class AboutDialog : Window
    {
        public AboutDialog()
        {
            InitializeComponent();
            LoadVersionInfo();

            // Drag-anywhere by holding the left mouse button. Window has no
            // chrome because of WindowStyle=None / AllowsTransparency=true.
            // DragMove throws if the button is no longer down by the time we call it
            // (mouse release races the event) — swallow that specific case silently.
            MouseLeftButtonDown += (_, _) => { try { DragMove(); } catch (InvalidOperationException) { } };
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        // mailto: links require an explicit Process.Start with UseShellExecute
        // in modern .NET — Hyperlink.RequestNavigate doesn't auto-launch them.
        private void ContactEmailLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName        = e.Uri.ToString(),
                    UseShellExecute = true,
                });
                e.Handled = true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "AboutDialog.ContactEmailLink_RequestNavigate");
            }
        }

        private async void CheckForUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            CheckForUpdatesButton.IsEnabled = false;
            UpdateStatusText.Text = "Checking GitHub for updates...";

            var check = await UpdateService.CheckForUpdatesAsync();

            if (check.Error != null)
            {
                UpdateStatusText.Text = check.Error;
                CheckForUpdatesButton.IsEnabled = true;
                return;
            }

            var current = UpdateService.GetCurrentVersion();

            if (!check.UpdateAvailable || check.AssetUrl == null)
            {
                UpdateStatusText.Text = $"You're up to date (version {current}).";
                CheckForUpdatesButton.IsEnabled = true;
                return;
            }

            UpdateStatusText.Text = $"Update available: {check.LatestVersion}.";

            var sizeMb = check.AssetSizeBytes > 0
                ? $" (~{check.AssetSizeBytes / 1048576.0:0} MB)"
                : "";
            var prompt = MessageBox.Show(
                this,
                $"A new version of My Crafty Stash is available.\n\n" +
                $"   Installed:  {current}\n" +
                $"   Available:  {check.LatestVersion}\n\n" +
                $"Download and install it now{sizeMb}? " +
                "The app will close when the installer starts.",
                "Update available",
                MessageBoxButton.YesNo, MessageBoxImage.Information,
                MessageBoxResult.Yes);

            if (prompt != MessageBoxResult.Yes)
            {
                CheckForUpdatesButton.IsEnabled = true;
                return;
            }

            var dlg = new UpdateDownloadDialog(check.AssetUrl, check.AssetName ?? "MyCraftyStash_Setup.exe")
            {
                Owner = this,
            };
            if (dlg.ShowDialog() != true || dlg.InstallerPath == null)
            {
                UpdateStatusText.Text = string.IsNullOrEmpty(dlg.ErrorMessage)
                    ? "Download did not complete."
                    : dlg.ErrorMessage;
                CheckForUpdatesButton.IsEnabled = true;
                return;
            }

            var (success, applyError) = UpdateService.ApplyUpdate(dlg.InstallerPath);
            if (!success)
            {
                UpdateStatusText.Text = $"Could not launch the installer: {applyError}";
                CheckForUpdatesButton.IsEnabled = true;
                return;
            }

            // Installer is launched on the user machine; shut down so it can
            // replace the running exe.
            Application.Current.Shutdown();
        }

        private void LoadVersionInfo()
        {
            try
            {
                var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                var assemblyVersion = assembly.GetName().Version;
                AssemblyVersionText.Text = assemblyVersion?.ToString() ?? "Unknown";

                bool isClickOnce = false;
                string clickOnceVersion = string.Empty;

                try
                {
                    var deploymentType = Type.GetType("System.Deployment.Application.ApplicationDeployment, System.Deployment");
                    if (deploymentType != null)
                    {
                        var isNetworkDeployedProp = deploymentType.GetProperty("IsNetworkDeployed",
                            BindingFlags.Public | BindingFlags.Static);
                        if (isNetworkDeployedProp?.GetValue(null) is true)
                        {
                            isClickOnce = true;
                            var currentDeploymentProp = deploymentType.GetProperty("CurrentDeployment",
                                BindingFlags.Public | BindingFlags.Static);
                            var currentDeployment = currentDeploymentProp?.GetValue(null);
                            if (currentDeployment != null)
                            {
                                var currentVersionProp = currentDeployment.GetType()
                                    .GetProperty("CurrentVersion", BindingFlags.Public | BindingFlags.Instance);
                                if (currentVersionProp?.GetValue(currentDeployment) is Version v)
                                    clickOnceVersion = v.ToString();
                            }
                        }
                    }
                }
                catch { /* ClickOnce API not available */ }

                if (isClickOnce && !string.IsNullOrEmpty(clickOnceVersion))
                {
                    InstalledVersionText.Text = clickOnceVersion;
                    DeploymentTypeText.Text   = "ClickOnce (Network Deployed)";
                }
                else
                {
                    var informationalVersion = assembly
                        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                        ?.InformationalVersion;

                    if (!string.IsNullOrEmpty(informationalVersion))
                    {
                        var plusIndex = informationalVersion.IndexOf('+');
                        InstalledVersionText.Text = plusIndex >= 0
                            ? informationalVersion[..plusIndex]
                            : informationalVersion;
                    }
                    else
                    {
                        InstalledVersionText.Text = assemblyVersion?.ToString(3) ?? "Unknown";
                    }

                    DeploymentTypeText.Text = "Local / Direct Install";
                }
            }
            catch (Exception ex)
            {
                InstalledVersionText.Text = "Unable to determine";
                DeploymentTypeText.Text   = "Unknown";
                AssemblyVersionText.Text  = "Unknown";
                LoggingService.LogError(ex, "AboutDialog.LoadVersionInfo - Failed to read version");
            }
        }
    }
}
