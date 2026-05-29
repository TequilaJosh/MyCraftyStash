using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;
using MyCraftyStash.Models;
using MyCraftyStash.Services;
using MyCraftyStash.Views;
using Microsoft.Win32;

namespace MyCraftyStash.ViewModels
{
    public partial class WishlistViewModel : BaseViewModel
    {
        private readonly WishlistService _wishlistService;
        private readonly InventoryService _inventoryService;
        private readonly MainViewModel _mainVM;
        private readonly TeWishlistImportService _teImportService = new();

        // All items in the active list
        [ObservableProperty] private ObservableCollection<WishlistItem> _items = new();
        [ObservableProperty] private string _errorMessage = string.Empty;
        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _searchText = string.Empty;

        // Tabs
        [ObservableProperty] private ObservableCollection<WishlistTabViewModel> _tabs = new();
        [ObservableProperty] private WishlistTabViewModel? _activeTab;

        public bool HasActiveTab => ActiveTab != null;
        public bool HasNoTabs   => Tabs.Count == 0;

        public IEnumerable<WishlistItem> FilteredItems
        {
            get
            {
                var items = Items.AsEnumerable();
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    var q = SearchText.Trim().ToLowerInvariant();
                    items = items.Where(i =>
                        i.Name.ToLowerInvariant().Contains(q) ||
                        (i.PurchasedFrom?.ToLowerInvariant().Contains(q) ?? false) ||
                        (i.Theme?.ToLowerInvariant().Contains(q) ?? false) ||
                        (i.ItemNumber?.ToLowerInvariant().Contains(q) ?? false) ||
                        (i.Type?.ToLowerInvariant().Contains(q) ?? false) ||
                        (i.Notes?.ToLowerInvariant().Contains(q) ?? false));
                }
                return items;
            }
        }

        public WishlistViewModel(WishlistService wishlistService, InventoryService inventoryService, MainViewModel mainVM)
        {
            _wishlistService  = wishlistService;
            _inventoryService = inventoryService;
            _mainVM           = mainVM;
        }

        partial void OnSearchTextChanged(string value) => OnPropertyChanged(nameof(FilteredItems));

        partial void OnActiveTabChanged(WishlistTabViewModel? value)
        {
            OnPropertyChanged(nameof(HasActiveTab));
            foreach (var tab in Tabs) tab.IsActive = ReferenceEquals(tab, value);
        }

        // ── Load ─────────────────────────────────────────────────────────────

        [RelayCommand]
        public async Task LoadItems()
        {
            IsLoading = true;
            try
            {
                var lists = await _wishlistService.GetWishlistsAsync();
                Tabs = new ObservableCollection<WishlistTabViewModel>(
                    lists.Select(l => new WishlistTabViewModel(l)));

                // Load all items once and partition stats per tab.
                var allItems = await _wishlistService.GetAllAsync();
                RecalculateTabStats(allItems);

                // Pick an active tab (preserve current id if possible).
                var preserveId = ActiveTab?.Id;
                ActiveTab = preserveId.HasValue
                    ? Tabs.FirstOrDefault(t => t.Id == preserveId.Value) ?? Tabs.FirstOrDefault()
                    : Tabs.FirstOrDefault();

                await RefreshItemsAsync();

                OnPropertyChanged(nameof(HasNoTabs));
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to load wish list.";
                LoggingService.LogError(ex, "WishlistViewModel.LoadItems");
            }
            finally { IsLoading = false; }
        }

        private async Task RefreshItemsAsync()
        {
            if (ActiveTab == null) { Items = new ObservableCollection<WishlistItem>(); OnPropertyChanged(nameof(FilteredItems)); return; }
            var list = await _wishlistService.GetAllAsync(ActiveTab.Id);
            Items = new ObservableCollection<WishlistItem>(list);
            OnPropertyChanged(nameof(FilteredItems));
            // Refresh active tab stats.
            UpdateTabStats(ActiveTab, list);
        }

        private void RecalculateTabStats(IEnumerable<WishlistItem> allItems)
        {
            var byList = allItems.GroupBy(i => i.WishlistId).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var tab in Tabs)
            {
                if (byList.TryGetValue(tab.Id, out var list))
                    UpdateTabStats(tab, list);
                else
                {
                    tab.ItemCount = 0;
                    tab.EstimatedTotal = 0;
                }
            }
        }

        private static void UpdateTabStats(WishlistTabViewModel tab, IList<WishlistItem> list)
        {
            tab.ItemCount      = list.Count;
            tab.EstimatedTotal = list.Sum(i => i.Price ?? 0m);
        }

        // ── Tab management ───────────────────────────────────────────────────

        [RelayCommand]
        private async Task SelectTab(WishlistTabViewModel? tab)
        {
            if (tab == null || ReferenceEquals(tab, ActiveTab)) return;
            ActiveTab = tab;
            IsLoading = true;
            try { await RefreshItemsAsync(); }
            finally { IsLoading = false; }
        }

        [RelayCommand]
        private async Task NewList()
        {
            var dlg = new WishlistListDialog { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() != true) return;

            IsLoading = true;
            try
            {
                var created = await _wishlistService.CreateWishlistAsync(
                    dlg.Result.Name, dlg.Result.Color, dlg.Result.Description);
                var tab = new WishlistTabViewModel(created);
                Tabs.Add(tab);
                ActiveTab = tab;
                await RefreshItemsAsync();
                OnPropertyChanged(nameof(HasNoTabs));
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to create list.";
                LoggingService.LogError(ex, "WishlistViewModel.NewList");
            }
            finally { IsLoading = false; }
        }

        [RelayCommand]
        private async Task EditActiveList()
        {
            if (ActiveTab == null) return;
            var dlg = new WishlistListDialog(ActiveTab.Source) { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() != true) return;

            try
            {
                ActiveTab.Source.Name        = dlg.Result.Name;
                ActiveTab.Source.Color       = dlg.Result.Color;
                ActiveTab.Source.Description = dlg.Result.Description;
                await _wishlistService.UpdateWishlistAsync(ActiveTab.Source);
                ActiveTab.SyncFromSource();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to update list.";
                LoggingService.LogError(ex, "WishlistViewModel.EditActiveList");
            }
        }

        [RelayCommand]
        private async Task DeleteActiveList()
        {
            if (ActiveTab == null) return;
            var confirm = MessageBox.Show(
                $"Delete the list \"{ActiveTab.Name}\"? Its items will be moved to no list.",
                "Delete list", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            IsLoading = true;
            try
            {
                var doomed = ActiveTab;
                await _wishlistService.DeleteWishlistAsync(doomed.Id);
                Tabs.Remove(doomed);
                ActiveTab = Tabs.FirstOrDefault();
                await RefreshItemsAsync();
                OnPropertyChanged(nameof(HasNoTabs));
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to delete list.";
                LoggingService.LogError(ex, "WishlistViewModel.DeleteActiveList");
            }
            finally { IsLoading = false; }
        }

        // ── Item management ───────────────────────────────────────────────────

        [RelayCommand]
        private async Task AddItem()
        {
            if (ActiveTab == null)
            {
                await NewList();
                if (ActiveTab == null) return;
            }
            var dlg = new WishlistItemDialog { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() != true) return;

            IsLoading = true;
            try
            {
                dlg.Result.WishlistId = ActiveTab.Id;
                var created = await _wishlistService.AddAsync(dlg.Result);
                Items.Add(created);
                OnPropertyChanged(nameof(FilteredItems));
                UpdateTabStats(ActiveTab, Items);
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to save item.";
                LoggingService.LogError(ex, "WishlistViewModel.AddItem");
            }
            finally { IsLoading = false; }
        }

        [RelayCommand]
        private async Task EditItem(WishlistItem item)
        {
            if (item == null) return;
            var dlg = new WishlistItemDialog(item) { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() != true) return;

            try
            {
                item.Name          = dlg.Result.Name;
                item.Type          = dlg.Result.Type;
                item.ItemNumber    = dlg.Result.ItemNumber;
                item.Theme         = dlg.Result.Theme;
                item.Price         = dlg.Result.Price;
                item.Notes         = dlg.Result.Notes;
                item.Priority      = dlg.Result.Priority;
                item.PurchasedFrom = dlg.Result.PurchasedFrom;
                item.Url           = dlg.Result.Url;

                await _wishlistService.UpdateAsync(item);
                OnPropertyChanged(nameof(FilteredItems));
                if (ActiveTab != null) UpdateTabStats(ActiveTab, Items);
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to update item.";
                LoggingService.LogError(ex, "WishlistViewModel.EditItem");
            }
        }

        [RelayCommand]
        private async Task DeleteItem(WishlistItem item)
        {
            if (item == null) return;
            try
            {
                await _wishlistService.DeleteAsync(item.Id);
                Items.Remove(item);
                OnPropertyChanged(nameof(FilteredItems));
                if (ActiveTab != null) UpdateTabStats(ActiveTab, Items);
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to delete item.";
                LoggingService.LogError(ex, "WishlistViewModel.DeleteItem");
            }
        }

        [RelayCommand]
        private async Task MoveToInventory(WishlistItem item)
        {
            if (item == null) return;
            try
            {
                // Resolve the inventory type. The wishlist often holds free-form
                // values (e.g. "Die" from a TE import) that don't match the
                // canonical inventory types ("Dies"). Try to auto-match first;
                // only prompt the user when we can't pick confidently.
                var availableTypes = _inventoryService.GetItemTypes();
                var resolvedType = WishlistTypeMatcher.FindBestMatch(item.Type, availableTypes);

                if (resolvedType == null)
                {
                    var dialog = new TypePickerDialog(
                        itemName: item.Name,
                        originalType: item.Type,
                        availableTypes: availableTypes,
                        suggestedType: null)
                    {
                        Owner = Application.Current?.MainWindow,
                    };

                    var ok = dialog.ShowDialog();
                    if (ok != true || string.IsNullOrWhiteSpace(dialog.SelectedType))
                        return; // user cancelled — leave the wishlist item in place
                    resolvedType = dialog.SelectedType;
                }

                await _wishlistService.MoveToInventoryAsync(item, _inventoryService, resolvedType);
                Items.Remove(item);
                OnPropertyChanged(nameof(FilteredItems));
                if (ActiveTab != null) UpdateTabStats(ActiveTab, Items);
                _mainVM.NavigateToInventoryCommand.Execute(null);
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to move item to inventory.";
                LoggingService.LogError(ex, "WishlistViewModel.MoveToInventory");
            }
        }

        // ── Imports ───────────────────────────────────────────────────────────

        [RelayCommand]
        private async Task ImportCsv()
        {
            if (ActiveTab == null)
            {
                await NewList();
                if (ActiveTab == null) return;
            }

            var picker = new OpenFileDialog
            {
                Title = "Import wish list CSV",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            };
            if (picker.ShowDialog() != true) return;

            IsLoading = true;
            try
            {
                var parsed = WishlistCsvImportService.Parse(picker.FileName);
                if (parsed.Count == 0)
                {
                    ErrorMessage = "CSV had no readable rows. Expected headers like name, type, price, priority, store, url, notes.";
                    return;
                }

                foreach (var item in parsed)
                {
                    item.WishlistId = ActiveTab.Id;
                    var created = await _wishlistService.AddAsync(item);
                    Items.Add(created);
                }
                OnPropertyChanged(nameof(FilteredItems));
                UpdateTabStats(ActiveTab, Items);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"CSV import failed: {ex.Message}";
                LoggingService.LogError(ex, "WishlistViewModel.ImportCsv");
            }
            finally { IsLoading = false; }
        }

        [RelayCommand]
        private async Task ImportFromLink()
        {
            if (ActiveTab == null)
            {
                await NewList();
                if (ActiveTab == null) return;
            }

            var prompt = new WishlistLinkPromptDialog { Owner = Application.Current.MainWindow };
            if (prompt.ShowDialog() != true || prompt.Result == null) return;

            // Open the item dialog prefilled — let user review/adjust.
            var review = new WishlistItemDialog(prompt.Result) { Owner = Application.Current.MainWindow };
            if (review.ShowDialog() != true) return;

            IsLoading = true;
            try
            {
                review.Result.WishlistId = ActiveTab.Id;
                review.Result.ImageUrl   = prompt.Result.ImageUrl;
                var created = await _wishlistService.AddAsync(review.Result);
                Items.Add(created);
                OnPropertyChanged(nameof(FilteredItems));
                UpdateTabStats(ActiveTab, Items);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to add imported item: {ex.Message}";
                LoggingService.LogError(ex, "WishlistViewModel.ImportFromLink");
            }
            finally { IsLoading = false; }
        }

        [RelayCommand]
        private async Task ImportFromTeWishlist()
        {
            var lists = Tabs.Select(t => t.Source).ToList();
            var dialog = new TeWishlistImportDialog(_teImportService, new ObservableCollection<Wishlist>(lists))
            {
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() != true || dialog.ImportedItems.Count == 0) return;

            IsLoading = true;
            try
            {
                int? targetWishlistId = dialog.ChosenWishlistId;
                if (!string.IsNullOrWhiteSpace(dialog.ChosenNewListName))
                {
                    var newList = await _wishlistService.CreateWishlistAsync(dialog.ChosenNewListName);
                    var newTab = new WishlistTabViewModel(newList);
                    Tabs.Add(newTab);
                    ActiveTab = newTab;
                    targetWishlistId = newList.Id;
                    OnPropertyChanged(nameof(HasNoTabs));
                }

                // Visit each product page in parallel to fill in SKU + proper category.
                await _teImportService.EnrichItemsAsync(dialog.ImportedItems);

                foreach (var teItem in dialog.ImportedItems)
                {
                    var wishlistItem = new WishlistItem
                    {
                        Name          = teItem.Name,
                        Type          = teItem.Type,
                        ItemNumber    = teItem.ItemNumber,
                        Price         = teItem.Price,
                        ImageUrl      = teItem.ImageUrl,
                        PurchasedFrom = "Taylored Expressions",
                        Url           = teItem.ProductUrl,
                        Priority      = 2,
                        WishlistId    = targetWishlistId,
                    };
                    var created = await _wishlistService.AddAsync(wishlistItem);
                    if (ActiveTab != null && created.WishlistId == ActiveTab.Id)
                        Items.Add(created);
                }
                OnPropertyChanged(nameof(FilteredItems));
                if (ActiveTab != null) UpdateTabStats(ActiveTab, Items);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Import failed: {ex.Message}";
                LoggingService.LogError(ex, "WishlistViewModel.ImportFromTeWishlist");
            }
            finally { IsLoading = false; }
        }
    }
}
