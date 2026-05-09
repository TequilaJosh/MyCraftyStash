using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MyCraftyStash.Models;
using MyCraftyStash.ViewModels;

namespace MyCraftyStash.Views
{
    public partial class SentimentSearchView : UserControl
    {
        private bool _isSelecting;
        private Point _selectionStart;
        private double _canvasX, _canvasY, _canvasWidth, _canvasHeight;
        
        public SentimentSearchView()
        {
            InitializeComponent();
            Loaded += (s, e) => UpdateCanvasSize();
        }
        
        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is SentimentSearchViewModel vm)
            {
                vm.SearchCommand.Execute(null);
            }
        }
        
        private void SentimentCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && 
                element.Tag is SentimentSearchResult result &&
                DataContext is SentimentSearchViewModel vm)
            {
                vm.ViewItemCommand.Execute(result);
            }
        }
        
        private void RelatedItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && 
                element.Tag is Item item &&
                DataContext is SentimentSearchViewModel vm)
            {
                vm.ViewRelatedItemCommand.Execute(item);
            }
        }
        
        private void ExtractImage_TargetUpdated(object? sender, System.Windows.Data.DataTransferEventArgs e)
        {
            // Set Canvas size to match image source dimensions when image changes
            UpdateCanvasSize();
        }
        
        private void UpdateCanvasSize()
        {
            if (ExtractImage.Source != null)
            {
                SelectionCanvas.Width = ExtractImage.Source.Width;
                SelectionCanvas.Height = ExtractImage.Source.Height;
            }
        }
        
        private void SelectionCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not SentimentSearchViewModel vm) return;
            
            _isSelecting = true;
            // Get position relative to the Canvas (which now equals image pixel coordinates)
            _selectionStart = e.GetPosition(SelectionCanvas);
            
            vm.ClearSelection();
            SelectionCanvas.CaptureMouse();
        }
        
        private void SelectionCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isSelecting || DataContext is not SentimentSearchViewModel vm) return;
            
            // Get position relative to the Canvas
            var currentPos = e.GetPosition(SelectionCanvas);
            
            _canvasX = Math.Min(_selectionStart.X, currentPos.X);
            _canvasY = Math.Min(_selectionStart.Y, currentPos.Y);
            _canvasWidth = Math.Abs(currentPos.X - _selectionStart.X);
            _canvasHeight = Math.Abs(currentPos.Y - _selectionStart.Y);
            
            vm.SetSelection(_canvasX, _canvasY, _canvasWidth, _canvasHeight);
        }
        
        private void SelectionCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isSelecting || DataContext is not SentimentSearchViewModel vm) return;
            
            _isSelecting = false;
            SelectionCanvas.ReleaseMouseCapture();
            
            if (_canvasWidth < 10 || _canvasHeight < 10)
            {
                vm.ClearSelection();
                return;
            }
            
            // Canvas coordinates are now 1:1 with image pixels
            // Just clamp to image bounds
            var source = ExtractImage.Source;
            if (source != null)
            {
                var sourceWidth = source.Width;
                var sourceHeight = source.Height;
                
                var imageX = Math.Max(0, Math.Min(_canvasX, sourceWidth - 1));
                var imageY = Math.Max(0, Math.Min(_canvasY, sourceHeight - 1));
                var imageWidth = Math.Max(1, Math.Min(_canvasWidth, sourceWidth - imageX));
                var imageHeight = Math.Max(1, Math.Min(_canvasHeight, sourceHeight - imageY));
                
                vm.SetImageSelection(imageX, imageY, imageWidth, imageHeight);
            }
            else
            {
                vm.SetImageSelection(_canvasX, _canvasY, _canvasWidth, _canvasHeight);
            }
        }
        
        private void ClearSelection_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SentimentSearchViewModel vm)
            {
                vm.ClearSelection();
            }
        }
    }
}
