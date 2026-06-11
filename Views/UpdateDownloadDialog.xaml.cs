using System;
using System.Threading;
using System.Windows;
using MyCraftyStash.Services;

namespace MyCraftyStash.Views
{
    /// <summary>
    /// Modal progress dialog that downloads a release installer from GitHub.
    /// ShowDialog() returns true when the download finished; the file path is
    /// then in <see cref="InstallerPath"/>. Cancel (or a failure) returns
    /// false, with the failure message in <see cref="ErrorMessage"/>.
    /// </summary>
    public partial class UpdateDownloadDialog : Window
    {
        private readonly string _assetUrl;
        private readonly string _assetName;
        private readonly CancellationTokenSource _cts = new();

        public string? InstallerPath { get; private set; }
        public string? ErrorMessage { get; private set; }

        public UpdateDownloadDialog(string assetUrl, string assetName)
        {
            InitializeComponent();
            _assetUrl = assetUrl;
            _assetName = assetName;
            Loaded += async (_, _) => await RunDownloadAsync();
        }

        private async System.Threading.Tasks.Task RunDownloadAsync()
        {
            var progress = new Progress<(long bytes, long total)>(p =>
            {
                if (p.total > 0)
                {
                    DownloadProgress.IsIndeterminate = false;
                    DownloadProgress.Value = 100.0 * p.bytes / p.total;
                    StatusText.Text = $"{FormatMb(p.bytes)} of {FormatMb(p.total)} MB";
                }
                else
                {
                    DownloadProgress.IsIndeterminate = true;
                    StatusText.Text = $"{FormatMb(p.bytes)} MB downloaded";
                }
            });

            try
            {
                InstallerPath = await UpdateService.DownloadUpdateAsync(
                    _assetUrl, _assetName, progress, _cts.Token);
                DialogResult = true;
            }
            catch (OperationCanceledException)
            {
                ErrorMessage = "Download cancelled.";
                DialogResult = false;
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "UpdateDownloadDialog.RunDownloadAsync");
                ErrorMessage = ex.Message;
                DialogResult = false;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            CancelButton.IsEnabled = false;
            StatusText.Text = "Cancelling...";
            _cts.Cancel();
        }

        protected override void OnClosed(EventArgs e)
        {
            // Window X while downloading counts as cancel.
            _cts.Cancel();
            _cts.Dispose();
            base.OnClosed(e);
        }

        private static string FormatMb(long bytes) =>
            (bytes / 1048576.0).ToString("0.0");
    }
}
