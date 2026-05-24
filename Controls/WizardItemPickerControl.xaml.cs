using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using JandH.Core.Services;
using JandH.Core.ViewModels;
using MyCraftyStash.Services;
using JandH.Core.Services;
using JandH.Core.ViewModels;
using MyCraftyStash.ViewModels;

using JandH.Core.Models;

namespace MyCraftyStash.Controls
{
    /// <summary>
    /// Drop-in reusable wizard item picker. Pair with a WizardItemPicker DataContext.
    /// Shows a HubButton-styled toggle that opens a popup containing:
    ///   • A WrapPanel chip strip of subtypes (auto-hidden when there's only "All").
    ///   • A search text box.
    ///   • A scrollable list of items (image + name, click anywhere to select).
    /// Set <see cref="PlaceholderText"/> via XAML; the rest is bound through the
    /// WizardItemPicker assigned as DataContext.
    /// </summary>
    public partial class WizardItemPickerControl : UserControl
    {
        public static readonly DependencyProperty PlaceholderTextProperty =
            DependencyProperty.Register(nameof(PlaceholderText), typeof(string), typeof(WizardItemPickerControl),
                new PropertyMetadata("Select..."));

        public string PlaceholderText
        {
            get => (string)GetValue(PlaceholderTextProperty);
            set => SetValue(PlaceholderTextProperty, value);
        }

        public ICommand SelectItemCommand { get; }

        public WizardItemPickerControl()
        {
            InitializeComponent();
            SelectItemCommand = new RelayActionCommand(o =>
            {
                if (DataContext is WizardItemPicker picker && o is WizardItemOption item)
                {
                    if (picker.IsMultiSelect)
                    {
                        // Multi-select: toggle the item in/out of SelectedItems and
                        // leave the popup open so the user can pick more.
                        picker.ToggleSelected(item);
                        return;
                    }

                    picker.SelectedItem = item;
                }
                // Close THIS instance's popup directly — VM.IsOpen is shared across
                // controls so we can't route through it (see XAML comment on OpenToggle).
                OpenToggle.IsChecked = false;
            });
        }

        // Tiny self-contained ICommand so we don't need to wire CommunityToolkit RelayCommand
        // generation inside a UserControl.
        private sealed class RelayActionCommand : ICommand
        {
            private readonly System.Action<object?> _exec;
            public RelayActionCommand(System.Action<object?> exec) { _exec = exec; }
            public bool CanExecute(object? parameter) => true;
            public void Execute(object? parameter) => _exec(parameter);
            public event System.EventHandler? CanExecuteChanged { add { } remove { } }
        }

        // ── Outside-click close ──────────────────────────────────────────────
        // Two failure modes the previous Mouse.Capture+StaysOpen=False pattern
        // hit: (1) the inner search TextBox stole focus and StaysOpen lost its
        // wired-up close behavior; (2) clicking the toggle to "close" it raced
        // with StaysOpen=False — outside-click closed the popup, then ClickMode
        // =Press on the toggle flipped IsChecked back to true and re-opened it.
        // This Window-level PreviewMouseDown handler closes the popup only when
        // the click is outside BOTH the popup subtree AND the toggle subtree;
        // toggle-clicks pass through and the toggle's own ClickMode=Press handles
        // the close via the IsChecked<->IsOpen TwoWay binding.
        private void PickerPopup_Opened(object sender, System.EventArgs e)
        {
            if (Window.GetWindow(this) is Window w)
            {
                w.PreviewMouseDown += OnWindowPreviewMouseDown;
                // Wheel events outside the popup dismiss it, so the user can
                // scroll the underlying wizard page without first closing the
                // popup manually.
                w.PreviewMouseWheel += OnWindowPreviewMouseWheel;
            }
        }

        private void PickerPopup_Closed(object sender, System.EventArgs e)
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
            if (PickerPopup.Child is DependencyObject child && IsInSubtree(src, child)) return; // click inside popup
            if (IsInSubtree(src, OpenToggle)) return;                                            // click on toggle (let it toggle naturally)
            OpenToggle.IsChecked = false;                                                        // outside both → close
        }

        private void OnWindowPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject src) return;
            if (PickerPopup.Child is DependencyObject child && IsInSubtree(src, child)) return;
            if (IsInSubtree(src, OpenToggle)) return;
            OpenToggle.IsChecked = false;
        }

        // Multi-select footer: clear wipes SelectedItems on the picker VM,
        // confirm just closes the popup (selections are already toggled in
        // SelectItemCommand).
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is WizardItemPicker picker) picker.SelectedItems.Clear();
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
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
