using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace MyCraftyStash.Views
{
    /// <summary>
    /// Modal shown by the Wishlist "Move to inventory" flow when the wishlist
    /// item's type doesn't match any entry in the inventory's canonical type
    /// list. The user picks the type to file the new item under.
    /// </summary>
    public partial class TypePickerDialog : Window
    {
        public string? SelectedType { get; private set; }

        public TypePickerDialog(
            string itemName,
            string? originalType,
            IEnumerable<string> availableTypes,
            string? suggestedType = null)
        {
            InitializeComponent();

            ItemNameText.Text = string.IsNullOrWhiteSpace(itemName) ? "(unnamed item)" : itemName;

            if (string.IsNullOrWhiteSpace(originalType))
            {
                OriginalTypeText.Text = "No type was set on the wishlist item.";
                SubtitleText.Text = "This wishlist item doesn't have a type yet — pick one for the new inventory item.";
            }
            else
            {
                OriginalTypeText.Text = $"Wishlist type was “{originalType}” — no exact inventory match.";
                SubtitleText.Text = "Pick the inventory type that best fits this item.";
            }

            var types = availableTypes?.ToList() ?? new List<string>();
            TypeCombo.ItemsSource = types;

            if (!string.IsNullOrWhiteSpace(suggestedType) && types.Contains(suggestedType))
                TypeCombo.SelectedItem = suggestedType;
            else if (types.Count > 0)
                TypeCombo.SelectedIndex = 0;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            SelectedType = TypeCombo.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(SelectedType)) return;
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
