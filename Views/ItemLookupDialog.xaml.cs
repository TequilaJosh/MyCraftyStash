using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MyCraftyStash.Models;
using MyCraftyStash.Services;

namespace MyCraftyStash.Views
{
    public partial class ItemLookupDialog : Window
    {
        public CatalogLookupResult? SelectedResult { get; private set; }

        private readonly InventoryService _inventoryService = new();
        private List<CatalogLookupResult> _results = new();
        private CatalogLookupResult? _previewing;
        private List<string> _previewAllImages = new();

        public ItemLookupDialog()
        {
            InitializeComponent();
            Loaded += (_, _) => SearchBox.Focus();
        }

        // Search

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) RunSearch();
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e) => RunSearch();

        private async void RunSearch()
        {
            var query = SearchBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(query)) return;

            LoadingText.Visibility    = Visibility.Visible;
            NoResultsPanel.Visibility = Visibility.Collapsed;
            ResultsList.Visibility    = Visibility.Collapsed;
            ClearPreview();
            SearchButton.IsEnabled    = false;

            try
            {
                _results = await CatalogLookupService.SearchAsync(query);

                // Flag any results whose item number already exists in the local inventory
                // so the user sees a duplicate badge in the list and a banner in the preview.
                if (_results.Count > 0)
                {
                    try
                    {
                        var existing = await _inventoryService.GetExistingItemsByItemNumbersAsync(
                            _results.Select(r => r.ItemNumber));
                        foreach (var r in _results)
                        {
                            if (!string.IsNullOrWhiteSpace(r.ItemNumber)
                                && existing.TryGetValue(r.ItemNumber.Trim(), out var hit))
                            {
                                r.IsAlreadyInInventory = true;
                                r.ExistingItemId       = hit.Id;
                                r.ExistingItemName     = hit.Name;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggingService.LogError(ex, "ItemLookupDialog.RunSearch existing-check");
                    }
                }

                var dupCount = _results.Count(r => r.IsAlreadyInInventory);
                StatusText.Text = _results.Count == 0
                    ? "No results"
                    : dupCount > 0
                        ? $"{_results.Count} result{(_results.Count == 1 ? "" : "s")} · {dupCount} already in inventory"
                        : $"{_results.Count} result{(_results.Count == 1 ? "" : "s")} found";

                if (_results.Count == 0)
                {
                    NoResultsPanel.Visibility = Visibility.Visible;
                }
                else
                {
                    ResultsList.Visibility  = Visibility.Visible;
                    ResultsList.ItemsSource = null;
                    ResultsList.ItemsSource = _results;
                    if (_results.Count == 1)
                        ResultsList.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "Search failed - check your connection";
                LoggingService.LogError(ex, "ItemLookupDialog.RunSearch");
            }
            finally
            {
                LoadingText.Visibility = Visibility.Collapsed;
                SearchButton.IsEnabled = true;
            }
        }

        // Preview

        private void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ResultsList.SelectedItem is CatalogLookupResult result)
                ShowPreview(result);
            else
                ClearPreview();
        }

        private void ShowPreview(CatalogLookupResult item)
        {
            _previewing       = item;
            _previewAllImages = item.AllImages;

            PreviewName.Text       = item.Name;
            PreviewType.Text       = item.Type;
            PreviewItemNumber.Text = string.IsNullOrEmpty(item.ItemNumber) ? "-" : item.ItemNumber;
            PreviewTheme.Text      = string.IsNullOrEmpty(item.Theme)      ? "-" : item.Theme;
            PreviewPrice.Text      = item.Price.HasValue ? $"${item.Price:F2}" : "-";

            if (item.IsAlreadyInInventory)
            {
                DuplicateBanner.Visibility = Visibility.Visible;
                DuplicateBannerText.Text =
                    $"\"{item.ExistingItemName}\" is already in your inventory with item number {item.ItemNumber}.";
            }
            else
            {
                DuplicateBanner.Visibility = Visibility.Collapsed;
            }

            if (_previewAllImages.Count > 0)
            {
                ImageArea.Visibility = Visibility.Visible;
                ShowMainImage(_previewAllImages[0]);
                BuildThumbStrip(_previewAllImages);
            }
            else
            {
                ImageArea.Visibility = Visibility.Collapsed;
            }

            var parts = new List<string> { "Name", "Type" };
            if (!string.IsNullOrEmpty(item.ItemNumber)) parts.Add("Item Number");
            if (!string.IsNullOrEmpty(item.Theme))      parts.Add("Theme");
            if (item.Price.HasValue)                    parts.Add("Price");
            if (_previewAllImages.Count > 0)
                parts.Add($"{_previewAllImages.Count} image{(_previewAllImages.Count == 1 ? "" : "s")}");
            ImportSummary.Text = string.Join(", ", parts);

            EmptyPreview.Visibility = Visibility.Collapsed;
            PreviewPanel.Visibility = Visibility.Visible;
            FooterHint.Visibility   = Visibility.Visible;
            ImportButton.IsEnabled  = true;
        }

        private void ShowMainImage(string source)
        {
            try
            {
                if (string.IsNullOrEmpty(source)) return;
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                if (source.StartsWith("data:") || (source.Contains(',') && !source.StartsWith("http")))
                {
                    var commaIdx = source.IndexOf(',');
                    var data  = commaIdx >= 0 ? source[(commaIdx + 1)..] : source;
                    var bytes = Convert.FromBase64String(data);
                    bmp.StreamSource = new System.IO.MemoryStream(bytes);
                }
                else
                {
                    bmp.UriSource = new Uri(source, UriKind.Absolute);
                }
                bmp.EndInit();
                bmp.Freeze();
                PreviewImage.Source = bmp;
            }
            catch { PreviewImage.Source = null; }
        }

        private void BuildThumbStrip(List<string> images)
        {
            ThumbPanel.Children.Clear();
            if (images.Count <= 1) { ThumbStrip.Visibility = Visibility.Collapsed; return; }

            ThumbStrip.Visibility = Visibility.Visible;
            for (int i = 0; i < images.Count; i++)
            {
                int idx = i;
                var border = new Border
                {
                    Width = 48, Height = 48,
                    Margin          = new Thickness(0, 0, 6, 0),
                    CornerRadius    = new CornerRadius(4),
                    BorderThickness = new Thickness(2),
                    BorderBrush     = idx == 0
                        ? (Brush)FindResource("PrimaryBrush")
                        : (Brush)FindResource("BorderBrush"),
                    ClipToBounds = true,
                    Cursor       = Cursors.Hand
                };
                var img = new Image { Stretch = Stretch.UniformToFill };
                try
                {
                    var src = images[i];
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption      = BitmapCacheOption.OnLoad;
                    bmp.DecodePixelWidth = 96;
                    if (src.StartsWith("data:") || (src.Contains(',') && !src.StartsWith("http")))
                    {
                        var commaIdx = src.IndexOf(',');
                        var data  = commaIdx >= 0 ? src[(commaIdx + 1)..] : src;
                        var bytes = Convert.FromBase64String(data);
                        bmp.StreamSource = new System.IO.MemoryStream(bytes);
                    }
                    else
                    {
                        bmp.UriSource = new Uri(src, UriKind.Absolute);
                    }
                    bmp.EndInit();
                    bmp.Freeze();
                    img.Source = bmp;
                }
                catch { }
                border.Child = img;
                border.MouseLeftButtonUp += (_, _) =>
                {
                    ShowMainImage(images[idx]);
                    foreach (Border b in ThumbPanel.Children)
                        b.BorderBrush = (Brush)FindResource("BorderBrush");
                    border.BorderBrush = (Brush)FindResource("PrimaryBrush");
                };
                ThumbPanel.Children.Add(border);
            }
        }

        private void ClearPreview()
        {
            _previewing             = null;
            _previewAllImages       = new();
            PreviewImage.Source     = null;
            ThumbPanel.Children.Clear();
            ThumbStrip.Visibility   = Visibility.Collapsed;
            ImageArea.Visibility    = Visibility.Collapsed;
            EmptyPreview.Visibility = Visibility.Visible;
            PreviewPanel.Visibility = Visibility.Collapsed;
            FooterHint.Visibility   = Visibility.Collapsed;
            ImportButton.IsEnabled  = false;
            DuplicateBanner.Visibility = Visibility.Collapsed;
        }

        // Buttons

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            if (_previewing == null) return;

            // Fetch SKU + downloaded base64 images from the product detail page
            // before handing the result back, so the new-item form gets a proper
            // local image and an item number rather than a remote URL.
            ImportButton.IsEnabled = false;
            StatusText.Text = "Loading product details…";
            try
            {
                await CatalogLookupService.EnrichResultAsync(_previewing);
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "ItemLookupDialog.ImportButton_Click enrich");
            }

            SelectedResult = _previewing;
            DialogResult   = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
