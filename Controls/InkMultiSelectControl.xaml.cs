using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using MyCraftyStash.ViewModels;

namespace MyCraftyStash.Controls
{
    /// <summary>
    /// Reusable multi-select ink dropdown. DataContext = <see cref="InkSelection"/>.
    /// Looks like a HubButton when closed; popup shows scrollable image+name rows
    /// with checkbox state, an optional "Was this embossed?" chip in the chip strip
    /// at the top (toggle <see cref="ShowEmbossedChip"/>), and a "Confirm Inks"
    /// button at the bottom that closes the popup. Click order is preserved on the
    /// closed face and inside the popup's "In order" preview.
    /// </summary>
    public partial class InkMultiSelectControl : UserControl
    {
        public static readonly DependencyProperty PlaceholderTextProperty =
            DependencyProperty.Register(nameof(PlaceholderText), typeof(string), typeof(InkMultiSelectControl),
                new PropertyMetadata("Pick inks..."));

        public static readonly DependencyProperty ShowEmbossedChipProperty =
            DependencyProperty.Register(nameof(ShowEmbossedChip), typeof(bool), typeof(InkMultiSelectControl),
                new PropertyMetadata(false));

        public string PlaceholderText
        {
            get => (string)GetValue(PlaceholderTextProperty);
            set => SetValue(PlaceholderTextProperty, value);
        }

        public bool ShowEmbossedChip
        {
            get => (bool)GetValue(ShowEmbossedChipProperty);
            set => SetValue(ShowEmbossedChipProperty, value);
        }

        public InkMultiSelectControl()
        {
            InitializeComponent();
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is InkSelection sel) sel.Clear();
        }

        // Window-level outside-click close. Same pattern as WizardItemPickerControl
        // — closes only when click is outside both popup AND toggle, so the toggle
        // can flip its own IsChecked via ClickMode=Press without the popup
        // re-opening on the same press.
        private void InkPopup_Opened(object sender, System.EventArgs e)
        {
            if (Window.GetWindow(this) is Window w)
            {
                w.PreviewMouseDown += OnWindowPreviewMouseDown;
                // Wheel events outside the popup close it, otherwise the popup
                // floats over the wizard scroll area and the user can't scroll
                // the underlying page until they manually dismiss it.
                w.PreviewMouseWheel += OnWindowPreviewMouseWheel;
            }
        }

        private void InkPopup_Closed(object sender, System.EventArgs e)
        {
            if (Window.GetWindow(this) is Window w)
            {
                w.PreviewMouseDown -= OnWindowPreviewMouseDown;
                w.PreviewMouseWheel -= OnWindowPreviewMouseWheel;
            }
        }

        private void OnWindowPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject src) return;
            if (InkPopup.Child is DependencyObject child && IsInSubtree(src, child)) return;
            if (IsInSubtree(src, OpenToggle)) return;
            OpenToggle.IsChecked = false;
        }

        private void OnWindowPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject src) return;
            // If the wheel event happens inside the popup, let the popup's own
            // ScrollViewer handle it. Outside → dismiss the popup so the wizard
            // page can scroll normally.
            if (InkPopup.Child is DependencyObject child && IsInSubtree(src, child)) return;
            if (IsInSubtree(src, OpenToggle)) return;
            OpenToggle.IsChecked = false;
        }

        private static bool IsInSubtree(DependencyObject node, DependencyObject root)
        {
            for (var d = node; d != null; d = VisualTreeHelper.GetParent(d) ?? LogicalTreeHelper.GetParent(d))
                if (d == root) return true;
            return false;
        }
    }
}
