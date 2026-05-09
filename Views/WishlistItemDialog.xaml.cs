using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using MyCraftyStash.Models;
using MyCraftyStash.Services;

namespace MyCraftyStash.Views
{
    public partial class WishlistItemDialog : Window
    {
        public WishlistItem Result { get; private set; } = new();

        public WishlistItemDialog(WishlistItem? existing = null)
        {
            InitializeComponent();

            if (existing != null)
            {
                Title          = "Edit item";
                HeaderText.Text = "Edit item";
                NameBox.Text       = existing.Name;
                TypeBox.Text       = existing.Type ?? string.Empty;
                ItemNumberBox.Text = existing.ItemNumber ?? string.Empty;
                PriceBox.Text      = existing.Price?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty;
                StoreBox.Text      = existing.PurchasedFrom ?? string.Empty;
                UrlBox.Text        = existing.Url ?? string.Empty;
                ThemeBox.Text      = existing.Theme ?? string.Empty;
                NotesBox.Text      = existing.Notes ?? string.Empty;
                SelectPriority(existing.Priority);
                Result = new WishlistItem
                {
                    Id         = existing.Id,
                    WishlistId = existing.WishlistId,
                    CreatedAt  = existing.CreatedAt,
                    ImageUrl   = existing.ImageUrl,
                };
            }
            else
            {
                SelectPriority(2);
            }

            Loaded += (_, __) => { NameBox.Focus(); NameBox.SelectAll(); };
        }

        private void SelectPriority(int p)
        {
            PriorityBox.SelectedIndex = p switch { 3 => 2, 2 => 1, _ => 0 };
        }

        private int ReadPriority()
        {
            if (PriorityBox.SelectedItem is ComboBoxItem cbi && int.TryParse(cbi.Tag?.ToString(), out var v))
                return v;
            return 1;
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

            decimal? price = null;
            if (!string.IsNullOrWhiteSpace(PriceBox.Text))
            {
                if (decimal.TryParse(PriceBox.Text.Trim().TrimStart('$'),
                        NumberStyles.Number, CultureInfo.CurrentCulture, out var p) ||
                    decimal.TryParse(PriceBox.Text.Trim().TrimStart('$'),
                        NumberStyles.Number, CultureInfo.InvariantCulture, out p))
                {
                    price = p;
                }
                else
                {
                    ErrorText.Text = "Price must be a number.";
                    ErrorText.Visibility = Visibility.Visible;
                    return;
                }
            }

            Result.Name          = name;
            Result.Type          = NullIfEmpty(TypeBox.Text);
            Result.ItemNumber    = NullIfEmpty(ItemNumberBox.Text);
            Result.Price         = price;
            Result.PurchasedFrom = NullIfEmpty(StoreBox.Text);
            Result.Url           = NullIfEmpty(UrlBox.Text);
            Result.Theme         = NullIfEmpty(ThemeBox.Text);
            Result.Notes         = NullIfEmpty(NotesBox.Text);
            Result.Priority      = ReadPriority();

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void LookUp_Click(object sender, RoutedEventArgs e)
        {
            if (!CatalogLookupService.IsAvailable)
            {
                ErrorText.Text = "Catalog lookup is not configured.";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            var dialog = new ItemLookupDialog { Owner = this };
            if (dialog.ShowDialog() == true && dialog.SelectedResult is { } r)
            {
                NameBox.Text       = r.Name;
                ItemNumberBox.Text = r.ItemNumber ?? string.Empty;
                PriceBox.Text      = r.Price?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty;
                TypeBox.Text       = r.Type ?? string.Empty;
                ThemeBox.Text      = r.Theme ?? string.Empty;
                StoreBox.Text      = "Taylored Expressions";
            }
        }

        private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}
