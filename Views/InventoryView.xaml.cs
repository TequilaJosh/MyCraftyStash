using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using MyCraftyStash.Models;
using MyCraftyStash.ViewModels;

namespace MyCraftyStash.Views
{
    public partial class InventoryView : UserControl
    {
        private double _savedScrollOffset;
        private bool _wasOnList = true;

        public InventoryView()
        {
            InitializeComponent();
            Loaded += InventoryView_Loaded;
        }

        private void InventoryView_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is InventoryViewModel vm)
            {
                vm.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var vm = DataContext as InventoryViewModel;
            if (vm == null) return;

            if (e.PropertyName == nameof(InventoryViewModel.IsViewingDetails) ||
                e.PropertyName == nameof(InventoryViewModel.IsEditingItem) ||
                e.PropertyName == nameof(InventoryViewModel.IsAddingItem))
            {
                bool isOnList = !vm.IsViewingDetails && !vm.IsEditingItem && !vm.IsAddingItem;

                if (!isOnList && _wasOnList)
                {
                    SaveScrollPosition();
                }

                if (isOnList && !_wasOnList)
                {
                    Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, RestoreScrollPosition);
                }

                _wasOnList = isOnList;

                // When the add form opens, force the Type ComboBox to show no selection.
                // WPF ComboBox retains its visual selection from previous sessions even
                // when the bound property is reset, so we clear it directly here.
                if (e.PropertyName == nameof(InventoryViewModel.IsAddingItem) && vm.IsAddingItem)
                {
                    Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
                    {
                        AddTypeComboBox.SelectedItem = null;
                    });
                }
            }
        }

        private void SaveScrollPosition()
        {
            var scrollViewer = FindScrollViewer(InventoryListBox);
            if (scrollViewer != null)
            {
                _savedScrollOffset = scrollViewer.VerticalOffset;
            }
        }

        private void RestoreScrollPosition()
        {
            var scrollViewer = FindScrollViewer(InventoryListBox);
            if (scrollViewer != null)
            {
                scrollViewer.ScrollToVerticalOffset(_savedScrollOffset);
            }
        }

        private static ScrollViewer? FindScrollViewer(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is ScrollViewer sv)
                    return sv;
                var result = FindScrollViewer(child);
                if (result != null)
                    return result;
            }
            return null;
        }
        
        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is InventoryViewModel vm)
            {
                vm.SearchCommand.Execute(null);
                e.Handled = true;
            }
        }
        
        private void ItemCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is Item item)
            {
                if (DataContext is InventoryViewModel vm)
                {
                    vm.ViewItemDetailsCommand.Execute(item);
                }
            }
        }

        // Middle-click (mouse wheel button) on a card opens the item in a new tab
        private void ItemCard_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle &&
                sender is FrameworkElement element && element.DataContext is Item item &&
                DataContext is InventoryViewModel vm)
            {
                vm.OpenItemInTabCommand.Execute(item);
                e.Handled = true;
            }
        }

        // Card right-click > "Open in New Tab" (ContextMenu inherits the card's Item DataContext)
        private void OpenItemInNewTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is Item item &&
                DataContext is InventoryViewModel vm)
            {
                vm.OpenItemInTabCommand.Execute(item);
            }
        }

        // ── Tab strip ────────────────────────────────────────────────────────
        private void AllItemsTab_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is InventoryViewModel vm)
                vm.BackToListCommand.Execute(null);
        }

        private void ItemTab_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is OpenTab tab &&
                DataContext is InventoryViewModel vm)
            {
                vm.ActivateTabCommand.Execute(tab);
            }
        }

        // Middle-click a tab closes it (browser-style)
        private void ItemTab_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle &&
                sender is FrameworkElement element && element.DataContext is OpenTab tab &&
                DataContext is InventoryViewModel vm)
            {
                vm.CloseTabCommand.Execute(tab);
                e.Handled = true;
            }
        }

        private void ItemTabClose_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is OpenTab tab &&
                DataContext is InventoryViewModel vm)
            {
                vm.CloseTabCommand.Execute(tab);
                e.Handled = true;
            }
        }

        private void RelatedItemCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is Item item)
            {
                if (DataContext is InventoryViewModel vm)
                {
                    vm.NavigateToRelatedItemCommand.Execute(item);
                }
            }
        }
        
        private void GalleryThumbnail_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is ItemImage clickedImage)
            {
                if (DataContext is InventoryViewModel vm)
                {
                    vm.SelectGalleryImage(clickedImage);
                }
            }
        }
        
        private void MainImageThumbnail_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is InventoryViewModel vm)
            {
                vm.SelectMainImage();
            }
        }
        
        private void DropZone_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                if (sender is Border border)
                {
                    border.BorderBrush = (Brush)FindResource("PrimaryBrush");
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }
        
        private void DropZone_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is Border border)
            {
                border.BorderBrush = (Brush)FindResource("BorderBrush");
            }
        }
        
        private void ItemDropZone_Drop(object sender, DragEventArgs e)
        {
            if (sender is Border border)
            {
                border.BorderBrush = (Brush)FindResource("BorderBrush");
            }
            
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                ProcessItemImageFiles(files);
            }
        }
        
        private void ItemDropZone_Click(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Button) return;
            
            OpenFileDialog dialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = MyCraftyStash.Services.ImageLoadService.OpenFileFilter
            };
            
            if (dialog.ShowDialog() == true)
            {
                ProcessItemImageFiles(dialog.FileNames);
            }
        }
        
        private void EditItemDropZone_Drop(object sender, DragEventArgs e)
        {
            if (sender is Border border)
            {
                border.BorderBrush = (Brush)FindResource("BorderBrush");
            }
            
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                ProcessItemImageFiles(files);
            }
        }
        
        private void EditItemDropZone_Click(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Button) return;
            
            OpenFileDialog dialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = MyCraftyStash.Services.ImageLoadService.OpenFileFilter
            };
            
            if (dialog.ShowDialog() == true)
            {
                ProcessItemImageFiles(dialog.FileNames);
            }
        }
        
        private void ProcessItemImageFiles(string[] files)
        {
            if (DataContext is InventoryViewModel vm)
            {
                foreach (string file in files)
                {
                    if (IsImageFile(file))
                    {
                        string base64 = ConvertToBase64(file);
                        if (!string.IsNullOrEmpty(base64))
                        {
                            vm.AddNewItemImage(base64);
                        }
                    }
                }
            }
        }
        
        private void RemoveItemImage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string imageUrl)
            {
                if (DataContext is InventoryViewModel vm)
                {
                    vm.RemoveNewItemImage(imageUrl);
                }
            }
            e.Handled = true;
        }

        // Inline X on a selected-theme pill: clear IsSelected on the bound item.
        private void RemoveSelectedTheme_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is MyCraftyStash.Models.ThemeCheckboxItem item)
                item.IsSelected = false;
            e.Handled = true;
        }

        // Inline X on a selected-subtype pill: clear IsChecked on the bound item.
        private void RemoveSelectedSubtype_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is MyCraftyStash.Models.SubtypeCheckboxItem item)
                item.IsChecked = false;
            e.Handled = true;
        }

        // Outside-click close for the inline subtype/theme picker popups.
        // Window-level PreviewMouseDown closes the popup only when the click is
        // outside both the popup and its toggle button — that way the toggle
        // can flip its own IsChecked normally without the popup re-opening on
        // the same press (the StaysOpen=False race).
        private readonly Dictionary<Popup, MouseButtonEventHandler> _chipPickerHandlers = new();

        private void ChipPickerPopup_Opened(object sender, System.EventArgs e)
        {
            if (sender is not Popup popup) return;
            // PlacementTarget is the ToggleButton that drives this popup's IsOpen.
            var toggle = popup.PlacementTarget as DependencyObject;
            var window = Window.GetWindow(this);
            if (window == null) return;

            void Handler(object s, MouseButtonEventArgs e2)
            {
                if (e2.OriginalSource is not DependencyObject src) return;
                if (popup.Child is DependencyObject child && IsInSubtree(src, child)) return;
                if (toggle != null && IsInSubtree(src, toggle)) return;
                if (toggle is ToggleButton tb) tb.IsChecked = false;
                else popup.IsOpen = false;
            }

            _chipPickerHandlers[popup] = Handler;
            window.PreviewMouseDown += Handler;
        }

        private void ChipPickerPopup_Closed(object sender, System.EventArgs e)
        {
            if (sender is not Popup popup) return;
            if (_chipPickerHandlers.TryGetValue(popup, out var handler))
            {
                if (Window.GetWindow(this) is Window w)
                    w.PreviewMouseDown -= handler;
                _chipPickerHandlers.Remove(popup);
            }
        }

        private static bool IsInSubtree(DependencyObject node, DependencyObject root)
        {
            for (var d = node; d != null; d = VisualTreeHelper.GetParent(d) ?? LogicalTreeHelper.GetParent(d))
                if (d == root) return true;
            return false;
        }
        
        private bool IsImageFile(string path) =>
            MyCraftyStash.Services.ImageLoadService.IsSupported(path);
        
        private string ConvertToBase64(string filePath)
        {
            try
            {
                return MyCraftyStash.Services.ImageLoadService.LoadAsDataUri(filePath);
            }
            catch
            {
                return string.Empty;
            }
        }

        private async void DeleteSentimentSnip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is int sentimentId)
        {
            var result = System.Windows.MessageBox.Show(
                "Remove this sentiment snip?",
                "Remove Sentiment",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                try
                {
                    var sentimentService = new MyCraftyStash.Services.SentimentService();
                    await sentimentService.DeleteSentimentImageAsync(sentimentId);

                    // Reload via the ViewModel
                    if (DataContext is MyCraftyStash.ViewModels.InventoryViewModel vm && vm.SelectedItem != null)
                    {
                        var item = vm.SentimentImages.FirstOrDefault(s => s.Id == sentimentId);
                        if (item != null)
                            vm.SentimentImages.Remove(item);
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error removing sentiment: {ex.Message}");
                }
            }
        }
    }

    private async void DeleteSentimentLine_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement el || el.Tag is not string sentLine) return;

        var result = System.Windows.MessageBox.Show(
            $"Are you sure you want to remove this sentiment?\n\n\"{sentLine}\"\n\nThis will update the sentiments text for this item.",
            "Remove Sentiment",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning,
            System.Windows.MessageBoxResult.No);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        if (DataContext is not MyCraftyStash.ViewModels.InventoryViewModel vm || vm.SelectedItem == null) return;

        // Quote-aware: chips with commas/newlines are preserved as one entry.
        var updatedLines = MyCraftyStash.Services.SentimentService
            .ParseSentimentLines(vm.SelectedItem.Sentiments ?? string.Empty)
            .Where(l => !string.Equals(l, sentLine, StringComparison.OrdinalIgnoreCase))
            .ToList();

        vm.SelectedItem.Sentiments = updatedLines.Count > 0
            ? MyCraftyStash.Services.SentimentService.SerializeSentimentLines(updatedLines)
            : null;

        try
        {
            await vm.SaveSentimentTextAsync();
            vm.SentimentLines.Remove(sentLine);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Could not save the change: {ex.Message}",
                "Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

        private void EditSentimentsButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not InventoryViewModel vm || vm.SelectedItem == null) return;
            vm.EditingSentimentsText = vm.SelectedItem.Sentiments ?? string.Empty;
            vm.IsEditingSentiments = true;
        }

        private void CancelEditSentiments_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not InventoryViewModel vm) return;
            vm.IsEditingSentiments = false;
        }

        private async void SaveEditSentiments_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not InventoryViewModel vm || vm.SelectedItem == null) return;

            var raw = vm.EditingSentimentsText ?? string.Empty;
            var lines = MyCraftyStash.Services.SentimentService.ParseSentimentLines(raw);

            vm.SelectedItem.Sentiments = lines.Count > 0
                ? MyCraftyStash.Services.SentimentService.SerializeSentimentLines(lines)
                : null;

            try
            {
                await vm.SaveSentimentTextAsync();
                vm.SentimentLines = new System.Collections.ObjectModel.ObservableCollection<string>(lines);
                vm.IsEditingSentiments = false;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Could not save sentiments: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
    }
}
