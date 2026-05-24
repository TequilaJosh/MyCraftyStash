using System.Windows;
using JandH.Core.Services;
using MyCraftyStash.Services;

using JandH.Core.Models;
using JandH.Core.ViewModels;

namespace MyCraftyStash.Views
{
    public partial class ExportDialog : Window
    {
        private readonly ExportService _exportService;
        private bool _exporting;

        public ExportDialog(ExportService exportService)
        {
            InitializeComponent();
            _exportService = exportService;
        }

        private async void ExportBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_exporting) return;
            _exporting    = true;
            ExportBtn.IsEnabled  = false;
            CancelBtn.IsEnabled  = false;
            ProgressArea.Visibility = Visibility.Visible;
            ResultLabel.Visibility  = Visibility.Collapsed;

            bool includeImages = CsvAndImagesRadio.IsChecked == true;

            var progress = new Progress<(int current, int total, string status)>(p =>
            {
                ProgressLabel.Text = p.status;
                if (p.total > 0)
                    ProgressBar.Value = (double)p.current / p.total * 100;
            });

            try
            {
                var path = await _exportService.ExportInventoryCsvAsync(includeImages, progress);
                ProgressBar.Value       = 100;
                ProgressLabel.Text      = "Done!";
                ResultLabel.Text        = $"Saved to Desktop: {System.IO.Path.GetFileName(path)}";
                ResultLabel.Foreground  = System.Windows.Media.Brushes.Green;
                ResultLabel.Visibility  = Visibility.Visible;
                CancelBtn.Content       = "Close";
                CancelBtn.IsEnabled     = true;
            }
            catch (Exception ex)
            {
                ProgressArea.Visibility = Visibility.Collapsed;
                ResultLabel.Text        = $"Export failed: {ex.Message}";
                ResultLabel.Foreground  = System.Windows.Media.Brushes.Red;
                ResultLabel.Visibility  = Visibility.Visible;
                ExportBtn.IsEnabled     = true;
                CancelBtn.IsEnabled     = true;
                LoggingService.LogError(ex, "ExportDialog.ExportBtn_Click");
            }
            finally { _exporting = false; }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e) => Close();
    }
}
