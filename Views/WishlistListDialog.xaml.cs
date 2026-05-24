using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using JandH.Core.Models;
using JandH.Core.Services;
using MyCraftyStash.Models;
using JandH.Core.Models;
using JandH.Core.Services;
using MyCraftyStash.Services;

using JandH.Core.ViewModels;

namespace MyCraftyStash.Views
{
    public partial class WishlistListDialog : Window
    {
        public Wishlist Result { get; private set; } = new();

        private string _selectedColor = WishlistColorPalette.Default;
        private readonly bool _isEdit;

        public WishlistListDialog(Wishlist? existing = null)
        {
            InitializeComponent();
            _isEdit = existing != null;

            SwatchList.ItemsSource = WishlistColorPalette.Colors;

            if (existing != null)
            {
                Title          = "Edit list";
                HeaderText.Text = "Edit list";
                NameBox.Text   = existing.Name;
                DescriptionBox.Text = existing.Description ?? string.Empty;
                _selectedColor = WishlistColorPalette.Normalize(existing.Color);
                Result = new Wishlist
                {
                    Id          = existing.Id,
                    Name        = existing.Name,
                    Color       = existing.Color,
                    Description = existing.Description,
                    CreatedAt   = existing.CreatedAt,
                };
            }

            Loaded += (_, __) =>
            {
                ApplySwatchSelection();
                NameBox.Focus();
                NameBox.SelectAll();
            };
        }

        private void Swatch_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border b && b.Tag is string hex)
            {
                _selectedColor = hex;
                ApplySwatchSelection();
            }
        }

        private void ApplySwatchSelection()
        {
            for (int i = 0; i < SwatchList.Items.Count; i++)
            {
                if (SwatchList.ItemContainerGenerator.ContainerFromIndex(i) is ContentPresenter cp)
                {
                    cp.ApplyTemplate();
                    if (FindBorder(cp) is Border border)
                    {
                        var hex = border.Tag as string ?? string.Empty;
                        var isSelected = string.Equals(hex, _selectedColor, System.StringComparison.OrdinalIgnoreCase);
                        border.BorderBrush = isSelected
                            ? new SolidColorBrush(Color.FromRgb(0x1E, 0x1B, 0x17))
                            : Brushes.Transparent;
                    }
                }
            }
        }

        private static Border? FindBorder(DependencyObject root)
        {
            if (root is Border b) return b;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                var found = FindBorder(child);
                if (found != null) return found;
            }
            return null;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var name = NameBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(name))
            {
                ErrorText.Text = "Name is required.";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            Result.Name        = name;
            Result.Color       = _selectedColor;
            Result.Description = string.IsNullOrWhiteSpace(DescriptionBox.Text) ? null : DescriptionBox.Text.Trim();
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
