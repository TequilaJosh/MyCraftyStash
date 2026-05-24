using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using JandH.Core.Models;
using JandH.Core.Services;
using MyCraftyStash.Models;
using JandH.Core.Models;
using JandH.Core.Services;
using MyCraftyStash.Services;

using JandH.Core.ViewModels;

namespace MyCraftyStash.Views
{
    /// <summary>Sentinel object used for the "Create new list..." ComboBox entry.</summary>
    public class CreateNewWishlistOption
    {
        public string Name => "- Create new list -";
        public override string ToString() => Name;
    }

    public partial class TeWishlistImportDialog : Window, INotifyPropertyChanged
    {
        private readonly TeWishlistImportService _importService;

        // ── Bindable properties ───────────────────────────────────────────────

        private string _wishlistUrl = string.Empty;
        public string WishlistUrl
        {
            get => _wishlistUrl;
            set { _wishlistUrl = value; Notify(); }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            private set { _isLoading = value; Notify(); Notify(nameof(ShowPrompt)); }
        }

        private string _errorMessage = string.Empty;
        public string ErrorMessage
        {
            get => _errorMessage;
            private set { _errorMessage = value; Notify(); Notify(nameof(ShowPrompt)); }
        }

        public bool ShowPrompt => !IsLoading && Items.Count == 0 && string.IsNullOrEmpty(ErrorMessage);
        public bool HasItems   => Items.Count > 0;
        public bool CanImport  => Items.Any(i => i.Selected) && (!ShowNewListInput || !string.IsNullOrWhiteSpace(NewListName));
        public int  SelectedCount => Items.Count(i => i.Selected);

        public bool AllSelected
        {
            get => Items.Count > 0 && Items.All(i => i.Selected);
            set
            {
                foreach (var item in Items) item.Selected = value;
                NotifySelectionChanged();
            }
        }

        // Wishlist destination
        private object? _selectedWishlistOption;
        public object? SelectedWishlistOption
        {
            get => _selectedWishlistOption;
            set { _selectedWishlistOption = value; Notify(); Notify(nameof(ShowNewListInput)); Notify(nameof(CanImport)); }
        }

        public bool ShowNewListInput => SelectedWishlistOption is CreateNewWishlistOption;

        private string _newListName = string.Empty;
        public string NewListName
        {
            get => _newListName;
            set { _newListName = value; Notify(); Notify(nameof(CanImport)); }
        }

        /// <summary>ComboBox options: null entry (no list) + existing lists + create-new sentinel.</summary>
        public ObservableCollection<object> WishlistOptions { get; } = new();

        public ObservableCollection<TeWishlistItem> Items { get; } = new();

        /// <summary>Items the user confirmed; populated when DialogResult = true.</summary>
        public List<TeWishlistItem> ImportedItems { get; private set; } = new();

        /// <summary>
        /// If non-null: import to this existing wishlist.
        /// If null and NewListName is non-empty: caller should create a new wishlist with that name.
        /// If both null/empty: import with no list assigned.
        /// </summary>
        public int? ChosenWishlistId { get; private set; }

        /// <summary>Non-empty when the user wants to create a new wishlist on import.</summary>
        public string ChosenNewListName => ShowNewListInput ? NewListName.Trim() : string.Empty;

        // ── Constructor ───────────────────────────────────────────────────────

        public TeWishlistImportDialog(TeWishlistImportService importService,
                                      IEnumerable<Wishlist>? existingWishlists = null)
        {
            InitializeComponent();
            DataContext    = this;
            _importService = importService;
            Loaded        += (_, _) => UrlBox.Focus();

            // Build the wishlist options
            WishlistOptions.Add(new Wishlist { Id = 0, Name = "- No list (general) -" });
            if (existingWishlists != null)
                foreach (var wl in existingWishlists)
                    WishlistOptions.Add(wl);
            WishlistOptions.Add(new CreateNewWishlistOption());
            SelectedWishlistOption = WishlistOptions[0];

            Items.CollectionChanged += (_, _) =>
            {
                Notify(nameof(HasItems));
                Notify(nameof(ShowPrompt));
                Notify(nameof(CanImport));
                Notify(nameof(SelectedCount));
                Notify(nameof(AllSelected));
            };
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void UrlBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) _ = RunFetchAsync();
        }

        private void FetchButton_Click(object sender, RoutedEventArgs e) => _ = RunFetchAsync();

        private void WishlistCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Notify(nameof(ShowNewListInput));
            Notify(nameof(CanImport));
            if (ShowNewListInput)
                NewListNameBox.Focus();
        }

        private async Task RunFetchAsync()
        {
            if (string.IsNullOrWhiteSpace(WishlistUrl)) return;

            IsLoading    = true;
            ErrorMessage = string.Empty;
            Items.Clear();

            try
            {
                var fetched = await _importService.FetchWishlistAsync(WishlistUrl.Trim());
                foreach (var item in fetched.Items)
                    Items.Add(item);

                // If TE gave us a wishlist title, default the import target to
                // "Create new list" pre-filled with that name. The user can still
                // pick an existing list from the combo if they'd rather merge.
                if (!string.IsNullOrWhiteSpace(fetched.Title))
                {
                    var createOption = WishlistOptions.OfType<CreateNewWishlistOption>().FirstOrDefault();
                    if (createOption != null)
                    {
                        SelectedWishlistOption = createOption;
                        NewListName = fetched.Title!;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsLoading = false;
                NotifySelectionChanged();
            }
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            ImportedItems = Items.Where(i => i.Selected).ToList();

            // Resolve chosen wishlist
            if (SelectedWishlistOption is Wishlist wl && wl.Id != 0)
                ChosenWishlistId = wl.Id;
            else
                ChosenWishlistId = null;

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void NotifySelectionChanged()
        {
            Notify(nameof(SelectedCount));
            Notify(nameof(CanImport));
            Notify(nameof(AllSelected));
        }

        // ── INotifyPropertyChanged ─────────────────────────────────────────────

        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
