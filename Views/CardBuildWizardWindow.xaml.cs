using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using MyCraftyStash.Services;
using MyCraftyStash.ViewModels;

namespace MyCraftyStash.Views
{
    public partial class CardBuildWizardWindow : Window
    {
        public CardBuildWizardWindow(InventoryService service) : this(service, null, null, null) { }
        public CardBuildWizardWindow(InventoryService service, string? projectImageBase64) : this(service, projectImageBase64, null, null) { }
        public CardBuildWizardWindow(InventoryService service, string? projectImageBase64, Action<string>? onImageAddedToProject)
            : this(service, projectImageBase64, onImageAddedToProject, null) { }

        public CardBuildWizardWindow(InventoryService service, string? projectImageBase64, Action<string>? onImageAddedToProject, string? existingSnapshotJson)
        {
            InitializeComponent();
            var vm = new CardBuildWizardViewModel(service, projectImageBase64, onImageAddedToProject);
            vm.CloseRequested += (_, _) =>
            {
                DialogResult = vm.WasConfirmed;
                Close();
            };
            DataContext = vm;
            Loaded += async (_, _) =>
            {
                await vm.InitializeAsync();
                // Hydrate after init so the per-bucket cardstock collections etc.
                // are populated and our SelectedItem lookups can resolve by Id.
                if (!string.IsNullOrWhiteSpace(existingSnapshotJson))
                    vm.LoadFromSnapshotJson(existingSnapshotJson);
            };
        }

        public CardBuildWizardViewModel WizardVM => (CardBuildWizardViewModel)DataContext;

        // ── Code-behind event glue ───────────────────────────────────────────
        // Only handlers still bound from XAML are kept here; the rest were
        // removed alongside the legacy in-line forms that referenced them.

        private void SentimentSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is CardBuildWizardViewModel vm)
                _ = vm.SearchSentimentsCommand.ExecuteAsync(null);
        }

        // Wheel-scroll rescue. Two problems compounded:
        //  1. Nested ToggleButton-style pickers mark wheel events as Handled on
        //     bubble, so the parent ScrollViewer's built-in wheel handler never
        //     sees the event.
        //  2. The Details sub-page has BOTH an outer ScrollViewer (this one) and
        //     an inner one (Selection Details, Grid.Row=3). When the user adds
        //     enough follow-up content, it's the INNER one that overflows — so a
        //     blind "scroll the sender" handler does nothing.
        // Fix: from the element under the cursor, walk up the visual tree and
        // scroll the FIRST ScrollViewer that actually has overflow. Skip when a
        // ComboBox / ListBox / Popup owns its own wheel behavior.
        private void FollowupsScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled) return;

            // Popup contents live in a separate PresentationSource (PopupRoot's
            // own HwndSource). The picker's inner ScrollViewer uses item-mode
            // scrolling (CanContentScroll=True), so feeding it a 120-pixel
            // delta scrolls 120 *items* — i.e. jumps top-to-bottom in one tick.
            // Let popup-hosted scrollers handle their own wheel events.
            if (e.OriginalSource is Visual ov)
            {
                var src = PresentationSource.FromVisual(ov);
                if (src != null && src != PresentationSource.FromVisual(this))
                    return;
            }

            ScrollViewer? target = null;
            for (var d = e.OriginalSource as DependencyObject; d != null; d = ParentOf(d))
            {
                if (d is ComboBox || d is ListBox || d is Popup) return;
                if (d is ScrollViewer sv && sv.ScrollableHeight > 0)
                {
                    target = sv;
                    break;
                }
            }
            target ??= sender as ScrollViewer;
            if (target == null) return;

            target.ScrollToVerticalOffset(target.VerticalOffset - e.Delta);
            e.Handled = true;
        }

        private static DependencyObject? ParentOf(DependencyObject d)
        {
            // Visual parent first (handles popup-hosted templates), then logical
            // (handles ContentPresenter / TextBlock-style runs).
            if (d is Visual || d is System.Windows.Media.Media3D.Visual3D)
            {
                var v = VisualTreeHelper.GetParent(d);
                if (v != null) return v;
            }
            return LogicalTreeHelper.GetParent(d);
        }

        // Outside-click close for the Inks/Watercolors picker popup. Window-level
        // PreviewMouseDown — closes only when the click lands outside both the
        // popup and its toggle, so the toggle's ClickMode=Press can flip
        // IsChecked for itself without the popup re-opening on the same press.
        private void InksWatercolorsPopup_Opened(object sender, System.EventArgs e)
        {
            PreviewMouseDown += OnInksWatercolorsWindowPreviewMouseDown;
        }

        private void InksWatercolorsPopup_Closed(object sender, System.EventArgs e)
        {
            PreviewMouseDown -= OnInksWatercolorsWindowPreviewMouseDown;
        }

        private void OnInksWatercolorsWindowPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject src) return;
            if (InksWatercolorsPopup.Child is DependencyObject child && IsInPopupSubtree(src, child)) return;
            if (IsInPopupSubtree(src, InksWatercolorsToggle)) return;
            InksWatercolorsToggle.IsChecked = false;
        }

        private static bool IsInPopupSubtree(DependencyObject node, DependencyObject root)
        {
            for (var d = node; d != null; d = VisualTreeHelper.GetParent(d) ?? LogicalTreeHelper.GetParent(d))
                if (d == root) return true;
            return false;
        }
    }
}
