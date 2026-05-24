using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using JandH.Core.Services;
using MyCraftyStash.Services;

using JandH.Core.Models;
using JandH.Core.ViewModels;

namespace MyCraftyStash.Views
{
    /// <summary>
    /// Themed "what's new" popup. Shown once per upgrade — see App.OnStartup
    /// where it's gated by UpdateSettings.LastNotesShownVersion.
    /// </summary>
    public partial class ReleaseNotesDialog : Window
    {
        public ReleaseNotesDialog(IEnumerable<ReleaseNotesEntry> entries, System.Version newVersion)
        {
            InitializeComponent();

            var list = entries
                .Select(e => new EntryViewModel
                {
                    VersionText = e.Version.ToString(),
                    Bullets = e.Bullets,
                })
                .ToList();
            EntriesList.ItemsSource = list;

            SubtitleText.Text = list.Count switch
            {
                0 => $"Updated to version {newVersion}",
                1 => $"Updated to version {newVersion}",
                _ => $"Updated to version {newVersion} (catching you up on {list.Count} releases)"
            };

            // Drag-anywhere by holding the left mouse button. Window has no chrome
            // because of WindowStyle=None / AllowsTransparency=true.
            MouseLeftButtonDown += (_, _) => { try { DragMove(); } catch { } };
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private sealed class EntryViewModel
        {
            public string VersionText { get; set; } = string.Empty;
            public List<string> Bullets { get; set; } = new();
        }
    }
}
