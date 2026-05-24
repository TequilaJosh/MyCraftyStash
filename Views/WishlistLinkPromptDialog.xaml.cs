using System.Windows;
using JandH.Core.Models;
using JandH.Core.Services;
using MyCraftyStash.Models;
using JandH.Core.Models;
using JandH.Core.Services;
using MyCraftyStash.Services;

using JandH.Core.ViewModels;

namespace MyCraftyStash.Views
{
    public partial class WishlistLinkPromptDialog : Window
    {
        private readonly WishlistLinkImportService _importer = new();
        public WishlistItem? Result { get; private set; }

        public WishlistLinkPromptDialog()
        {
            InitializeComponent();
            Loaded += (_, __) => UrlBox.Focus();
        }

        private async void Fetch_Click(object sender, RoutedEventArgs e)
        {
            var url = UrlBox.Text?.Trim() ?? string.Empty;
            if (!Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                ErrorText.Text = "Enter a valid URL (including https://).";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            ErrorText.Visibility = Visibility.Collapsed;
            StatusText.Text = "Fetching…";
            StatusText.Visibility = Visibility.Visible;
            FetchButton.IsEnabled = false;
            UrlBox.IsEnabled = false;

            try
            {
                Result = await _importer.FetchAsync(url);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                ErrorText.Text = $"Could not load that page: {ex.Message}";
                ErrorText.Visibility = Visibility.Visible;
                StatusText.Visibility = Visibility.Collapsed;
                FetchButton.IsEnabled = true;
                UrlBox.IsEnabled = true;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
