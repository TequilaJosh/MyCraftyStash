using System.Collections.Generic;
using System.Linq;
using System.Windows;
using MyCraftyStash.Models;

namespace MyCraftyStash.Views
{
    public partial class ShareProjectDialog : Window
    {
        public class Recipient
        {
            public string DisplayName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
        }

        public string? SelectedEmail { get; private set; }
        public bool OpenInExplorer => OpenInExplorerCheck.IsChecked == true;

        /// <summary>True when user picked Save &amp; Email; false on Save Only.</summary>
        public bool ShouldSendEmail { get; private set; }

        public ShareProjectDialog(IEnumerable<AddressBookEntry> contacts)
        {
            InitializeComponent();
            var withEmail = contacts
                .Where(c => !string.IsNullOrWhiteSpace(c.Email))
                .Select(c => new Recipient
                {
                    DisplayName = (string.IsNullOrWhiteSpace(c.FirstName) && string.IsNullOrWhiteSpace(c.LastName))
                        ? c.Email!
                        : $"{c.FirstName} {c.LastName}".Trim(),
                    Email = c.Email!,
                })
                .OrderBy(r => r.DisplayName)
                .ToList();

            ContactsList.ItemsSource = withEmail;
            if (withEmail.Count == 0)
            {
                ContactsList.Visibility = Visibility.Collapsed;
                EmptyHint.Visibility = Visibility.Visible;
                SendBtn.IsEnabled = false;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void SaveOnly_Click(object sender, RoutedEventArgs e)
        {
            ShouldSendEmail = false;
            DialogResult = true;
            Close();
        }

        private void Send_Click(object sender, RoutedEventArgs e)
        {
            if (ContactsList.SelectedItem is Recipient r)
            {
                SelectedEmail = r.Email;
                ShouldSendEmail = true;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show(this,
                    "Select a contact to email, or click Save Only to skip the email step.",
                    "No contact selected", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
