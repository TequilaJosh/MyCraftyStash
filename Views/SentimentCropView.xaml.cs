using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MyCraftyStash.Models;
using MyCraftyStash.Services;

namespace MyCraftyStash.Views
{
    public partial class SentimentCropView : Window
    {
        private readonly Item _item;
        private readonly List<ItemImage> _images;
        private readonly ObservableCollection<string> _sentimentsList;
        private readonly SentimentService _sentimentService;
        
        private bool _isDrawing;
        private bool _isMoving;
        private Point _startPoint;
        private Point _moveOffset;
        
        private double _selectionX, _selectionY, _selectionWidth, _selectionHeight;
        private BitmapSource? _currentBitmap;
        
        private double _zoomLevel = 1.0;
        private readonly ObservableCollection<SentimentImage> _savedThisSession = new();
        private const double ZoomStep = 0.25;
        private const double MinZoom = 0.25;
        private const double MaxZoom = 5.0;
        
        public bool SentimentSaved { get; private set; }

        // Draft mode - when no item exists yet, snips are held in memory
        private readonly bool _isDraftMode;
        public List<(string ImageData, string Text)> DraftSnips { get; } = new();
        
        public SentimentCropView(Item item, List<ItemImage> images, List<string> sentiments)
        {
            InitializeComponent();
            
            _item = item;
            _images = images;
            _sentimentsList = new ObservableCollection<string>(sentiments);
            _sentimentService = new SentimentService();
            
            Loaded += OnLoaded;
        }

        // Draft mode constructor - item not saved yet
        public SentimentCropView(List<ItemImage> images, List<string> sentiments)
        {
            InitializeComponent();
            _item = null!; // not used in draft mode
            _images = images;
            _sentimentsList = new ObservableCollection<string>(sentiments);
            _sentimentService = new SentimentService();
            _isDraftMode = true;
            Loaded += OnLoaded;
        }
        
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded; // unsubscribe so it only fires once even if Loaded is raised again
            ImageSelector.Items.Clear();
            for (int i = 0; i < _images.Count; i++)
            {
                ImageSelector.Items.Add($"Image {i + 1}");
            }
            
            if (_images.Count > 0)
            {
                ImageSelector.SelectedIndex = 0;
            }
            
            SentimentsList.ItemsSource = _sentimentsList;
            
            if (_sentimentsList.Count > 0)
            {
                SentimentsLabel.Text = $"Or select from item sentiments ({_sentimentsList.Count}):";
            }
            else
            {
                SentimentsLabel.Text = "No sentiments defined for this item. Type the text manually.";
            }
            
            StatusText.Text = "Draw a rectangle on the image, then drag to reposition if needed";

            // Apply starting zoom of 100%
            SetZoom(1.0);
        }
        
        private void ImageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ImageSelector.SelectedIndex >= 0 && ImageSelector.SelectedIndex < _images.Count)
            {
                LoadImage(_images[ImageSelector.SelectedIndex].ImageUrl);
                ClearSelection();
            }
        }
        
        private static string StripDataUriPrefix(string base64Data)
        {
            var commaIndex = base64Data.IndexOf(',');
            return commaIndex >= 0 ? base64Data[(commaIndex + 1)..] : base64Data;
        }

        private void LoadImage(string base64Data)
        {
            try
            {
                var imageData = StripDataUriPrefix(base64Data);
                
                var bytes = Convert.FromBase64String(imageData);
                using var stream = new MemoryStream(bytes);
                
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                
                _currentBitmap = bitmap;
                SourceImage.Source = bitmap;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error loading image: {ex.Message}";
                LoggingService.LogError(ex, "Error loading image for crop");
            }
        }
        
        private Rect GetImageBoundsInContainer()
        {
            if (_currentBitmap == null || SourceImage.ActualWidth == 0) 
                return new Rect(0, 0, ImageContainer.ActualWidth, ImageContainer.ActualHeight);
            
            double containerWidth = ImageContainer.ActualWidth;
            double containerHeight = ImageContainer.ActualHeight;
            double imageRatio = _currentBitmap.PixelWidth / (double)_currentBitmap.PixelHeight;
            double containerRatio = containerWidth / containerHeight;
            
            double displayWidth, displayHeight, offsetX, offsetY;
            
            if (imageRatio > containerRatio)
            {
                displayWidth = containerWidth;
                displayHeight = containerWidth / imageRatio;
                offsetX = 0;
                offsetY = (containerHeight - displayHeight) / 2;
            }
            else
            {
                displayHeight = containerHeight;
                displayWidth = containerHeight * imageRatio;
                offsetX = (containerWidth - displayWidth) / 2;
                offsetY = 0;
            }
            
            return new Rect(offsetX, offsetY, displayWidth, displayHeight);
        }
        
        private Point ContainerToImagePixels(Point containerPoint)
        {
            if (_currentBitmap == null) return containerPoint;
            
            var bounds = GetImageBoundsInContainer();
            
            double relativeX = (containerPoint.X - bounds.X) / bounds.Width;
            double relativeY = (containerPoint.Y - bounds.Y) / bounds.Height;
            
            return new Point(
                relativeX * _currentBitmap.PixelWidth,
                relativeY * _currentBitmap.PixelHeight
            );
        }
        
        private Point ImagePixelsToContainer(Point imagePoint)
        {
            if (_currentBitmap == null) return imagePoint;
            
            var bounds = GetImageBoundsInContainer();
            
            double relativeX = imagePoint.X / _currentBitmap.PixelWidth;
            double relativeY = imagePoint.Y / _currentBitmap.PixelHeight;
            
            return new Point(
                bounds.X + relativeX * bounds.Width,
                bounds.Y + relativeY * bounds.Height
            );
        }
        
        private bool IsPointInSelection(Point containerPoint)
        {
            if (SelectionRect.Visibility != Visibility.Visible) return false;
            
            double left = Canvas.GetLeft(SelectionRect);
            double top = Canvas.GetTop(SelectionRect);
            
            return containerPoint.X >= left && containerPoint.X <= left + SelectionRect.Width &&
                   containerPoint.Y >= top && containerPoint.Y <= top + SelectionRect.Height;
        }
        
        private void ImageContainer_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(ImageContainer);
            
            if (IsPointInSelection(pos))
            {
                _isMoving = true;
                _moveOffset = new Point(pos.X - Canvas.GetLeft(SelectionRect), pos.Y - Canvas.GetTop(SelectionRect));
                ImageContainer.Cursor = Cursors.SizeAll;
            }
            else
            {
                _isDrawing = true;
                _startPoint = pos;
                
                SelectionRect.Visibility = Visibility.Visible;
                Canvas.SetLeft(SelectionRect, pos.X);
                Canvas.SetTop(SelectionRect, pos.Y);
                SelectionRect.Width = 0;
                SelectionRect.Height = 0;
            }
            
            ImageContainer.CaptureMouse();
        }
        
        private void ImageContainer_MouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(ImageContainer);
            var bounds = GetImageBoundsInContainer();
            
            if (_isDrawing)
            {
                double x = Math.Max(bounds.X, Math.Min(_startPoint.X, pos.X));
                double y = Math.Max(bounds.Y, Math.Min(_startPoint.Y, pos.Y));
                double width = Math.Abs(pos.X - _startPoint.X);
                double height = Math.Abs(pos.Y - _startPoint.Y);
                
                double right = Math.Min(x + width, bounds.Right);
                double bottom = Math.Min(y + height, bounds.Bottom);
                x = Math.Max(x, bounds.X);
                y = Math.Max(y, bounds.Y);
                width = right - x;
                height = bottom - y;
                
                Canvas.SetLeft(SelectionRect, x);
                Canvas.SetTop(SelectionRect, y);
                SelectionRect.Width = Math.Max(0, width);
                SelectionRect.Height = Math.Max(0, height);
            }
            else if (_isMoving)
            {
                double newX = pos.X - _moveOffset.X;
                double newY = pos.Y - _moveOffset.Y;
                
                newX = Math.Max(bounds.X, Math.Min(newX, bounds.Right - SelectionRect.Width));
                newY = Math.Max(bounds.Y, Math.Min(newY, bounds.Bottom - SelectionRect.Height));
                
                Canvas.SetLeft(SelectionRect, newX);
                Canvas.SetTop(SelectionRect, newY);
            }
            else
            {
                ImageContainer.Cursor = IsPointInSelection(pos) ? Cursors.SizeAll : Cursors.Cross;
            }
        }
        
        private void ImageContainer_MouseUp(object sender, MouseButtonEventArgs e)
        {
            bool wasDrawing = _isDrawing;
            _isDrawing = false;
            _isMoving = false;
            ImageContainer.ReleaseMouseCapture();
            ImageContainer.Cursor = Cursors.Cross;
            
            if (SelectionRect.Width < 10 || SelectionRect.Height < 10)
            {
                if (wasDrawing)
                {
                    ClearSelection();
                }
                return;
            }
            
            CalculateImageSelection();
            UpdatePreview();
            UpdateSaveButtonState();
            
            StatusText.Text = "Drag the selection to reposition, or Save when ready";
        }
        
        private void CalculateImageSelection()
        {
            if (_currentBitmap == null) return;
            
            var topLeft = ContainerToImagePixels(new Point(Canvas.GetLeft(SelectionRect), Canvas.GetTop(SelectionRect)));
            var bottomRight = ContainerToImagePixels(new Point(
                Canvas.GetLeft(SelectionRect) + SelectionRect.Width,
                Canvas.GetTop(SelectionRect) + SelectionRect.Height));
            
            _selectionX = Math.Max(0, Math.Min(topLeft.X, _currentBitmap.PixelWidth - 1));
            _selectionY = Math.Max(0, Math.Min(topLeft.Y, _currentBitmap.PixelHeight - 1));
            _selectionWidth = Math.Max(1, Math.Min(bottomRight.X - topLeft.X, _currentBitmap.PixelWidth - _selectionX));
            _selectionHeight = Math.Max(1, Math.Min(bottomRight.Y - topLeft.Y, _currentBitmap.PixelHeight - _selectionY));
        }
        
        private void UpdatePreview()
        {
            if (_currentBitmap == null || _selectionWidth < 1 || _selectionHeight < 1)
            {
                PreviewImage.Source = null;
                return;
            }
            
            try
            {
                var cropRect = new Int32Rect(
                    (int)_selectionX, 
                    (int)_selectionY, 
                    (int)_selectionWidth, 
                    (int)_selectionHeight);
                
                var cropped = new CroppedBitmap(_currentBitmap, cropRect);
                PreviewImage.Source = cropped;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Preview error: {ex.Message}";
            }
        }
        
        private void UpdateSaveButtonState()
        {
            SaveButton.IsEnabled = _selectionWidth >= 10 && _selectionHeight >= 10;
        }
        
        private void ClearSelection_Click(object sender, RoutedEventArgs e)
        {
            ClearSelection();
        }
        
        private void ClearSelection()
        {
            _selectionX = _selectionY = _selectionWidth = _selectionHeight = 0;
            SelectionRect.Visibility = Visibility.Collapsed;
            SelectionRect.Width = 0;
            SelectionRect.Height = 0;
            PreviewImage.Source = null;
            SaveButton.IsEnabled = false;
            StatusText.Text = "Draw a rectangle on the image, then drag to reposition if needed";
        }
        
        private void SentimentsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SentimentsList.SelectedItem is string text)
            {
                SentimentTextBox.Text = text;
            }
        }
        
        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentBitmap == null || _selectionWidth < 10 || _selectionHeight < 10)
            {
                StatusText.Text = "Please select an area first";
                return;
            }
            
            var sentimentText = SentimentTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(sentimentText))
            {
                StatusText.Text = "Please enter the sentiment text";
                return;
            }
            
            SaveButton.IsEnabled = false;
            StatusText.Text = "Saving...";
            
            try
            {
                var croppedData = CropCurrentSelection();
                if (string.IsNullOrEmpty(croppedData))
                {
                    StatusText.Text = "Failed to crop image";
                    SaveButton.IsEnabled = true;
                    return;
                }
                
                if (_isDraftMode)
                {
                    // Hold in memory - will be saved after item is created
                    DraftSnips.Add((croppedData, sentimentText));
                    var draft = new SentimentImage { Id = -DraftSnips.Count, ExtractedText = sentimentText, ImageData = croppedData };
                    _savedThisSession.Add(draft);
                }
                else
                {
                    var newSentiment = await _sentimentService.AddSentimentImageAsync(_item.Id, croppedData, sentimentText);
                    _savedThisSession.Add(newSentiment);
                }

                SentimentSaved = true;
                StatusText.Text = $"Saved: \"{sentimentText}\"";

                // Add to the live strip below the crop tool
                SavedSentimentsItems.ItemsSource = null;
                SavedSentimentsItems.ItemsSource = _savedThisSession;
                SavedSentimentsPanel.Visibility = System.Windows.Visibility.Visible;
                
                var match = _sentimentsList.FirstOrDefault(s => 
                    string.Equals(s, sentimentText, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                    _sentimentsList.Remove(match);
                SentimentTextBox.Clear();
                ClearSelection();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
                SaveButton.IsEnabled = true;
                LoggingService.LogError(ex, "Error saving sentiment");
            }
        }
        
        private string? CropCurrentSelection()
        {
            if (_currentBitmap == null) return null;
            
            try
            {
                var cropRect = new Int32Rect(
                    (int)_selectionX, 
                    (int)_selectionY, 
                    (int)_selectionWidth, 
                    (int)_selectionHeight);
                
                var cropped = new CroppedBitmap(_currentBitmap, cropRect);
                
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(cropped));
                
                using var stream = new MemoryStream();
                encoder.Save(stream);
                var base64 = Convert.ToBase64String(stream.ToArray());
                
                return $"data:image/png;base64,{base64}";
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "Error cropping selection");
                return null;
            }
        }
        
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = SentimentSaved;
            Close();
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e) => SetZoom(_zoomLevel + ZoomStep);
        private void ZoomOut_Click(object sender, RoutedEventArgs e) => SetZoom(_zoomLevel - ZoomStep);
        private void ZoomReset_Click(object sender, RoutedEventArgs e) => SetZoom(1.0);

        private void SetZoom(double newZoom)
        {
            _zoomLevel = Math.Max(MinZoom, Math.Min(MaxZoom, newZoom));
            ZoomTransform.ScaleX = _zoomLevel;
            ZoomTransform.ScaleY = _zoomLevel;
            ZoomLabel.Text = $"{(int)(_zoomLevel * 100)}%";
            ClearSelection();
        }

        private void ImageScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                SetZoom(_zoomLevel + (e.Delta > 0 ? ZoomStep : -ZoomStep));
            }
        }
    private async void DeleteSavedSentiment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is int sentimentId)
        {
            try
            {
                var item = _savedThisSession.FirstOrDefault(s => s.Id == sentimentId);
                if (_isDraftMode)
                {
                    // Remove from draft list by matching negative ID index
                    int draftIndex = (-sentimentId) - 1;
                    if (draftIndex >= 0 && draftIndex < DraftSnips.Count)
                        DraftSnips.RemoveAt(draftIndex);
                }
                else
                {
                    await _sentimentService.DeleteSentimentImageAsync(sentimentId);
                }
                if (item != null)
                {
                    _savedThisSession.Remove(item);
                    SavedSentimentsItems.ItemsSource = null;
                    SavedSentimentsItems.ItemsSource = _savedThisSession;
                }
                if (_savedThisSession.Count == 0)
                    SavedSentimentsPanel.Visibility = System.Windows.Visibility.Collapsed;
                StatusText.Text = "Sentiment removed.";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error removing: {ex.Message}";
            }
        }
    }

    }
}