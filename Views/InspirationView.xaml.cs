using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MyCraftyStash.ViewModels;

namespace MyCraftyStash.Views
{
    public partial class InspirationView : UserControl
    {
        public InspirationView()
        {
            InitializeComponent();
        }

        // ── Image card ────────────────────────────────────────────────────────

        private void ImageCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is InspirationEntry entry)
            {
                if (DataContext is InspirationViewModel vm)
                {
                    if (vm.IsOrganizing)
                        vm.ToggleOrgImageSelectionCommand.Execute(entry);
                    else
                        vm.SelectImageCommand.Execute(entry);
                }
            }
        }

        // ── Image card drag initiation ────────────────────────────────────────

        private Point _dragStartPoint;
        private InspirationEntry? _dragEntry;

        private void ImageCard_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            if (sender is Border border && border.Tag is InspirationEntry entry)
                _dragEntry = entry;
        }

        private void ImageCard_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _dragEntry == null) return;

            // Don't drag in org mode - that uses click-to-select
            if (DataContext is InspirationViewModel vm && vm.IsOrganizing) return;

            var pos = e.GetPosition(null);
            var diff = _dragStartPoint - pos;
            if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance) return;

            var dragData = new DataObject("InspirationEntry", _dragEntry);
            DragDrop.DoDragDrop((DependencyObject)sender, dragData, DragDropEffects.Move);
            _dragEntry = null;
        }

        // ── Board card ────────────────────────────────────────────────────────

        private void BoardCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border border || border.Tag is not BoardEntry board) return;

            // Don't navigate when the edit/delete buttons were clicked
            if (IsClickInsideButton(e.OriginalSource as DependencyObject, border)) return;

            if (DataContext is InspirationViewModel vm)
                vm.NavigateToBoardCommand.Execute(board.Id);
        }

        // ── Board card drag-drop target ───────────────────────────────────────

        private void BoardCard_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("InspirationEntry"))
            {
                e.Effects = DragDropEffects.Move;
                if (sender is Border border)
                    border.BorderBrush = new SolidColorBrush(Color.FromRgb(0xD6, 0x1F, 0x26));
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void BoardCard_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is Border border)
                border.ClearValue(Border.BorderBrushProperty);
        }

        private void BoardCard_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent("InspirationEntry")
                ? DragDropEffects.Move
                : DragDropEffects.None;
            e.Handled = true;
        }

        private async void BoardCard_Drop(object sender, DragEventArgs e)
        {
            if (sender is Border border)
                border.ClearValue(Border.BorderBrushProperty);

            if (!e.Data.GetDataPresent("InspirationEntry")) return;
            if (e.Data.GetData("InspirationEntry") is not InspirationEntry entry) return;
            if (sender is not Border b || b.Tag is not BoardEntry boardEntry) return;

            if (DataContext is InspirationViewModel vm)
                await vm.DragMoveImageToBoardAsync(entry.Id, boardEntry.Id);

            e.Handled = true;
        }

        // ── Breadcrumb ────────────────────────────────────────────────────────

        private void BreadcrumbItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock tb && DataContext is InspirationViewModel vm)
            {
                int? boardId = tb.Tag is int id ? id : (int?)null;
                vm.NavigateToBoardCommand.Execute(boardId);
            }
        }

        // ── Move to board ─────────────────────────────────────────────────────

        private void MoveToNoBoard_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is InspirationViewModel vm)
                vm.MoveImageToBoardCommand.Execute(null);
        }

        private void MoveToBoard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is int boardId)
            {
                if (DataContext is InspirationViewModel vm)
                    vm.MoveImageToBoardCommand.Execute(boardId);
            }
        }

        // ── Image upload ──────────────────────────────────────────────────────

        private void ImageUpload_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is InspirationViewModel vm)
                vm.BrowseImageCommand.Execute(null);
        }

        private void InspirationDropZone_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                var valid = files?.Any(f => MyCraftyStash.Services.ImageLoadService.IsSupported(f)) ?? false;

                e.Effects = valid ? DragDropEffects.Copy : DragDropEffects.None;

                if (sender is Border border && valid)
                    border.BorderBrush = new SolidColorBrush(Color.FromRgb(0x93, 0x33, 0xEA));
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void InspirationDropZone_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is Border border)
                border.ClearValue(Border.BorderBrushProperty);
        }

        private void InspirationDropZone_Drop(object sender, DragEventArgs e)
        {
            if (sender is Border border)
                border.ClearValue(Border.BorderBrushProperty);

            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0) return;

            var imageFile = files.FirstOrDefault(f => MyCraftyStash.Services.ImageLoadService.IsSupported(f));

            if (imageFile != null && DataContext is InspirationViewModel vm)
                vm.LoadImageFromPath(imageFile);

            e.Handled = true;
        }

        // ── Linked items ──────────────────────────────────────────────────────

        private void LinkedItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is int itemId)
            {
                if (DataContext is InspirationViewModel vm)
                    _ = vm.NavigateToItem(itemId);
            }
        }

        private void AddSelectableItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is InspirationSelectableItem item)
                item.IsSelected = !item.IsSelected;
        }

        private void AddSelectedTag_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is InspirationSelectableItem item)
                item.IsSelected = false;
        }

        private void EditSelectableItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is InspirationSelectableItem item)
                item.IsSelected = !item.IsSelected;
        }

        // ── Image detail popup ────────────────────────────────────────────────

        private void ImageDetailOverlay_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is InspirationViewModel vm)
                vm.CloseImageDetailPopupCommand.Execute(null);
        }

        private void ImageDetailCard_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true; // Prevent click from bubbling to the overlay
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Returns true if the click originated inside a Button within the given container.
        /// Used to prevent board card navigation when edit/delete buttons are clicked.
        /// </summary>
        private static bool IsClickInsideButton(DependencyObject? source, DependencyObject container)
        {
            var el = source;
            while (el != null && el != container)
            {
                if (el is Button) return true;
                el = VisualTreeHelper.GetParent(el);
            }
            return false;
        }
    }
}
