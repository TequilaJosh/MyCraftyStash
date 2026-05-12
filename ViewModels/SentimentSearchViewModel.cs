using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.IO;
using MyCraftyStash.Models;
using MyCraftyStash.Services;
using MyCraftyStash.Views;

namespace MyCraftyStash.ViewModels
{
    public class SentimentSearchResult
    {
        public int SentimentId { get; set; }
        public int ItemId { get; set; }
        public string ImageData { get; set; } = string.Empty;
        public string ExtractedText { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;

        /// <summary>Width / Height ratio of the cropped sentiment image. 1.0 = square.</summary>
        public double AspectRatio { get; set; } = 1.0;

        /// <summary>Pixel area of the original cropped image. Set after all results collected for normalization.</summary>
        public double PixelArea { get; set; } = 1.0;

        /// <summary>0–4 size bucket assigned after normalization: 0=XS, 1=S, 2=M, 3=L, 4=XL</summary>
        public int SizeBucket { get; set; } = 2;

        public string SizeLabel => SizeBucket switch { 0 => "XS", 1 => "S", 3 => "L", 4 => "XL", _ => "M" };

        public string SizeLabelColor => SizeBucket switch
        {
            0 or 1 => "#9CA3AF",
            3      => "#7C3AED",
            4      => "#6D28D9",
            _      => "#6B7280"
        };

        /// <summary>Card width: narrow for portrait, wide for landscape. Range 150–300px.</summary>
        public double CardWidth
        {
            get
            {
                double w = 180.0 * Math.Max(0.6, Math.Min(AspectRatio, 2.2));
                return Math.Round(Math.Max(150, Math.Min(300, w)));
            }
        }

        /// <summary>
        /// Image area height varies significantly based on aspect ratio AND size bucket,
        /// so users can visually tell which sentiments belong to bigger stamps.
        /// Range: 70–220px.
        /// </summary>
        public double ImageHeight
        {
            get
            {
                double baseH = AspectRatio >= 1.0 ? 90 : Math.Min(200, 90 / AspectRatio);
                double sizeScale = SizeBucket switch { 0 => 0.60, 1 => 0.80, 3 => 1.35, 4 => 1.65, _ => 1.00 };
                return Math.Round(Math.Max(70, Math.Min(220, baseH * sizeScale)));
            }
        }
    }

    public partial class SentimentToExtract : ObservableObject
    {
        public string Text { get; set; } = string.Empty;

        [ObservableProperty]
        private bool _isExtracted;
    }

    public class UnsnippedSetResult
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public int TotalSentiments { get; set; }
        public int SnippedCount { get; set; }
        public int MissingCount => Math.Max(0, TotalSentiments - SnippedCount);
        public string ProgressLabel => $"{SnippedCount} / {TotalSentiments} snipped";
    }
    
    public partial class SentimentSearchViewModel : BaseViewModel
    {
        private readonly InventoryService _inventoryService;
        private readonly SentimentService _sentimentService;
        private readonly MainViewModel _mainVm;
        
        private string? _lastSearchText;
        
        [ObservableProperty]
        private ObservableCollection<SentimentSearchResult> _sentiments = new();
        
        [ObservableProperty]
        private SentimentSearchResult? _selectedSentiment;
        
        [ObservableProperty]
        private string _searchText = string.Empty;
        
        [ObservableProperty]
        private bool _isLoading;

        /// <summary>When true, search results include all sentiments from each matching set,
        /// with matched sentiments shown first within each set.</summary>
        [ObservableProperty]
        private bool _expandBySet;
        
        [ObservableProperty]
        private bool _isViewingItem;

        [ObservableProperty]
        private Item? _currentItem;

        /// <summary>Currently-displayed hero image (gallery selection). Null falls back to CurrentItem.ImageUrl in XAML.</summary>
        [ObservableProperty]
        private string? _currentDisplayImage;
        
        [ObservableProperty]
        private ObservableCollection<ItemImage> _currentItemImages = new();
        
        [ObservableProperty]
        private ObservableCollection<Item> _currentItemRelated = new();
        
        [ObservableProperty]
        private ObservableCollection<SentimentSearchResult> _currentItemSentiments = new();
        
        [ObservableProperty]
        private bool _isExtractingMode;
        
        [ObservableProperty]
        private ObservableCollection<Item> _itemsWithSentiments = new();
        
        [ObservableProperty]
        private Item? _selectedExtractItem;
        
        [ObservableProperty]
        private string _extractionStatus = string.Empty;
        
        [ObservableProperty]
        private bool _isManualSelectionMode;
        
        [ObservableProperty]
        private ObservableCollection<ItemImage> _extractItemImages = new();
        
        [ObservableProperty]
        private ItemImage? _selectedExtractImage;
        
        [ObservableProperty]
        private ObservableCollection<SentimentToExtract> _sentimentsToExtract = new();
        
        [ObservableProperty]
        private SentimentToExtract? _selectedSentimentToExtract;
        
        [ObservableProperty]
        private double _selectionX;
        
        [ObservableProperty]
        private double _selectionY;
        
        [ObservableProperty]
        private double _selectionWidth;
        
        [ObservableProperty]
        private double _selectionHeight;
        
        [ObservableProperty]
        private bool _hasSelection;

        [ObservableProperty]
        private bool _isViewingUnsnipped;

        [ObservableProperty]
        private ObservableCollection<UnsnippedSetResult> _unsnippedSets = new();
        
        private double _imageSelectionX;
        private double _imageSelectionY;
        private double _imageSelectionWidth;
        private double _imageSelectionHeight;
        
        public SentimentSearchViewModel(InventoryService inventoryService, MainViewModel mainVm)
        {
            _inventoryService = inventoryService;
            _sentimentService = new SentimentService();
            _mainVm = mainVm;
        }
        
        [RelayCommand]
        private async Task Search()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                Sentiments.Clear();
                IsViewingUnsnipped = false;
                return;
            }

            IsLoading = true;
            try
            {
                _lastSearchText = SearchText;
                IsViewingUnsnipped = false;

                var results = ExpandBySet
                    ? await _sentimentService.SearchSentimentsExpandedAsync(SearchText)
                    : await _sentimentService.SearchSentimentsAsync(SearchText);
                var displayResults = results.Select(s =>
                {
                    var result = new SentimentSearchResult
                    {
                        SentimentId = s.Id,
                        ItemId = s.ItemId,
                        ImageData = s.ImageData,
                        ExtractedText = s.ExtractedText,
                        ItemName = s.Item?.Name ?? "Unknown",
                        ItemType = s.Item?.Type ?? ""
                    };
                    result.AspectRatio = GetImageAspectRatio(s.ImageData);
                    result.PixelArea   = GetImagePixelArea(s.ImageData);
                    return result;
                }).ToList();

                // Assign size buckets relative to all results in this search
                AssignSizeBuckets(displayResults);

                Sentiments = new ObservableCollection<SentimentSearchResult>(displayResults);
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "Error searching sentiments");
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        [RelayCommand]
        private async Task FindUnsnippedSets()
        {
            IsLoading = true;
            try
            {
                var items = await _sentimentService.GetItemsWithSentimentsAsync();
                var counts = await _sentimentService.GetSentimentCountsByItemAsync();

                var results = new List<UnsnippedSetResult>();
                foreach (var item in items)
                {
                    var total = SentimentService.ParseSentimentsList(item.Sentiments ?? string.Empty).Count;
                    if (total <= 0) continue;
                    counts.TryGetValue(item.Id, out var snipped);
                    if (snipped >= total) continue;

                    results.Add(new UnsnippedSetResult
                    {
                        ItemId = item.Id,
                        ItemName = item.Name,
                        ItemType = item.Type,
                        ImageUrl = item.ImageUrl,
                        TotalSentiments = total,
                        SnippedCount = snipped
                    });
                }

                UnsnippedSets = new ObservableCollection<UnsnippedSetResult>(
                    results.OrderByDescending(r => r.MissingCount).ThenBy(r => r.ItemName));
                Sentiments.Clear();
                IsViewingUnsnipped = true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "Error finding unsnipped sentiment sets");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task NavigateToItemFromUnsnipped(UnsnippedSetResult? result)
        {
            if (result == null) return;
            _mainVm.NavigateToInventoryCommand.Execute(null);
            await _mainVm.InventoryVM.SelectItemByIdAsync(result.ItemId);
        }

        [RelayCommand]
        private async Task ViewItem(SentimentSearchResult? result)
        {
            if (result == null) return;
            
            IsLoading = true;
            try
            {
                var item = await _inventoryService.GetItemAsync(result.ItemId);
                if (item == null) return;
                
                CurrentItem = item;
                CurrentDisplayImage = item.ImageUrl;

                var images = await _inventoryService.GetItemImagesAsync(result.ItemId);
                CurrentItemImages = new ObservableCollection<ItemImage>(images);
                
                var related = await _inventoryService.GetRelatedItemsAsync(result.ItemId);
                CurrentItemRelated = new ObservableCollection<Item>(related);
                
                var itemSentiments = await _sentimentService.GetSentimentsByItemIdAsync(result.ItemId);
                var sentimentResults = itemSentiments.Select(s => new SentimentSearchResult
                {
                    SentimentId = s.Id,
                    ItemId = s.ItemId,
                    ImageData = s.ImageData,
                    ExtractedText = s.ExtractedText,
                    ItemName = item.Name,
                    ItemType = item.Type
                }).ToList();
                CurrentItemSentiments = new ObservableCollection<SentimentSearchResult>(sentimentResults);
                
                IsViewingItem = true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, $"Error viewing item {result.ItemId}");
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        private static double GetImageAspectRatio(string imageData)
        {
            try
            {
                if (string.IsNullOrEmpty(imageData)) return 1.0;
                int comma = imageData.IndexOf(',');
                if (comma < 0) return 1.0;
                byte[] bytes = Convert.FromBase64String(imageData.Substring(comma + 1));
                using var ms = new System.IO.MemoryStream(bytes);
                var frame = System.Windows.Media.Imaging.BitmapFrame.Create(
                    ms,
                    System.Windows.Media.Imaging.BitmapCreateOptions.DelayCreation,
                    System.Windows.Media.Imaging.BitmapCacheOption.None);
                if (frame.PixelHeight == 0) return 1.0;
                return (double)frame.PixelWidth / frame.PixelHeight;
            }
            catch { return 1.0; }
        }

        private static double GetImagePixelArea(string imageData)
        {
            try
            {
                if (string.IsNullOrEmpty(imageData)) return 1.0;
                int comma = imageData.IndexOf(',');
                if (comma < 0) return 1.0;
                byte[] bytes = Convert.FromBase64String(imageData.Substring(comma + 1));
                using var ms = new System.IO.MemoryStream(bytes);
                var frame = System.Windows.Media.Imaging.BitmapFrame.Create(
                    ms,
                    System.Windows.Media.Imaging.BitmapCreateOptions.DelayCreation,
                    System.Windows.Media.Imaging.BitmapCacheOption.None);
                return (double)frame.PixelWidth * frame.PixelHeight;
            }
            catch { return 1.0; }
        }

        /// <summary>
        /// Divides results into 5 size buckets (XS/S/M/L/XL) based on pixel area
        /// relative to the median result so each search normalizes to itself.
        /// </summary>
        private static void AssignSizeBuckets(List<SentimentSearchResult> results)
        {
            if (results.Count == 0) return;

            var areas = results.Select(r => r.PixelArea).OrderBy(a => a).ToList();
            double median = areas[areas.Count / 2];
            if (median <= 0) median = 1;

            foreach (var r in results)
            {
                double ratio = r.PixelArea / median;
                r.SizeBucket = ratio switch
                {
                    < 0.25  => 0,   // XS: much smaller than median
                    < 0.60  => 1,   // S
                    < 1.70  => 2,   // M: close to median
                    < 4.00  => 3,   // L
                    _       => 4    // XL: much larger than median
                };
            }
        }

        [RelayCommand]
        private void GoBack()
        {
            IsViewingItem = false;
            CurrentItem = null;
            CurrentDisplayImage = null;
            CurrentItemImages.Clear();
            CurrentItemRelated.Clear();
            CurrentItemSentiments.Clear();
        }

        public void SelectGalleryImage(ItemImage clickedImage)
        {
            if (clickedImage == null) return;
            CurrentDisplayImage = clickedImage.ImageUrl;
        }

        public void SelectMainImage()
        {
            CurrentDisplayImage = CurrentItem?.ImageUrl;
        }
        
        [RelayCommand]
        private async Task ViewRelatedItem(Item? item)
        {
            if (item == null) return;
            
            var tempResult = new SentimentSearchResult
            {
                ItemId = item.Id,
                ItemName = item.Name,
                ItemType = item.Type
            };
            await ViewItem(tempResult);
        }
        
        [RelayCommand]
        private async Task StartExtractMode()
        {
            IsExtractingMode = true;
            ExtractionStatus = "";
            
            try
            {
                var items = await _sentimentService.GetItemsWithSentimentsAsync();
                ItemsWithSentiments = new ObservableCollection<Item>(items);
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "Error loading items with sentiments");
            }
        }
        
        [RelayCommand]
        private void CancelExtractMode()
        {
            IsExtractingMode = false;
            IsManualSelectionMode = false;
            SelectedExtractItem = null;
            SelectedExtractImage = null;
            SelectedSentimentToExtract = null;
            ExtractItemImages.Clear();
            SentimentsToExtract.Clear();
            ClearSelection();
            ExtractionStatus = "";
        }
        
        [RelayCommand]
        private async Task StartManualSelection()
        {
            if (SelectedExtractItem == null) return;
            
            IsLoading = true;
            try
            {
                var allImages = new List<ItemImage>();
                
                if (!string.IsNullOrEmpty(SelectedExtractItem.ImageUrl))
                {
                    allImages.Add(new ItemImage 
                    { 
                        Id = 0, 
                        ItemId = SelectedExtractItem.Id, 
                        ImageUrl = SelectedExtractItem.ImageUrl,
                        SortOrder = 0
                    });
                }
                
                var additionalImages = await _inventoryService.GetItemImagesAsync(SelectedExtractItem.Id);
                allImages.AddRange(additionalImages.OrderBy(i => i.SortOrder));
                
                if (allImages.Count == 0)
                {
                    ExtractionStatus = "No images found for this item";
                    return;
                }
                
                var sentimentsList = SentimentService.ParseSentimentsList(SelectedExtractItem.Sentiments);
                
                // Show all sentiments from the item (don't filter out already extracted ones)
                // This allows users to re-extract if needed
                var cropWindow = new SentimentCropView(SelectedExtractItem, allImages, sentimentsList);
                cropWindow.Owner = System.Windows.Application.Current.MainWindow;
                cropWindow.ShowDialog();
                
                if (cropWindow.SentimentSaved && !string.IsNullOrWhiteSpace(_lastSearchText))
                {
                    await Search();
                }
                
                ExtractionStatus = cropWindow.SentimentSaved ? "Sentiment(s) saved successfully!" : "";
            }
            catch (Exception ex)
            {
                ExtractionStatus = $"Error: {ex.Message}";
                LoggingService.LogError(ex, "Error opening crop tool");
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        [RelayCommand]
        private void CancelManualSelection()
        {
            IsManualSelectionMode = false;
            ExtractItemImages.Clear();
            SentimentsToExtract.Clear();
            SelectedExtractImage = null;
            SelectedSentimentToExtract = null;
            ClearSelection();
            ExtractionStatus = "";
        }
        
        public void ClearSelection()
        {
            SelectionX = 0;
            SelectionY = 0;
            SelectionWidth = 0;
            SelectionHeight = 0;
            HasSelection = false;
            _imageSelectionX = 0;
            _imageSelectionY = 0;
            _imageSelectionWidth = 0;
            _imageSelectionHeight = 0;
        }
        
        public void SetSelection(double x, double y, double width, double height)
        {
            SelectionX = x;
            SelectionY = y;
            SelectionWidth = width;
            SelectionHeight = height;
            HasSelection = width > 10 && height > 10;
        }
        
        public void SetImageSelection(double x, double y, double width, double height)
        {
            _imageSelectionX = x;
            _imageSelectionY = y;
            _imageSelectionWidth = width;
            _imageSelectionHeight = height;
        }
        
        [RelayCommand]
        private async Task SnipSentiment()
        {
            if (SelectedExtractItem == null || SelectedExtractImage == null || 
                SelectedSentimentToExtract == null || !HasSelection)
            {
                ExtractionStatus = "Please select an image, sentiment, and draw a rectangle first";
                return;
            }
            
            IsLoading = true;
            try
            {
                var croppedImageData = await CropImageAsync(
                    SelectedExtractImage.ImageUrl,
                    (int)_imageSelectionX, (int)_imageSelectionY, (int)_imageSelectionWidth, (int)_imageSelectionHeight);
                
                if (string.IsNullOrEmpty(croppedImageData))
                {
                    ExtractionStatus = "Failed to crop image";
                    return;
                }
                
                await _sentimentService.AddSentimentImageAsync(
                    SelectedExtractItem.Id, 
                    croppedImageData, 
                    SelectedSentimentToExtract.Text);
                
                SelectedSentimentToExtract.IsExtracted = true;
                ExtractionStatus = $"Saved sentiment: \"{SelectedSentimentToExtract.Text}\"";
                
                ClearSelection();
                
                var nextSentiment = SentimentsToExtract.FirstOrDefault(s => !s.IsExtracted);
                if (nextSentiment != null)
                {
                    SelectedSentimentToExtract = nextSentiment;
                }
                else
                {
                    ExtractionStatus = "All sentiments extracted! Click Done to finish.";
                }
                
                if (!string.IsNullOrWhiteSpace(_lastSearchText))
                {
                    await Search();
                }
            }
            catch (Exception ex)
            {
                ExtractionStatus = $"Error: {ex.Message}";
                LoggingService.LogError(ex, "Error snipping sentiment");
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        private async Task<string?> CropImageAsync(string base64ImageData, int x, int y, int width, int height)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var imageData = base64ImageData
                        .Replace("data:image/png;base64,", "")
                        .Replace("data:image/jpeg;base64,", "")
                        .Replace("data:image/jpg;base64,", "")
                        .Replace("data:image/webp;base64,", "");
                    
                    var imageBytes = Convert.FromBase64String(imageData);
                    
                    using var stream = new MemoryStream(imageBytes);
                    using var bitmap = new System.Drawing.Bitmap(stream);
                    
                    x = Math.Max(0, Math.Min(x, bitmap.Width - 1));
                    y = Math.Max(0, Math.Min(y, bitmap.Height - 1));
                    width = Math.Min(width, bitmap.Width - x);
                    height = Math.Min(height, bitmap.Height - y);
                    
                    if (width <= 0 || height <= 0)
                        return null;
                    
                    var rect = new System.Drawing.Rectangle(x, y, width, height);
                    using var cropped = bitmap.Clone(rect, bitmap.PixelFormat);
                    
                    using var outputStream = new MemoryStream();
                    cropped.Save(outputStream, System.Drawing.Imaging.ImageFormat.Png);
                    var croppedBase64 = Convert.ToBase64String(outputStream.ToArray());
                    
                    return $"data:image/png;base64,{croppedBase64}";
                }
                catch (Exception ex)
                {
                    LoggingService.LogError(ex, "Error cropping image");
                    return null;
                }
            });
        }
        
        [RelayCommand]
        private async Task DeleteSentiment(SentimentSearchResult? sentiment)
        {
            if (sentiment == null) return;
            
            var result = System.Windows.MessageBox.Show(
                $"Delete this sentiment image?\n\n\"{sentiment.ExtractedText}\"",
                "Confirm Delete",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question
            );
            
            if (result != System.Windows.MessageBoxResult.Yes) return;
            
            try
            {
                await _sentimentService.DeleteSentimentImageAsync(sentiment.SentimentId);
                
                Sentiments.Remove(sentiment);
                CurrentItemSentiments.Remove(sentiment);
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, $"Error deleting sentiment {sentiment.SentimentId}");
            }
        }
        
        [RelayCommand]
        private async Task NavigateToItemInInventory()
        {
            if (CurrentItem == null) return;
            
            _mainVm.NavigateToInventoryCommand.Execute(null);
            await _mainVm.InventoryVM.SelectItemByIdAsync(CurrentItem.Id);
        }
    }
}
