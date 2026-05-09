using System.Windows;
using MyCraftyStash.Models;
using MyCraftyStash.Services;

namespace MyCraftyStash.Views
{
    public partial class UpdateSettingsDialog : Window
    {
        public UpdateSettings Settings { get; private set; }
        public bool WasSaved { get; private set; }
        
        public UpdateSettingsDialog(UpdateSettings? existingSettings = null)
        {
            InitializeComponent();
            Settings = existingSettings ?? new UpdateSettings();
            DataContext = Settings;
        }
        
        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var path = UpdatePathTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(path))
            {

            }
            else
            {
                System.Windows.MessageBox.Show("Please enter a path first.", "No Path", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            Settings.UpdatePath = UpdatePathTextBox.Text.Trim();
            Settings.CheckForUpdatesOnStartup = CheckOnStartupCheckBox.IsChecked ?? true;
            Settings.Save();
            WasSaved = true;
            DialogResult = true;
            Close();
        }
        
        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            WasSaved = false;
            DialogResult = false;
            Close();
        }
    }
}
