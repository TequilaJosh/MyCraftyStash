using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Windows;
using MyCraftyStash.Models;
using MyCraftyStash.Services;
using MyCraftyStash.Views;

namespace MyCraftyStash.ViewModels
{
    /// <summary>Lightweight item reference used for the related-items picker list.</summary>
    internal class ItemLookupEntry
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? ItemNumber { get; set; }
        public string? Subtype { get; set; }
        public string? Theme { get; set; }
    }

    public partial class InventoryViewModel : BaseViewModel
    {
        private readonly InventoryService _service;
        private readonly SentimentService _sentimentService;
        private readonly MainViewModel _mainVM;
        private List<Item>? _cachedItemsList;
        private readonly SemaphoreSlim _loadLock = new SemaphoreSlim(1, 1);
        private List<ItemLookupEntry> _itemLookupList = new();
        private readonly Stack<Item> _navigationHistory = new();
        private Action? _onBackOverride;
        private bool _imagesChangedDuringEdit;
        private bool _itemsAreDirty = true;
        private bool _suppressSearch;
        private CancellationTokenSource? _searchDebounce;
        private HashSet<int> _savedRelatedSelections = new();
        public bool ItemsAreDirty => _itemsAreDirty;
        
        [ObservableProperty]
        private ObservableCollection<Item> _items = new();
        
        [ObservableProperty]
        private string _searchText = string.Empty;

        /// <summary>When true, the search text only matches against item names.
        /// Defaults to true: most searches are "where's my Big Thanks set" and
        /// the broader theme/sentiment scan adds noise the user usually doesn't want.</summary>
        [ObservableProperty]
        private bool _searchByName = true;

        /// <summary>When true, the search text only matches against themes.</summary>
        [ObservableProperty]
        private bool _searchByTheme;

        /// <summary>When true, only items with no image are shown.</summary>
        [ObservableProperty]
        private bool _noPictureOnly;

        /// <summary>When true, only items flagged as discontinued are shown.
        /// Combines with the rest of the search filters — set on its own to
        /// browse all discontinued items, or layer on top of an active search.</summary>
        [ObservableProperty]
        private bool _discontinuedOnly;

        [ObservableProperty]
        private string? _selectedType;

        [ObservableProperty]
        private string? _selectedTheme;

        // Subtype checkboxes shown in the search bar when a type with subtypes is selected
        [ObservableProperty]
        private ObservableCollection<SubtypeCheckboxItem> _searchSubtypeFilters = new();

        public bool HasSearchSubtypeFilters => SearchSubtypeFilters.Count > 0;

        // Theme checkboxes shown in the search bar instead of subtypes while
        // "Theme only" is checked
        [ObservableProperty]
        private ObservableCollection<SubtypeCheckboxItem> _searchThemeFilters = new();

        public bool HasSearchThemeFilters => SearchThemeFilters.Count > 0;
        
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsTrackedItem))]
        [NotifyPropertyChangedFor(nameof(StockUnitLabel))]
        private Item? _selectedItem;
        
        [ObservableProperty]
        private ObservableCollection<Item> _relatedItems = new();
        
        [ObservableProperty]
        private ObservableCollection<ItemImage> _itemImages = new();
        
        [ObservableProperty]
        private ObservableCollection<SentimentImage> _sentimentImages = new();

        [ObservableProperty]
        private ObservableCollection<string> _sentimentLines = new();

        [ObservableProperty]
        private bool _isEditingSentiments;

        [ObservableProperty]
        private string _editingSentimentsText = string.Empty;

        // Snips captured during Add Item - saved to DB after item is created
        public List<(string ImageData, string Text)> PendingSnips { get; } = new();
        
        partial void OnItemImagesChanged(ObservableCollection<ItemImage> value)
        {
            OnPropertyChanged(nameof(TotalImages));
        }
        
        [ObservableProperty]
        private int _currentImageIndex;
        
        [ObservableProperty]
        private string? _currentDisplayImage;
        
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowTabStrip))]
        private bool _isAddingItem;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowTabStrip))]
        private bool _isEditingItem;
        
        [ObservableProperty]
        private bool _isViewingDetails;
        
        [ObservableProperty]
        private ObservableCollection<ItemPurchase> _itemPurchases = new();
        
        [ObservableProperty]
        private int _totalQuantity;
        
        [ObservableProperty]
        private decimal _totalSpend;
        
        [ObservableProperty]
        private bool _isAddingPurchase;

        [ObservableProperty]
        private string? _purchaseErrorMessage;
        
        [ObservableProperty]
        private int _newPurchaseQuantity = 1;
        
        [ObservableProperty]
        private decimal? _newPurchasePrice;
        
        [ObservableProperty]
        private DateTime? _newPurchaseDate;

        // ── Sales (revenue side; mirrors the purchase fields above) ──
        [ObservableProperty]
        private ObservableCollection<ItemSale> _itemSales = new();

        [ObservableProperty]
        private int _totalSold;

        [ObservableProperty]
        private decimal _totalRevenue;

        [ObservableProperty]
        private bool _isAddingSale;

        [ObservableProperty]
        private string? _saleErrorMessage;

        [ObservableProperty]
        private int _newSaleQuantity = 1;

        [ObservableProperty]
        private decimal? _newSalePrice;

        [ObservableProperty]
        private DateTime? _newSaleDate;

        [ObservableProperty]
        private string _newItemName = string.Empty;

        /// <summary>Inline warning shown above the Add/Edit form when the in-progress
        /// item duplicates an existing inventory entry by ItemNumber or Name. Null hides
        /// the banner.</summary>
        [ObservableProperty]
        private string? _duplicateItemWarning;

        /// <summary>Id of the existing matching item (if any), used by the "View" link
        /// on the duplicate banner.</summary>
        [ObservableProperty]
        private int? _duplicateItemId;

        // Per-field validation flags. Set by SaveItemCoreAsync / UpdateItem when the
        // user clicks Save with missing or invalid input, cleared as soon as the user
        // edits the offending field. The Add/Edit XAML binds the badge visibility and
        // red border directly to these flags.
        [ObservableProperty] private bool _newItemNameHasError;
        [ObservableProperty] private bool _newItemTypeHasError;
        [ObservableProperty] private bool _newItemPriceHasError;
        [ObservableProperty] private bool _newItemDatePurchasedHasError;

        // Tooltip messages shown on the "!" badge — required-field cases use a static
        // string, but Price and Date have value-range validations whose message we
        // surface here so the user sees the specific reason.
        [ObservableProperty] private string? _newItemPriceErrorMessage;
        [ObservableProperty] private string? _newItemDatePurchasedErrorMessage;
        
        [ObservableProperty]
        private string _newItemType = "Stamp";
        
        [ObservableProperty]
        private string _newItemTheme = string.Empty;
        
        [ObservableProperty]
        private ObservableCollection<ThemeCheckboxItem> _themeCheckboxes = new();
        
        [ObservableProperty]
        private ObservableCollection<SubtypeCheckboxItem> _subtypeCheckboxes = new();
        public bool HasSubtypeCheckboxes => SubtypeCheckboxes.Count > 0;
        
        [ObservableProperty]
        private string _comboClubYear = string.Empty;

        [ObservableProperty]
        private bool _isComboClubSelected;

        [ObservableProperty]
        private ObservableCollection<SubtypeCheckboxItem> _comboClubOptions = new();

        public bool HasComboClubOptions => ComboClubOptions.Count > 0;
        
        private static List<string>? _themeOptions;
        private static HashSet<string> _requiresInfoThemes = new();
        
        private static List<string> ThemeOptions
        {
            get
            {
                if (_themeOptions == null)
                {
                    _themeOptions = LoadThemesFromFile();
                }
                return _themeOptions;
            }
        }
        
        public static List<string> GetThemeOptions() => ThemeOptions;
        
        public static void ReloadThemeOptions()
        {
            _themeOptions = null;
            _requiresInfoThemes.Clear();
        }
        
        private static bool ThemeRequiresInfo(string themeName)
        {
            return _requiresInfoThemes.Contains(themeName);
        }
        
        private static List<string> LoadThemesFromFile()
        {
            _requiresInfoThemes.Clear();
            try
            {
                var lines = MyCraftyStash.Services.ConfigStore.GetLines(MyCraftyStash.Services.ConfigStore.Themes);
                {
                    if (lines.Any())
                    {
                        var themes = new List<string>();
                        foreach (var line in lines)
                        {
                            if (line.StartsWith("*"))
                            {
                                var name = line.Substring(1).Trim();
                                themes.Add(name);
                                _requiresInfoThemes.Add(name);
                            }
                            else
                            {
                                themes.Add(line);
                            }
                        }
                        LoggingService.LogInfo($"Loaded {themes.Count} themes ({_requiresInfoThemes.Count} require extra info)");
                        return themes;
                    }
                }
                LoggingService.LogInfo("Theme list empty in settings DB, using defaults");
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "Error loading themes from file");
            }
            
            _requiresInfoThemes.Add("Combo Club");
            return new List<string>
            {
                "Mini & Bitty Strips", "New Years", "Kits",
                "Valentine's Day", "Freebies",
                "St. Patrick's Day", "Combo Club",
                "Greetings", "Easter",
                "Parent's Days", "Halloween",
                "Sympathy", "Thanksgiving",
                "Wedding & Baby", "Christmas",
                "Graduation", "Winter", "Spring",
                "Itty Bitty", "Summer", "Autumn", "Sports", "Words", 
                "Alphabets & Numbers", "Floral", "Strips", "Inspiration"
            };
        }
        
        [ObservableProperty]
        private string _newItemSentiments = string.Empty;
        
        [ObservableProperty]
        private string _newItemImageUrl = string.Empty;
        
        [ObservableProperty]
        private ObservableCollection<string> _newItemImages = new();
        
        [ObservableProperty]
        private decimal? _newItemPrice;
        
        [ObservableProperty]
        private DateTime? _newItemDatePurchased;
        
        [ObservableProperty]
        private string _newItemNumber = string.Empty;

        [ObservableProperty]
        private string _newItemLocation = string.Empty;
        
        [ObservableProperty]
        private bool _newItemIsDiscontinued;
        
        [ObservableProperty]
        private int? _newItemStencilLayers;

        [ObservableProperty]
        private int? _newItemPackSize;

        [ObservableProperty]
        private int? _newItemCurrentStock;

        [ObservableProperty]
        private string _newItemPurchasedFrom = string.Empty;

        [ObservableProperty]
        private string _newItemNotes = string.Empty;

        [ObservableProperty]
        private string _newItemSiteUrl = string.Empty;

        [ObservableProperty]
        private ObservableCollection<string> _purchasedFromOptions = new();

        // For inline stock editing on detail view
        [ObservableProperty]
        private bool _isEditingStock;

        [ObservableProperty]
        private int _editStockValue;
        
        [ObservableProperty]
        private ObservableCollection<SelectableItem> _selectableItems = new();
        
        [ObservableProperty]
        private string _relatedItemsSearchText = string.Empty;

        [ObservableProperty]
        private string? _relatedItemsFilterType;

        [ObservableProperty]
        private ObservableCollection<SubtypeCheckboxItem> _relatedSubtypeFilters = new();

        [ObservableProperty]
        private bool _relatedItemsSearchByName;

        [ObservableProperty]
        private bool _relatedItemsSearchByTheme;

        public bool HasRelatedSubtypeFilters => RelatedSubtypeFilters.Count > 0;
        
        public IEnumerable<SelectableItem> FilteredSelectableItems
        {
            get
            {
                // Don't pre-populate the related-items picker — only show results once
                // the user starts typing or sets a type/subtype filter. Avoids dumping
                // the entire inventory into the sidebar by default.
                var hasSearch = !string.IsNullOrWhiteSpace(RelatedItemsSearchText);
                var hasTypeFilter = !string.IsNullOrEmpty(RelatedItemsFilterType);
                var checkedSubs = RelatedSubtypeFilters.Where(c => c.IsChecked).Select(c => c.Label).ToList();
                if (!hasSearch && !hasTypeFilter && checkedSubs.Count == 0)
                    return Enumerable.Empty<SelectableItem>();

                var items = SelectableItems.AsEnumerable();

                if (hasSearch)
                {
                    var q = RelatedItemsSearchText.ToLower();
                    if (RelatedItemsSearchByName)
                        items = items.Where(s => s.ItemName.ToLower().Contains(q));
                    else if (RelatedItemsSearchByTheme)
                        items = items.Where(s => s.ItemTheme?.ToLower().Contains(q) ?? false);
                    else
                        items = items.Where(s =>
                            s.ItemName.ToLower().Contains(q) ||
                            s.ItemType.ToLower().Contains(q) ||
                            (s.ItemNumber?.ToLower().Contains(q) ?? false));
                }

                if (hasTypeFilter)
                    items = items.Where(s => s.ItemType == RelatedItemsFilterType);

                if (checkedSubs.Count > 0)
                    items = items.Where(s => s.ItemSubtype != null && checkedSubs.Contains(s.ItemSubtype));

                return items;
            }
        }

        public IEnumerable<SelectableItem> SelectedRelatedItems =>
            SelectableItems.Where(s => s.IsSelected);

        public bool HasSelectedRelatedItems =>
            SelectableItems.Any(s => s.IsSelected);

        private void OnSelectableItemSelectionChanged()
        {
            OnPropertyChanged(nameof(SelectedRelatedItems));
            OnPropertyChanged(nameof(HasSelectedRelatedItems));
        }
        
        partial void OnRelatedItemsSearchTextChanged(string value) =>
            OnPropertyChanged(nameof(FilteredSelectableItems));

        partial void OnRelatedItemsSearchByNameChanged(bool value)
        {
            if (value) RelatedItemsSearchByTheme = false;
            OnPropertyChanged(nameof(FilteredSelectableItems));
        }

        partial void OnRelatedItemsSearchByThemeChanged(bool value)
        {
            if (value) RelatedItemsSearchByName = false;
            OnPropertyChanged(nameof(FilteredSelectableItems));
        }

        [RelayCommand]
        private void ClearRelatedItemsFilter()
        {
            _savedRelatedSelections = SelectableItems.Where(s => s.IsSelected).Select(s => s.ItemId).ToHashSet();
            RelatedItemsSearchText = string.Empty;
            RelatedItemsFilterType = null;
            RelatedItemsSearchByName = false;
            RelatedItemsSearchByTheme = false;
            RelatedSubtypeFilters.Clear();
            SelectableItems = new ObservableCollection<SelectableItem>();
            OnPropertyChanged(nameof(FilteredSelectableItems));
        }

        [RelayCommand]
        private async Task SearchRelatedItems()
        {
            if (!SelectableItems.Any())
            {
                if (IsEditingItem)
                    await LoadSelectableItemsForEditAsync();
                else
                    await LoadSelectableItems();

                // Restore any selections saved before Clear
                foreach (var item in SelectableItems)
                {
                    if (_savedRelatedSelections.Contains(item.ItemId))
                        item.IsSelected = true;
                }
            }
            OnPropertyChanged(nameof(FilteredSelectableItems));
        }

        partial void OnRelatedItemsFilterTypeChanged(string? value)
        {
            RelatedSubtypeFilters.Clear();
            if (!string.IsNullOrEmpty(value))
            {
                var subs = UserSettingsService.GetSubtypesForType(value);
                foreach (var s in subs)
                {
                    var cb = new SubtypeCheckboxItem { Label = s };
                    cb.PropertyChanged += (_, _) => OnPropertyChanged(nameof(FilteredSelectableItems));
                    RelatedSubtypeFilters.Add(cb);
                }
            }
            OnPropertyChanged(nameof(FilteredSelectableItems));
            OnPropertyChanged(nameof(HasRelatedSubtypeFilters));
        }
        
        private ObservableCollection<string>? _itemTypes;
        private ObservableCollection<string>? _itemLocations;
        
        public ObservableCollection<string> ItemTypes
        {
            get
            {
                if (_itemTypes == null)
                    _itemTypes = new ObservableCollection<string>(_service.GetItemTypes());
                return _itemTypes;
            }
        }
        
        public ObservableCollection<string> ItemLocations
        {
            get
            {
                if (_itemLocations == null)
                {
                    _itemLocations = new ObservableCollection<string>(_service.GetItemLocations());
                    if (!_itemLocations.Contains(string.Empty))
                        _itemLocations.Insert(0, string.Empty);
                }
                return _itemLocations;
            }
        }
        
        public void RefreshConfigLists()
        {
            ReloadThemeOptions();
            InitializeThemeCheckboxes();
            
            // Refresh ItemTypes in-place to avoid clearing ComboBox selection
            var freshTypes = _service.GetItemTypes();
            if (_itemTypes == null)
                _itemTypes = new ObservableCollection<string>(freshTypes);
            else
            {
                _itemTypes.Clear();
                foreach (var t in freshTypes) _itemTypes.Add(t);
            }
            
            var freshLocations = _service.GetItemLocations();
            if (_itemLocations == null)
                _itemLocations = new ObservableCollection<string>(freshLocations);
            else
            {
                _itemLocations.Clear();
                foreach (var l in freshLocations) _itemLocations.Add(l);
            }
            if (!_itemLocations.Contains(string.Empty))
                _itemLocations.Insert(0, string.Empty);
            
            OnPropertyChanged(nameof(ItemTypes));
            OnPropertyChanged(nameof(ItemLocations));

            var freshPurchasedFrom = _service.GetPurchasedFromOptions();
            PurchasedFromOptions.Clear();
            PurchasedFromOptions.Add(string.Empty);
            foreach (var p in freshPurchasedFrom) PurchasedFromOptions.Add(p);
        }
        
        public InventoryViewModel(InventoryService service, MainViewModel mainVM)
        {
            _service = service;
            _sentimentService = new SentimentService();
            _mainVM = mainVM;
            OpenTabs.CollectionChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(HasOpenTabs));
                OnPropertyChanged(nameof(ShowTabStrip));
            };
            InitializeThemeCheckboxes();
            var purchasedFromOptions = _service.GetPurchasedFromOptions();
            purchasedFromOptions.Insert(0, string.Empty);
            PurchasedFromOptions = new ObservableCollection<string>(purchasedFromOptions);
        }
        
        private void InitializeThemeCheckboxes()
        {
            ThemeCheckboxes = new ObservableCollection<ThemeCheckboxItem>(
                ThemeOptions.Select(t => new ThemeCheckboxItem(t, OnThemeSelectionChanged))
            );
            ComboClubYear = string.Empty;
            IsComboClubSelected = false;

            // Load saved Combo Club options (e.g. "Spring 2024", "Fall 2024") from subtypes config
            var options = UserSettingsService.GetSubtypesForType("Combo Club");
            ComboClubOptions = new ObservableCollection<SubtypeCheckboxItem>(
                options.Select(o =>
                {
                    var cb = new SubtypeCheckboxItem { Label = o };
                    cb.PropertyChanged += (_, _) => BuildThemeString();
                    return cb;
                })
            );
            OnPropertyChanged(nameof(HasComboClubOptions));
        }
        
        private void OnThemeSelectionChanged()
        {
            IsComboClubSelected = ThemeCheckboxes.Any(t => t.IsSelected && ThemeRequiresInfo(t.Name));
            BuildThemeString();
        }
        
        private void BuildThemeString()
        {
            var result = new List<string>();
            foreach (var t in ThemeCheckboxes.Where(t => t.IsSelected))
            {
                if (ThemeRequiresInfo(t.Name))
                {
                    if (HasComboClubOptions)
                    {
                        // Each checked Combo Club option becomes a separate "Combo Club {option}" entry
                        var checkedOptions = ComboClubOptions.Where(o => o.IsChecked).Select(o => o.Label).ToList();
                        if (checkedOptions.Count > 0)
                            result.AddRange(checkedOptions.Select(o => $"{t.Name} {o}"));
                        else
                            result.Add(t.Name); // no option selected, just add the theme name
                    }
                    else if (!string.IsNullOrWhiteSpace(ComboClubYear))
                    {
                        result.Add($"{t.Name} {ComboClubYear}");
                    }
                    else
                    {
                        result.Add(t.Name);
                    }
                }
                else
                {
                    result.Add(t.Name);
                }
            }
            NewItemTheme = string.Join(", ", result);
        }
        
        partial void OnComboClubYearChanged(string value)
        {
            BuildThemeString();
        }
        
        private void SetThemeCheckboxesFromString(string? theme)
        {
            foreach (var checkbox in ThemeCheckboxes)
                checkbox.IsSelected = false;
            foreach (var opt in ComboClubOptions)
                opt.IsChecked = false;
            ComboClubYear = string.Empty;

            if (!string.IsNullOrWhiteSpace(theme))
            {
                var themes = theme.Split(',').Select(t => t.Trim()).ToList();

                foreach (var t in themes)
                {
                    var matchedInfoTheme = ThemeCheckboxes.FirstOrDefault(c => ThemeRequiresInfo(c.Name) && t.StartsWith(c.Name));
                    if (matchedInfoTheme != null)
                    {
                        matchedInfoTheme.IsSelected = true;
                        var suffix = t.Substring(matchedInfoTheme.Name.Length).Trim();
                        if (!string.IsNullOrEmpty(suffix))
                        {
                            if (HasComboClubOptions)
                            {
                                // Try to match to a saved Combo Club option
                                var opt = ComboClubOptions.FirstOrDefault(o => o.Label == suffix);
                                if (opt != null) opt.IsChecked = true;
                                else ComboClubYear = suffix; // fallback: old free-text year
                            }
                            else
                            {
                                ComboClubYear = suffix;
                            }
                        }
                    }
                    else
                    {
                        var checkbox = ThemeCheckboxes.FirstOrDefault(c => c.Name == t);
                        if (checkbox != null)
                            checkbox.IsSelected = true;
                    }
                }

                IsComboClubSelected = ThemeCheckboxes.FirstOrDefault(c => c.Name == "Combo Club")?.IsSelected ?? false;
            }
            else
            {
                IsComboClubSelected = false;
            }

            // Always rebuild the theme string so NewItemTheme is in sync with the current
            // checkbox state - even when no callbacks fired (e.g. all boxes were already false).
            BuildThemeString();
        }
        
        [RelayCommand]
        private async Task LoadItems()
        {
            var selectedSubtypes = SearchSubtypeFilters.Where(s => s.IsChecked).Select(s => s.Label).ToList();
            var selectedThemes = SearchThemeFilters.Where(s => s.IsChecked).Select(s => s.Label).ToList();
            bool hasFilters = !string.IsNullOrWhiteSpace(SearchText) || SelectedType != null || _selectedTypes.Count > 0 || SelectedTheme != null || NoPictureOnly || DiscontinuedOnly || selectedSubtypes.Count > 0 || selectedThemes.Count > 0;
            
            if (!_itemsAreDirty && !hasFilters && _cachedItemsList != null && Items.Count > 0)
            {
                return;
            }
            
            await _loadLock.WaitAsync();
            try
            {
                IsLoading = true;
                string? searchMode = SearchByName ? "name" : SearchByTheme ? "theme" : null;
                // A single selected type goes through the legacy `type` parameter so
                // its special cases (Combo Club editions) keep working; two or more
                // go through the `types` list.
                string? singleType = _selectedTypes.Count == 1 ? _selectedTypes[0] : SelectedType;
                var typeList = _selectedTypes.Count > 1 ? _selectedTypes : null;
                var items = await _service.GetItemsAsync(SearchText, singleType, SelectedTheme, searchMode: searchMode, noPictureOnly: NoPictureOnly, subtypes: selectedSubtypes.Count > 0 ? selectedSubtypes : null, themes: selectedThemes.Count > 0 ? selectedThemes : null, types: typeList, discontinuedOnly: DiscontinuedOnly);
                Items = new ObservableCollection<Item>(items);
                
                // Kick off background thumbnail preloading so images are ready when user scrolls
                ThumbnailCacheService.PreloadAsync(items.Select(i => i.Id));
                
                if (!hasFilters)
                {
                    _cachedItemsList = items;
                    _itemsAreDirty = false;
                    _itemLookupList = items.Select(i => new ItemLookupEntry
                    {
                        Id = i.Id,
                        Name = i.Name,
                        Type = i.Type,
                        ItemNumber = i.ItemNumber,
                        Subtype = i.Subtype
                    }).ToList();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                _loadLock.Release();
                IsLoading = false;
            }
        }
        
        private void TriggerSearch()
        {
            if (!_suppressSearch) _ = LoadItems();
        }

        partial void OnSearchTextChanged(string value)
        {
            if (_suppressSearch) return;
            _searchDebounce?.Cancel();
            _searchDebounce = new CancellationTokenSource();
            var cts = _searchDebounce;
            _ = Task.Delay(350, cts.Token).ContinueWith(t =>
            {
                if (!t.IsCanceled)
                    Application.Current.Dispatcher.Invoke(() => _ = LoadItems());
            }, TaskScheduler.Default);
        }

        partial void OnSearchByNameChanged(bool value)
        {
            if (value) SearchByTheme = false;
            TriggerSearch();
        }

        partial void OnSearchByThemeChanged(bool value)
        {
            if (value)
            {
                SearchByName = false;

                // "Theme only" swaps the filter-chip row from subtypes to themes.
                SearchSubtypeFilters.Clear();
                OnPropertyChanged(nameof(HasSearchSubtypeFilters));

                SearchThemeFilters.Clear();
                foreach (var theme in ThemeOptions)
                {
                    var cb = new SubtypeCheckboxItem { Label = theme };
                    cb.PropertyChanged += (_, _) => TriggerSearch();
                    SearchThemeFilters.Add(cb);
                }
                OnPropertyChanged(nameof(HasSearchThemeFilters));
                TriggerSearch();
            }
            else
            {
                SearchThemeFilters.Clear();
                OnPropertyChanged(nameof(HasSearchThemeFilters));
                // Restore the subtype chips for the selected types.
                RebuildSubtypeChips();
                TriggerSearch();
            }
        }

        partial void OnNoPictureOnlyChanged(bool value) => TriggerSearch();

        partial void OnDiscontinuedOnlyChanged(bool value) => TriggerSearch();

        // ── Multi-select type filter ─────────────────────────────────────────
        // The Type dropdown is a checkbox popup that stays open until Confirm,
        // so several types can be picked in one go.
        public ObservableCollection<SubtypeCheckboxItem> TypeFilterOptions { get; } = new();

        [ObservableProperty] private bool _isTypeDropdownOpen;
        [ObservableProperty] private string _typeFilterSummary = "All Types";

        private List<string> _selectedTypes = new();

        partial void OnIsTypeDropdownOpenChanged(bool value)
        {
            if (!value) return;
            // Rebuild on every open so new types show up and checks match the filter.
            TypeFilterOptions.Clear();
            foreach (var t in ItemTypes)
                TypeFilterOptions.Add(new SubtypeCheckboxItem { Label = t, IsChecked = _selectedTypes.Contains(t) });
        }

        [RelayCommand]
        private void ConfirmTypeFilter()
        {
            _selectedTypes = TypeFilterOptions.Where(t => t.IsChecked).Select(t => t.Label).ToList();
            TypeFilterSummary = _selectedTypes.Count == 0 ? "All Types"
                : _selectedTypes.Count == 1 ? _selectedTypes[0]
                : $"{_selectedTypes[0]} +{_selectedTypes.Count - 1}";
            IsTypeDropdownOpen = false;
            RebuildSubtypeChips();
            TriggerSearch();
        }

        /// <summary>Repopulates the subtype chip strip with the subtypes of every
        /// selected type (union, deduped). No-op in "Theme only" mode.</summary>
        private void RebuildSubtypeChips()
        {
            SearchSubtypeFilters.Clear();
            OnPropertyChanged(nameof(HasSearchSubtypeFilters));

            if (SearchByTheme || _selectedTypes.Count == 0) return;

            if (_selectedTypes.Count == 1 && _selectedTypes[0] == "Combo Club")
            {
                // Editions are derived dynamically from existing item themes
                if (!_suppressSearch) _ = LoadComboClubEditionsAsync();
                return;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var type in _selectedTypes.Where(t => t != "Combo Club"))
            {
                foreach (var s in UserSettingsService.GetSubtypesForType(type))
                {
                    if (!seen.Add(s)) continue;
                    var cb = new SubtypeCheckboxItem { Label = s };
                    cb.PropertyChanged += (_, _) => TriggerSearch();
                    SearchSubtypeFilters.Add(cb);
                }
            }
            OnPropertyChanged(nameof(HasSearchSubtypeFilters));
        }

        private async Task LoadComboClubEditionsAsync()
        {
            try
            {
                var editions = await _service.GetComboClubEditionsAsync();
                SearchSubtypeFilters.Clear();
                foreach (var ed in editions)
                {
                    var cb = new SubtypeCheckboxItem { Label = ed };
                    cb.PropertyChanged += (_, _) => TriggerSearch();
                    SearchSubtypeFilters.Add(cb);
                }
                OnPropertyChanged(nameof(HasSearchSubtypeFilters));
                TriggerSearch();
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "LoadComboClubEditionsAsync");
            }
        }

        partial void OnNewItemTypeChanged(string value)
        {
            LoadSubtypeCheckboxes(value);
            if (!string.IsNullOrWhiteSpace(value)) NewItemTypeHasError = false;
            OnPropertyChanged(nameof(ShowStencilLayers));
        }

        /// <summary>Whether to show the "Number of Layers" input on the add/edit
        /// form: true when the type OR any selected subtype contains "stencil".</summary>
        public bool ShowStencilLayers =>
            NewItemType.Contains("stencil", StringComparison.OrdinalIgnoreCase)
            || SubtypeCheckboxes.Any(c => c.IsChecked
                   && (c.Label?.Contains("stencil", StringComparison.OrdinalIgnoreCase) ?? false));

        partial void OnNewItemNameChanged(string value)
        {
            if (!string.IsNullOrWhiteSpace(value)) NewItemNameHasError = false;
            _ = ScheduleDuplicateCheck();
        }
        partial void OnNewItemNumberChanged(string value) => _ = ScheduleDuplicateCheck();
        partial void OnNewItemPriceChanged(decimal? value)
        {
            NewItemPriceHasError = false;
            NewItemPriceErrorMessage = null;
        }
        partial void OnNewItemDatePurchasedChanged(DateTime? value)
        {
            NewItemDatePurchasedHasError = false;
            NewItemDatePurchasedErrorMessage = null;
        }

        private void ClearFieldErrors()
        {
            NewItemNameHasError = false;
            NewItemTypeHasError = false;
            NewItemPriceHasError = false;
            NewItemDatePurchasedHasError = false;
            NewItemPriceErrorMessage = null;
            NewItemDatePurchasedErrorMessage = null;
        }

        // Debounce concurrent edits — without it every keystroke would hit the lookup
        // list, and each cancellation prevents stale background tasks from clobbering
        // the warning state set by a later keystroke.
        private CancellationTokenSource? _duplicateCheckCts;

        private async Task ScheduleDuplicateCheck()
        {
            if (!IsAddingItem)
            {
                DuplicateItemWarning = null;
                DuplicateItemId = null;
                return;
            }

            _duplicateCheckCts?.Cancel();
            _duplicateCheckCts = new CancellationTokenSource();
            var ct = _duplicateCheckCts.Token;
            try
            {
                await Task.Delay(300, ct);
                CheckDuplicate();
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "InventoryViewModel.ScheduleDuplicateCheck");
            }
        }

        private void CheckDuplicate()
        {
            var name   = NewItemName?.Trim()   ?? string.Empty;
            var number = NewItemNumber?.Trim() ?? string.Empty;

            // Item-number match is exact and high-confidence. Name match requires at
            // least 3 characters to avoid false positives mid-typing.
            if (number.Length == 0 && name.Length < 3)
            {
                DuplicateItemWarning = null;
                DuplicateItemId = null;
                return;
            }

            ItemLookupEntry? match = null;
            string reason = string.Empty;

            if (number.Length > 0)
            {
                match = _itemLookupList.FirstOrDefault(i =>
                    !string.IsNullOrWhiteSpace(i.ItemNumber) &&
                    string.Equals(i.ItemNumber!.Trim(), number, StringComparison.OrdinalIgnoreCase));
                if (match != null) reason = $"item number {match.ItemNumber}";
            }

            if (match == null && name.Length >= 3)
            {
                match = _itemLookupList.FirstOrDefault(i =>
                    string.Equals(i.Name?.Trim(), name, StringComparison.OrdinalIgnoreCase));
                if (match != null) reason = "the same name";
            }

            // When editing an existing item the form is reused — don't warn about the
            // very item being edited matching itself.
            if (match != null && IsEditingItem && SelectedItem != null && match.Id == SelectedItem.Id)
                match = null;

            if (match == null)
            {
                DuplicateItemWarning = null;
                DuplicateItemId = null;
            }
            else
            {
                DuplicateItemWarning = $"\"{match.Name}\" already exists in your inventory ({reason}).";
                DuplicateItemId = match.Id;
            }
        }

        [RelayCommand]
        private async Task ViewDuplicateItem()
        {
            if (DuplicateItemId is not int id) return;
            var existing = await _service.GetItemByIdAsync(id);
            if (existing == null) return;
            DuplicateItemWarning = null;
            DuplicateItemId = null;
            await ViewItemDetails(existing);
        }

        [RelayCommand]
        private void DismissDuplicateWarning()
        {
            DuplicateItemWarning = null;
            DuplicateItemId = null;
        }

        private void LoadSubtypeCheckboxes(string type)
        {
            SubtypeCheckboxes.Clear();
            if (!string.IsNullOrEmpty(type))
            {
                var subtypes = UserSettingsService.GetSubtypesForType(type);
                foreach (var s in subtypes)
                {
                    var cb = new SubtypeCheckboxItem { Label = s };
                    // Checking a "stencil" subtype should reveal the layers field.
                    cb.PropertyChanged += (_, e) =>
                    {
                        if (e.PropertyName == nameof(SubtypeCheckboxItem.IsChecked))
                            OnPropertyChanged(nameof(ShowStencilLayers));
                    };
                    SubtypeCheckboxes.Add(cb);
                }
            }
            OnPropertyChanged(nameof(HasSubtypeCheckboxes));
            OnPropertyChanged(nameof(ShowStencilLayers));
        }

        private string GetSelectedSubtype()
        {
            var selected = SubtypeCheckboxes.Where(s => s.IsChecked).Select(s => s.Label).ToList();
            return selected.Count > 0 ? string.Join(", ", selected) : string.Empty;
        }

        private void SetSubtypeCheckboxesFromString(string? subtype)
        {
            if (string.IsNullOrEmpty(subtype)) return;
            var parts = subtype.Split(',').Select(s => s.Trim());
            foreach (var cb in SubtypeCheckboxes)
                cb.IsChecked = parts.Contains(cb.Label);
        }

        [RelayCommand]
        private async Task Search()
        {
            await LoadItems();
        }
        
        public async Task SelectItemByIdAsync(int itemId, Action? onBackOverride = null)
        {
            try
            {
                _suppressSearch = true;
                SearchText = string.Empty;
                SearchByName = false;
                SearchByTheme = false;
                NoPictureOnly = false;
                DiscontinuedOnly = false;
                SelectedType = null;
                SelectedTheme = null;
                _selectedTypes.Clear();
                TypeFilterSummary = "All Types";
                SearchSubtypeFilters.Clear();
                OnPropertyChanged(nameof(HasSearchSubtypeFilters));
                _suppressSearch = false;

                await LoadItems();

                var item = Items.FirstOrDefault(i => i.Id == itemId)
                           ?? await _service.GetItemByIdAsync(itemId);
                if (item != null)
                {
                    _onBackOverride = onBackOverride;
                    await ViewItemDetails(item);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, $"Error selecting item {itemId}");
            }
        }
        
        [RelayCommand]
        private void ClearFilters()
        {
            _suppressSearch = true;
            SearchText = string.Empty;
            SearchByName = false;
            SearchByTheme = false;
            NoPictureOnly = false;
            DiscontinuedOnly = false;
            SelectedType = null;
            SelectedTheme = null;
            _selectedTypes.Clear();
            TypeFilterSummary = "All Types";
            SearchSubtypeFilters.Clear();
            OnPropertyChanged(nameof(HasSearchSubtypeFilters));
            _suppressSearch = false;
            Items.Clear();
        }
        
        [RelayCommand]
        private async Task ViewItemDetails(Item item)
        {
            // Hide the detail/edit UI while loading to prevent the user clicking
            // Edit before ItemImages and related data are loaded for this item.
            IsViewingDetails = false;
            IsAddingItem = false;
            IsEditingItem = false;

            var freshItem = await _service.GetItemByIdAsync(item.Id);
            if (freshItem != null)
                item = freshItem;

            SelectedItem = item;

            var related = await _service.GetRelatedItemsAsync(item.Id);
            RelatedItems = new ObservableCollection<Item>(related);

            var images = await _service.GetItemImagesAsync(item.Id);
            ItemImages = new ObservableCollection<ItemImage>(images);
            CurrentImageIndex = 0;
            UpdateCurrentDisplayImage();

            await LoadItemPurchases();
            await LoadItemSales();
            await LoadSentimentImages(item.Id);

            // All data loaded - now show the view so Edit button becomes available
            IsViewingDetails = true;

            // Highlight this item's tab (if it's open in one)
            foreach (var tab in OpenTabs)
                tab.IsActive = tab.Id == item.Id;
        }

        // ── Item tabs ────────────────────────────────────────────────────────
        // Browser-style tabs: middle-click a card (or right-click > Open in New
        // Tab) to bookmark an item without leaving the grid. Activating a tab
        // reloads it through ViewItemDetails, reusing the single detail panel.
        public ObservableCollection<OpenTab> OpenTabs { get; } = new();

        public bool HasOpenTabs => OpenTabs.Count > 0;

        // Hidden while the Add/Edit form is open so a stray tab click can't
        // silently discard a half-filled form.
        public bool ShowTabStrip => HasOpenTabs && !IsAddingItem && !IsEditingItem;

        [RelayCommand]
        private void OpenItemInTab(Item item)
        {
            if (!OpenTabs.Any(t => t.Id == item.Id))
                OpenTabs.Add(new OpenTab { Id = item.Id, Title = item.Name, IsActive = IsViewingDetails && SelectedItem?.Id == item.Id });
        }

        [RelayCommand]
        private async Task ActivateTab(OpenTab tab)
        {
            var item = await _service.GetItemByIdAsync(tab.Id);
            if (item == null)
            {
                // Item was deleted since the tab was opened - drop the tab.
                CloseTab(tab);
                return;
            }
            tab.Title = item.Name;
            await ViewItemDetails(item);
        }

        [RelayCommand]
        private void CloseTab(OpenTab tab)
        {
            int index = OpenTabs.IndexOf(tab);
            OpenTabs.Remove(tab);

            // If the closed tab was on screen, fall to a neighbor tab or the list.
            if (IsViewingDetails && SelectedItem?.Id == tab.Id)
            {
                if (OpenTabs.Count > 0)
                    _ = ActivateTab(OpenTabs[Math.Min(Math.Max(index, 0), OpenTabs.Count - 1)]);
                else
                    BackToList();
            }
        }

        private void CloseTabForItem(int itemId)
        {
            var tab = OpenTabs.FirstOrDefault(t => t.Id == itemId);
            if (tab != null)
                OpenTabs.Remove(tab);
        }
        
        private async Task LoadSentimentImages(int itemId)
        {
            try
            {
                var sentiments = await _sentimentService.GetSentimentsByItemIdAsync(itemId);
                SentimentImages = new ObservableCollection<SentimentImage>(sentiments);
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "Error loading sentiment images");
                SentimentImages = new ObservableCollection<SentimentImage>();
            }

            // Quote-aware parse so chips containing commas/newlines stay intact across reads.
            var lines = SentimentService.ParseSentimentLines(SelectedItem?.Sentiments ?? string.Empty);
            SentimentLines = new ObservableCollection<string>(lines);
            IsEditingSentiments = false;
        }
        
        private async Task LoadItemPurchases()
        {
            if (SelectedItem == null) return;
            
            try
            {
                var purchases = await _service.GetItemPurchasesAsync(SelectedItem.Id);
                ItemPurchases = new ObservableCollection<ItemPurchase>(purchases);
                var totals = await _service.GetItemPurchaseTotalsAsync(SelectedItem.Id);
                TotalQuantity = totals.TotalQuantity;
                TotalSpend = totals.TotalSpend;
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex);
            }
        }

        private async Task LoadItemSales()
        {
            if (SelectedItem == null) return;

            try
            {
                var sales = await _service.GetItemSalesAsync(SelectedItem.Id);
                ItemSales = new ObservableCollection<ItemSale>(sales);
                var totals = await _service.GetItemSaleTotalsAsync(SelectedItem.Id);
                TotalSold = totals.TotalQuantity;
                TotalRevenue = totals.TotalRevenue;
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex);
            }
        }
        
        private void UpdateCurrentDisplayImage()
        {
            if (CurrentImageIndex == 0 && SelectedItem?.ImageUrl != null)
            {
                CurrentDisplayImage = SelectedItem.ImageUrl;
            }
            else
            {
                var adjustedIndex = SelectedItem?.ImageUrl != null ? CurrentImageIndex - 1 : CurrentImageIndex;
                if (adjustedIndex >= 0 && adjustedIndex < ItemImages.Count)
                {
                    CurrentDisplayImage = ItemImages[adjustedIndex].ImageUrl;
                }
                else if (SelectedItem?.ImageUrl != null)
                {
                    CurrentDisplayImage = SelectedItem.ImageUrl;
                }
                else if (ItemImages.Count > 0)
                {
                    CurrentDisplayImage = ItemImages[0].ImageUrl;
                }
                else
                {
                    CurrentDisplayImage = null;
                }
            }
        }
        
        public int TotalImages => (SelectedItem?.ImageUrl != null ? 1 : 0) + ItemImages.Count;
        
        [RelayCommand]
        private void PreviousImage()
        {
            if (TotalImages <= 1) return;
            
            CurrentImageIndex = (CurrentImageIndex - 1 + TotalImages) % TotalImages;
            UpdateCurrentDisplayImage();
        }
        
        [RelayCommand]
        private void NextImage()
        {
            if (TotalImages <= 1) return;
            
            CurrentImageIndex = (CurrentImageIndex + 1) % TotalImages;
            UpdateCurrentDisplayImage();
        }
        
        public void SelectGalleryImage(ItemImage clickedImage)
        {
            var index = ItemImages.IndexOf(clickedImage);
            if (index >= 0)
            {
                CurrentImageIndex = SelectedItem?.ImageUrl != null ? index + 1 : index;
                UpdateCurrentDisplayImage();
            }
        }
        
        public void SelectMainImage()
        {
            CurrentImageIndex = 0;
            UpdateCurrentDisplayImage();
        }
        
        [RelayCommand]
        private async Task AddItemImage(string imageUrl)
        {
            if (SelectedItem == null || string.IsNullOrWhiteSpace(imageUrl)) return;
            
            try
            {
                var image = await _service.AddItemImageAsync(SelectedItem.Id, imageUrl, ItemImages.Count);
                ItemImages.Add(image);
                OnPropertyChanged(nameof(TotalImages));
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }
        
        [RelayCommand]
        private async Task DeleteCurrentImage()
        {
            if (CurrentImageIndex == 0 && SelectedItem?.ImageUrl != null) return;
            
            var adjustedIndex = SelectedItem?.ImageUrl != null ? CurrentImageIndex - 1 : CurrentImageIndex;
            if (adjustedIndex >= 0 && adjustedIndex < ItemImages.Count)
            {
                var image = ItemImages[adjustedIndex];
                await _service.DeleteItemImageAsync(image.Id);
                ItemImages.RemoveAt(adjustedIndex);
                CurrentImageIndex = 0;
                UpdateCurrentDisplayImage();
                OnPropertyChanged(nameof(TotalImages));
            }
        }
        
        [RelayCommand]
        private async Task GoBack()
        {
            if (_navigationHistory.Count > 0)
            {
                var previousItem = _navigationHistory.Pop();
                await ViewItemDetails(previousItem);
                return;
            }

            if (_onBackOverride != null)
            {
                var cb = _onBackOverride;
                _onBackOverride = null;
                IsViewingDetails = false;
                IsAddingItem = false;
                IsEditingItem = false;
                SelectedItem = null;
                cb();
                return;
            }

            IsViewingDetails = false;
            IsAddingItem = false;
            IsEditingItem = false;
            SelectedItem = null;
        }

        [RelayCommand]
        public void BackToList()
        {
            _navigationHistory.Clear();
            _onBackOverride = null;
            IsViewingDetails = false;
            IsAddingItem = false;
            IsEditingItem = false;
            SelectedItem = null;
            foreach (var tab in OpenTabs)
                tab.IsActive = false;
        }
        
        [RelayCommand]
        private async Task StartEditItem()
        {
            if (SelectedItem == null) return;
            
            var freshItem = await _service.GetItemByIdAsync(SelectedItem.Id);
            if (freshItem != null)
            {
                SelectedItem = freshItem;
            }
            
            // Populate all fields BEFORE showing the edit form to avoid mid-setup flicker
            _imagesChangedDuringEdit = false;
            RelatedItemsSearchText = string.Empty;
            RelatedItemsFilterType = null;
            RelatedItemsSearchByName = false;
            RelatedItemsSearchByTheme = false;
            RelatedSubtypeFilters.Clear();
            SelectableItems = new ObservableCollection<SelectableItem>();

            NewItemName = SelectedItem.Name;
            NewItemSentiments = SelectedItem.Sentiments ?? string.Empty;
            NewItemImageUrl = SelectedItem.ImageUrl ?? string.Empty;
            NewItemPrice = SelectedItem.Price;
            NewItemDatePurchased = SelectedItem.DatePurchased;
            NewItemNumber = SelectedItem.ItemNumber ?? string.Empty;
            NewItemIsDiscontinued = SelectedItem.IsDiscontinued;
            NewItemStencilLayers = SelectedItem.StencilLayers;
            NewItemPackSize = SelectedItem.PackSize;
            NewItemCurrentStock = SelectedItem.CurrentStock;
            NewItemPurchasedFrom = SelectedItem.PurchasedFrom ?? string.Empty;
            NewItemNotes = SelectedItem.Notes ?? string.Empty;
            NewItemSiteUrl = SelectedItem.SiteUrl ?? string.Empty;
            // Build the full image list before assigning to avoid per-Add re-renders
            var editImages = new List<string>();
            if (!string.IsNullOrEmpty(SelectedItem.ImageUrl))
                editImages.Add(SelectedItem.ImageUrl);
            foreach (var img in ItemImages)
                editImages.Add(img.ImageUrl);
            NewItemImages = new ObservableCollection<string>(editImages);
            
            // Set Type and Location LAST so ComboBox ItemsSource is already stable
            var typeInList = ItemTypes.FirstOrDefault(t => t == SelectedItem.Type) ?? SelectedItem.Type;
            // If the item's type isn't in ItemTypes (e.g. legacy/DB-only type), add it so the
            // ComboBox can find it. Without this, SelectedItem binding pushes null back via
            // two-way binding, blanking the field and saving null on the next Save.
            if (!string.IsNullOrEmpty(typeInList) && !ItemTypes.Contains(typeInList))
                ItemTypes.Add(typeInList);

            var locationInList = !string.IsNullOrEmpty(SelectedItem.Location)
                ? (ItemLocations.FirstOrDefault(l => l == SelectedItem.Location) ?? SelectedItem.Location)
                : string.Empty;
            if (!string.IsNullOrEmpty(locationInList) && !ItemLocations.Contains(locationInList))
                ItemLocations.Add(locationInList);

            // Always notify, even if the value didn't change. CommunityToolkit skips the
            // setter when the value is identical, but the ComboBox may need to re-sync
            // its SelectedItem after transitioning from a previous edit session.
            if (NewItemType == typeInList)
                OnPropertyChanged(nameof(NewItemType));
            else
                NewItemType = typeInList;
            if (NewItemLocation == locationInList)
                OnPropertyChanged(nameof(NewItemLocation));
            else
                NewItemLocation = locationInList;
            
            InitializeThemeCheckboxes();
            SetThemeCheckboxesFromString(SelectedItem.Theme);
            LoadSubtypeCheckboxes(SelectedItem.Type);
            SetSubtypeCheckboxesFromString(SelectedItem.Subtype);

            // All fields ready - now switch view so edit form renders fully populated
            IsViewingDetails = false;
            IsEditingItem = true;

            await LoadSelectableItemsForEditAsync();
        }
        
        private async Task LoadSelectableItemsForEditAsync()
        {
            if (SelectedItem == null) return;
            
            var currentItemId = SelectedItem.Id;
            
            if (_itemLookupList.Count == 0)
            {
                var allItems = _cachedItemsList ?? await _service.GetItemsAsync();
                if (_cachedItemsList == null) _cachedItemsList = allItems;
                _itemLookupList = allItems.Select(i => new ItemLookupEntry
                {
                    Id = i.Id, Name = i.Name, Type = i.Type, ItemNumber = i.ItemNumber, Subtype = i.Subtype,
                    Theme = i.Theme
                }).ToList();
            }
            
            var existingRelated = await _service.GetRelatedItemsAsync(currentItemId);
            var relatedIds = existingRelated.Select(r => r.Id).ToHashSet();
            
            var selectableList = _itemLookupList
                .Where(i => i.Id != currentItemId)
                .Select(i => new SelectableItem
                {
                    ItemId = i.Id,
                    ItemName = i.Name,
                    ItemType = i.Type,
                    ItemNumber = i.ItemNumber,
                    ItemSubtype = i.Subtype,
                    ItemTheme = i.Theme,
                    IsSelected = relatedIds.Contains(i.Id),
                    RelatedCount = 0,
                    SelectionChanged = OnSelectableItemSelectionChanged
                })
                .ToList();
            
            SelectableItems = new ObservableCollection<SelectableItem>(selectableList);
            OnPropertyChanged(nameof(SelectedRelatedItems));
            OnPropertyChanged(nameof(HasSelectedRelatedItems));
        }
        
        [RelayCommand]
        private async Task OpenCropTool()
        {
            if (SelectedItem == null) return;
            
            try
            {
                IsLoading = true;
                
                var allImages = new List<ItemImage>();
                
                if (!string.IsNullOrEmpty(SelectedItem.ImageUrl))
                {
                    allImages.Add(new ItemImage
                    {
                        Id = 0,
                        ItemId = SelectedItem.Id,
                        ImageUrl = SelectedItem.ImageUrl,
                        SortOrder = 0
                    });
                }
                
                var additionalImages = await _service.GetItemImagesAsync(SelectedItem.Id);
                allImages.AddRange(additionalImages.OrderBy(i => i.SortOrder));
                
                if (allImages.Count == 0)
                {
                    ErrorMessage = "No images found for this item. Save the item with images first.";
                    return;
                }
                
                var sentimentsList = SentimentService.ParseSentimentsList(NewItemSentiments);

                // Hide sentiments that already have a saved snip for this item — no
                // point offering them again. Compare case-insensitively on trimmed
                // text since the dropdown matches the same way before removing rows
                // mid-session (SentimentCropView.SaveButton_Click).
                var existingSnips = await _sentimentService.GetSentimentsByItemIdAsync(SelectedItem.Id);
                var alreadyClipped = new HashSet<string>(
                    existingSnips
                        .Select(s => (s.ExtractedText ?? string.Empty).Trim())
                        .Where(t => t.Length > 0),
                    StringComparer.OrdinalIgnoreCase);
                if (alreadyClipped.Count > 0)
                {
                    sentimentsList = sentimentsList
                        .Where(s => !alreadyClipped.Contains(s.Trim()))
                        .ToList();
                }

                var cropWindow = new SentimentCropView(SelectedItem, allImages, sentimentsList);
                cropWindow.Owner = System.Windows.Application.Current.MainWindow;
                cropWindow.ShowDialog();

                // Crop view saves directly to sentiment_images via SelectedItem.Id, so
                // anything added (or deleted via the "Saved this session" strip) is
                // already in the DB. Refresh the in-memory collection so the
                // "Sentiments from this Set" card on the Item Detail view picks them
                // up — important when the user cancels the Edit form afterwards, since
                // CancelEdit doesn't trigger a re-load.
                if (cropWindow.SentimentSaved && SelectedItem != null)
                {
                    await LoadSentimentImages(SelectedItem.Id);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error opening crop tool: {ex.Message}";
                LoggingService.LogError(ex);
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        [RelayCommand]
        private async Task UpdateItem()
        {
            if (SelectedItem == null) return;

            ClearFieldErrors();

            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(NewItemName))
            {
                NewItemNameHasError = true;
                errors.Add("Item name is required");
            }
            if (NewItemPrice.HasValue && NewItemPrice.Value > 10000)
            {
                NewItemPriceHasError = true;
                NewItemPriceErrorMessage = "Price seems unusually high. Please verify.";
                errors.Add(NewItemPriceErrorMessage);
            }
            if (NewItemDatePurchased.HasValue && NewItemDatePurchased.Value > DateTime.Today)
            {
                NewItemDatePurchasedHasError = true;
                NewItemDatePurchasedErrorMessage = "Purchase date cannot be in the future";
                errors.Add(NewItemDatePurchasedErrorMessage);
            }
            if (errors.Count > 0)
            {
                ErrorMessage = string.Join(" · ", errors);
                return;
            }
            
            IsLoading = true;
            ErrorMessage = string.Empty;
            try
            {
                SelectedItem.Name = NewItemName;
                SelectedItem.Type = string.IsNullOrEmpty(NewItemType) ? SelectedItem.Type : NewItemType;
                SelectedItem.Location = string.IsNullOrWhiteSpace(NewItemLocation) ? null : NewItemLocation;
                SelectedItem.Theme = string.IsNullOrWhiteSpace(NewItemTheme) ? null : NewItemTheme;
                SelectedItem.Sentiments = string.IsNullOrWhiteSpace(NewItemSentiments) ? null : NewItemSentiments;
                SelectedItem.ImageUrl = string.IsNullOrWhiteSpace(NewItemImageUrl) ? null : NewItemImageUrl;
                SelectedItem.Price = NewItemPrice;
                SelectedItem.DatePurchased = NewItemDatePurchased;
                SelectedItem.ItemNumber = string.IsNullOrWhiteSpace(NewItemNumber) ? null : NewItemNumber;
                SelectedItem.IsDiscontinued = NewItemIsDiscontinued;
                SelectedItem.StencilLayers = NewItemStencilLayers;
                SelectedItem.PackSize = NewItemPackSize;
                SelectedItem.CurrentStock = NewItemCurrentStock;
                SelectedItem.PurchasedFrom = string.IsNullOrWhiteSpace(NewItemPurchasedFrom) ? null : NewItemPurchasedFrom;
                SelectedItem.Notes = string.IsNullOrWhiteSpace(NewItemNotes) ? null : NewItemNotes;
                SelectedItem.SiteUrl = string.IsNullOrWhiteSpace(NewItemSiteUrl) ? null : NewItemSiteUrl;
                var subtypeValue = GetSelectedSubtype();
                SelectedItem.Subtype = string.IsNullOrEmpty(subtypeValue) ? null : subtypeValue;
                
                await _service.UpdateItemAsync(SelectedItem);
                
                if (_imagesChangedDuringEdit)
                {
                    await _service.ClearItemImagesAsync(SelectedItem.Id);
                    for (int i = 1; i < NewItemImages.Count; i++)
                    {
                        await _service.AddItemImageAsync(SelectedItem.Id, NewItemImages[i], i);
                    }
                    
                }
                
                var selectedRelatedIds = SelectableItems.Where(s => s.IsSelected).Select(s => s.ItemId).ToList();
                await _service.SetItemRelationshipsAsync(SelectedItem.Id, selectedRelatedIds);
                
                IsEditingItem = false;
                SelectableItems = new ObservableCollection<SelectableItem>();
                
                UpdateItemLookup(SelectedItem.Id, NewItemName, NewItemType ?? string.Empty, NewItemNumber);
                ThumbnailCacheService.InvalidateItem(SelectedItem.Id);
                
                var updatedItemId = SelectedItem.Id;
                if (_cachedItemsList != null)
                {
                    var cachedItem = _cachedItemsList.FirstOrDefault(i => i.Id == updatedItemId);
                    if (cachedItem != null)
                    {
                        cachedItem.Name = SelectedItem.Name;
                        cachedItem.Type = SelectedItem.Type;
                        cachedItem.Location = SelectedItem.Location;
                        cachedItem.Theme = SelectedItem.Theme;
                        cachedItem.Sentiments = SelectedItem.Sentiments;
                        cachedItem.ImageUrl = SelectedItem.ImageUrl;
                        cachedItem.Price = SelectedItem.Price;
                        cachedItem.DatePurchased = SelectedItem.DatePurchased;
                        cachedItem.ItemNumber = SelectedItem.ItemNumber;
                        cachedItem.IsDiscontinued = SelectedItem.IsDiscontinued;
                        cachedItem.StencilLayers = SelectedItem.StencilLayers;
                        // Keep the rest of the editable fields in sync too, or a
                        // later cache-served reload would show stale values.
                        cachedItem.Subtype = SelectedItem.Subtype;
                        cachedItem.PackSize = SelectedItem.PackSize;
                        cachedItem.CurrentStock = SelectedItem.CurrentStock;
                        cachedItem.PurchasedFrom = SelectedItem.PurchasedFrom;
                        cachedItem.Notes = SelectedItem.Notes;
                        cachedItem.SiteUrl = SelectedItem.SiteUrl;
                    }
                }
                var listItem = Items.FirstOrDefault(i => i.Id == updatedItemId);
                if (listItem != null)
                {
                    var idx = Items.IndexOf(listItem);
                    Items[idx] = SelectedItem;
                }
                
                await ViewItemDetails(SelectedItem);
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex);
                ErrorMessage = $"Failed to update item: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        [RelayCommand]
        private void CancelEdit()
        {
            IsEditingItem = false;
            SelectableItems = new ObservableCollection<SelectableItem>();
            if (SelectedItem != null)
            {
                IsViewingDetails = true;
            }
        }
        
        [RelayCommand]
        private void StartEditStock()
        {
            if (SelectedItem == null) return;
            EditStockValue = SelectedItem.CurrentStock ?? 0;
            IsEditingStock = true;
        }

        [RelayCommand]
        private void CancelEditStock()
        {
            IsEditingStock = false;
        }

        [RelayCommand]
        private async Task SaveEditStock()
        {
            if (SelectedItem == null) return;
            try
            {
                await _service.UpdateItemStockAsync(SelectedItem.Id, EditStockValue);
                SelectedItem.CurrentStock = EditStockValue;
                OnPropertyChanged(nameof(SelectedItem));
                IsEditingStock = false;
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex);
                ErrorMessage = $"Failed to update stock: {ex.Message}";
            }
        }

        public bool IsTrackedItem => SelectedItem != null && InventoryService.IsTrackedType(SelectedItem.Type);

        public string StockUnitLabel
        {
            get
            {
                if (SelectedItem == null) return "units";
                var type = SelectedItem.Type;
                if (InventoryService.IsEnvelopeType(type)) return "envelopes";
                if (type.Contains("Card Base", StringComparison.OrdinalIgnoreCase)) return "card bases";
                if (type.Contains("Cardstock", StringComparison.OrdinalIgnoreCase) ||
                    type.Contains("Watercolor", StringComparison.OrdinalIgnoreCase)) return "sheets";
                if (type.Contains("Foil-it", StringComparison.OrdinalIgnoreCase)) return "foil-its";
                if (type.Contains("Foil", StringComparison.OrdinalIgnoreCase)) return "foils";
                return "units";
            }
        }

        [RelayCommand]
        private void StartAddPurchase()
        {
            IsAddingPurchase = true;
            NewPurchaseQuantity = 1;
            NewPurchasePrice = null;
            NewPurchaseDate = DateTime.Today;
        }
        
        [RelayCommand]
        private void CancelAddPurchase()
        {
            IsAddingPurchase = false;
        }
        
        [RelayCommand]
        private async Task SavePurchase()
        {
            if (SelectedItem == null) return;

            if (NewPurchaseQuantity <= 0)
            {
                PurchaseErrorMessage = "Quantity must be at least 1.";
                return;
            }
            if ((NewPurchasePrice ?? 0) < 0)
            {
                PurchaseErrorMessage = "Price cannot be negative.";
                return;
            }

            try
            {
                var itemId = SelectedItem.Id;
                var result = await _service.AddItemPurchaseAsync(
                    itemId, 
                    NewPurchaseQuantity, 
                    NewPurchasePrice ?? 0, 
                    NewPurchaseDate
                );
                
                // Update totals from the returned result
                TotalQuantity = result.TotalQuantity;
                TotalSpend = result.TotalSpend;
                
                // Replace SelectedItem with fresh instance to trigger binding updates
                if (result.UpdatedItem != null)
                {
                    // Update item in the Items collection
                    var index = Items.ToList().FindIndex(i => i.Id == itemId);
                    if (index >= 0)
                    {
                        Items[index] = result.UpdatedItem;
                    }
                    SelectedItem = result.UpdatedItem;
                }
                
                // Reload purchases list
                var purchases = await _service.GetItemPurchasesAsync(itemId);
                ItemPurchases = new ObservableCollection<ItemPurchase>(purchases);
                
                IsAddingPurchase = false;
                
                // Reset form
                NewPurchaseQuantity = 1;
                NewPurchasePrice = null;
                NewPurchaseDate = DateTime.Today;
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex);
                ErrorMessage = $"Failed to save purchase: {ex.Message}";
            }
        }
        
        [RelayCommand]
        private async Task DeletePurchase(ItemPurchase purchase)
        {
            try
            {
                await _service.DeleteItemPurchaseAsync(purchase.Id);
                await LoadItemPurchases();
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex);
            }
        }

        // ── Sales ────────────────────────────────────────────────────────────

        /// <summary>Opens the "record a sale" form. When invoked from a specific
        /// purchase row the sale is pre-filled from that purchase (quantity + the
        /// price paid) so selling a previously purchased item is one click.</summary>
        [RelayCommand]
        private void StartAddSale(ItemPurchase? fromPurchase = null)
        {
            SaleErrorMessage = null;
            NewSaleQuantity = fromPurchase?.Quantity ?? 1;
            NewSalePrice = fromPurchase?.PricePerItem;
            NewSaleDate = DateTime.Today;
            IsAddingSale = true;
        }

        [RelayCommand]
        private void CancelAddSale()
        {
            IsAddingSale = false;
        }

        [RelayCommand]
        private async Task SaveSale()
        {
            if (SelectedItem == null) return;

            if (NewSaleQuantity <= 0)
            {
                SaleErrorMessage = "Quantity must be at least 1.";
                return;
            }
            if ((NewSalePrice ?? 0) < 0)
            {
                SaleErrorMessage = "Sale price cannot be negative.";
                return;
            }
            // Block overselling a tracked item: it would clamp stock to 0 and make
            // the sale non-reversible (deleting it would restore phantom stock).
            if (InventoryService.IsTrackedType(SelectedItem.Type)
                && SelectedItem.PackSize is int ps && ps > 0
                && (SelectedItem.CurrentStock ?? 0) < NewSaleQuantity * ps)
            {
                SaleErrorMessage = $"Not enough stock — {SelectedItem.CurrentStock ?? 0} in stock, this sale needs {NewSaleQuantity * ps}.";
                return;
            }

            try
            {
                var itemId = SelectedItem.Id;
                var result = await _service.AddItemSaleAsync(
                    itemId,
                    NewSaleQuantity,
                    NewSalePrice ?? 0,
                    NewSaleDate
                );

                TotalSold = result.TotalQuantity;
                TotalRevenue = result.TotalRevenue;

                // Selling can change stock (tracked types); refresh the item so the
                // detail view's stock badge updates.
                if (result.UpdatedItem != null)
                {
                    var index = Items.ToList().FindIndex(i => i.Id == itemId);
                    if (index >= 0)
                    {
                        Items[index] = result.UpdatedItem;
                    }
                    SelectedItem = result.UpdatedItem;
                }

                var sales = await _service.GetItemSalesAsync(itemId);
                ItemSales = new ObservableCollection<ItemSale>(sales);

                IsAddingSale = false;
                NewSaleQuantity = 1;
                NewSalePrice = null;
                NewSaleDate = DateTime.Today;
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex);
                SaleErrorMessage = $"Failed to record sale: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task DeleteSale(ItemSale sale)
        {
            try
            {
                await _service.DeleteItemSaleAsync(sale.Id);
                // Stock is restored server-side; refresh the item + list.
                if (SelectedItem != null)
                {
                    var refreshed = await _service.GetItemByIdAsync(SelectedItem.Id);
                    if (refreshed != null)
                    {
                        var index = Items.ToList().FindIndex(i => i.Id == refreshed.Id);
                        if (index >= 0) Items[index] = refreshed;
                        SelectedItem = refreshed;
                    }
                }
                await LoadItemSales();
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex);
            }
        }
        
        public async Task StartAddItem()
        {
            // Reset the form BEFORE making it visible. If IsAddingItem is set first,
            // WPF immediately re-activates the ComboBox bindings while NewItemType
            // still holds the previous session's value, and the ComboBox shows the
            // wrong type. Resetting while the form is still Collapsed guarantees
            // the ComboBox initialises from the correct default when it becomes Visible.
            IsViewingDetails = false;
            RelatedItemsSearchText = string.Empty;
            ResetNewItemForm();
            IsAddingItem = true;
            await LoadSelectableItems();
        }
        
        private async Task LoadSelectableItems()
        {
            if (_itemLookupList.Count == 0)
            {
                var allItems = _cachedItemsList ?? await _service.GetItemsAsync();
                if (_cachedItemsList == null) _cachedItemsList = allItems;
                _itemLookupList = allItems.Select(i => new ItemLookupEntry
                {
                    Id = i.Id, Name = i.Name, Type = i.Type, ItemNumber = i.ItemNumber, Subtype = i.Subtype,
                    Theme = i.Theme
                }).ToList();
            }
            
            SelectableItems = new ObservableCollection<SelectableItem>(
                _itemLookupList.Select(i => new SelectableItem
                {
                    ItemId = i.Id,
                    ItemName = i.Name,
                    ItemType = i.Type,
                    ItemNumber = i.ItemNumber,
                    ItemSubtype = i.Subtype,
                    ItemTheme = i.Theme,
                    IsSelected = false,
                    SelectionChanged = OnSelectableItemSelectionChanged
                })
            );
            OnPropertyChanged(nameof(SelectedRelatedItems));
            OnPropertyChanged(nameof(HasSelectedRelatedItems));
        }
        
        private void UpdateItemLookup(int id, string name, string type, string? itemNumber)
        {
            var existing = _itemLookupList.FirstOrDefault(l => l.Id == id);
            if (existing != null)
            {
                existing.Name = name;
                existing.Type = type;
                existing.ItemNumber = itemNumber;
            }
        }
        
        private void ResetNewItemForm()
        {
            NewItemName = string.Empty;
            // Force the Type ComboBox to clear visually by routing through null.
            // When bound to a string SelectedItem and the form is already on screen
            // (Save & add another), assigning string.Empty alone leaves the previous
            // text on the ComboBox face — which makes users think the type is still
            // set and they hit Save again with NewItemType actually empty.
            NewItemType = null!;
            NewItemType = string.Empty;
            LoadSubtypeCheckboxes(string.Empty);
            var defaultLocation = UserSettingsService.Current.DefaultLocation;
            var rawLocation = string.IsNullOrEmpty(defaultLocation) ? string.Empty : defaultLocation;
            NewItemLocation = string.IsNullOrEmpty(rawLocation) ? string.Empty : (ItemLocations.FirstOrDefault(l => l == rawLocation) ?? rawLocation);
            NewItemTheme = string.Empty;
            InitializeThemeCheckboxes();
            NewItemSentiments = string.Empty;
            NewItemImageUrl = string.Empty;
            NewItemImages = new ObservableCollection<string>();
            NewItemPrice = null;
            NewItemDatePurchased = null;
            NewItemNumber = string.Empty;
            NewItemIsDiscontinued = false;
            NewItemStencilLayers = null;
            NewItemPackSize = null;
            NewItemCurrentStock = null;
            NewItemPurchasedFrom = string.Empty;
            NewItemNotes = string.Empty;
            NewItemSiteUrl = string.Empty;
            DuplicateItemWarning = null;
            DuplicateItemId = null;
            // Clear any snips captured in a prior Add session so they can't leak
            // onto an unrelated item on the next save.
            PendingSnips.Clear();
            ClearFieldErrors();
        }
        
        public void AddNewItemImage(string base64Image)
        {
            NewItemImages.Add(base64Image);
            _imagesChangedDuringEdit = true;
            if (string.IsNullOrEmpty(NewItemImageUrl))
            {
                NewItemImageUrl = base64Image;
            }
        }
        
        public void RemoveNewItemImage(string base64Image)
        {
            NewItemImages.Remove(base64Image);
            _imagesChangedDuringEdit = true;
            if (NewItemImageUrl == base64Image && NewItemImages.Count > 0)
            {
                NewItemImageUrl = NewItemImages[0];
            }
            else if (NewItemImages.Count == 0)
            {
                NewItemImageUrl = string.Empty;
            }
        }
        

        [RelayCommand]
        private void CropSentimentsDuringAdd()
        {
            if (NewItemImages == null || NewItemImages.Count == 0)
            {
                ErrorMessage = "Please add at least one image before opening the crop tool.";
                return;
            }

            ErrorMessage = string.Empty;

            // Build image list from the current in-form images
            var allImages = new List<ItemImage>();
            for (int i = 0; i < NewItemImages.Count; i++)
            {
                allImages.Add(new ItemImage
                {
                    Id = i,
                    ItemId = 0,
                    ImageUrl = NewItemImages[i],
                    SortOrder = i
                });
            }

            var sentimentsList = SentimentService.ParseSentimentsList(NewItemSentiments);

            var cropWindow = new MyCraftyStash.Views.SentimentCropView(allImages, sentimentsList);
            cropWindow.Owner = System.Windows.Application.Current.MainWindow;
            cropWindow.ShowDialog();

            // Absorb any draft snips the user created
            if (cropWindow.DraftSnips.Count > 0)
            {
                PendingSnips.AddRange(cropWindow.DraftSnips);
                // Append any new sentiment text lines to the textbox
                var existingLines = SentimentService.ParseSentimentsList(NewItemSentiments);
                foreach (var snip in cropWindow.DraftSnips)
                    if (!existingLines.Contains(snip.Text, StringComparer.OrdinalIgnoreCase))
                        existingLines.Add(snip.Text);
                NewItemSentiments = string.Join(Environment.NewLine, existingLines);
            }
        }

        [RelayCommand]
        private Task SaveItem() => SaveItemCoreAsync(keepFormOpen: false);

        [RelayCommand]
        private Task SaveItemAndAddAnother() => SaveItemCoreAsync(keepFormOpen: true);

        private async Task SaveItemCoreAsync(bool keepFormOpen)
        {
            ClearFieldErrors();

            // Flag every failing field in one pass so the user sees all the badges
            // at once instead of fixing one error and triggering another on retry.
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(NewItemName))
            {
                NewItemNameHasError = true;
                errors.Add("Item name is required");
            }
            if (string.IsNullOrWhiteSpace(NewItemType))
            {
                NewItemTypeHasError = true;
                errors.Add("Item type is required");
            }
            if (NewItemPrice.HasValue && NewItemPrice.Value > 10000)
            {
                NewItemPriceHasError = true;
                NewItemPriceErrorMessage = "Price seems unusually high. Please verify.";
                errors.Add(NewItemPriceErrorMessage);
            }
            if (NewItemDatePurchased.HasValue && NewItemDatePurchased.Value > DateTime.Today)
            {
                NewItemDatePurchasedHasError = true;
                NewItemDatePurchasedErrorMessage = "Purchase date cannot be in the future";
                errors.Add(NewItemDatePurchasedErrorMessage);
            }
            if (errors.Count > 0)
            {
                ErrorMessage = string.Join(" · ", errors);
                return;
            }
            
            IsLoading = true;
            ErrorMessage = string.Empty;
            try
            {
                LoggingService.LogInfo($"Starting to save item: {NewItemName}");
                
                var item = new Item
                {
                    Name = NewItemName,
                    Type = NewItemType,
                    Location = string.IsNullOrWhiteSpace(NewItemLocation) ? null : NewItemLocation,
                    Theme = string.IsNullOrWhiteSpace(NewItemTheme) ? null : NewItemTheme,
                    Sentiments = string.IsNullOrWhiteSpace(NewItemSentiments) ? null : NewItemSentiments,
                    ImageUrl = string.IsNullOrWhiteSpace(NewItemImageUrl) ? null : NewItemImageUrl,
                    Price = NewItemPrice,
                    DatePurchased = NewItemDatePurchased,
                    ItemNumber = string.IsNullOrWhiteSpace(NewItemNumber) ? null : NewItemNumber,
                    IsDiscontinued = NewItemIsDiscontinued,
                    StencilLayers = NewItemStencilLayers,
                    Subtype = string.IsNullOrEmpty(GetSelectedSubtype()) ? null : GetSelectedSubtype(),
                    PackSize = NewItemPackSize,
                    CurrentStock = NewItemCurrentStock,
                    PurchasedFrom = string.IsNullOrWhiteSpace(NewItemPurchasedFrom) ? null : NewItemPurchasedFrom,
                    Notes = string.IsNullOrWhiteSpace(NewItemNotes) ? null : NewItemNotes,
                    SiteUrl = string.IsNullOrWhiteSpace(NewItemSiteUrl) ? null : NewItemSiteUrl
                };
                
                LoggingService.LogInfo($"Calling CreateItemAsync for: {item.Name}");
                var createdItem = await _service.CreateItemAsync(item);
                LoggingService.LogInfo($"Item created with ID: {createdItem.Id}");

                // Flush any pending sentiment snips captured during add.
                // Snapshot and clear BEFORE iterating so a failure mid-flush can't
                // strand stale snips that would later attach to a different item.
                if (PendingSnips.Count > 0)
                {
                    var snipsToFlush = PendingSnips.ToList();
                    PendingSnips.Clear();
                    var sentimentSvc = new SentimentService();
                    foreach (var snip in snipsToFlush)
                    {
                        try
                        {
                            await sentimentSvc.AddSentimentImageAsync(createdItem.Id, snip.ImageData, snip.Text);
                        }
                        catch (Exception snipEx)
                        {
                            LoggingService.LogError(snipEx, $"Failed to save sentiment snip for item {createdItem.Id}");
                        }
                    }
                }
                
                // Every new item gets an opening purchase-history entry (qty 1).
                // This seeds the "bought" side of the bought-vs-sold count that
                // drives the "Sold" card badge, and means the purchase history is
                // never empty. Price defaults to 0 when none was entered.
                await _service.AddItemPurchaseAsync(
                    createdItem.Id,
                    1, // Default quantity of 1 for new item
                    NewItemPrice ?? 0,
                    NewItemDatePurchased ?? DateTime.Today,
                    adjustStock: false // stock was already set on the item; don't double-count
                );
                LoggingService.LogInfo($"Opening purchase-history entry added for item: {createdItem.Id}");
                
                for (int i = 1; i < NewItemImages.Count; i++)
                {
                    await _service.AddItemImageAsync(createdItem.Id, NewItemImages[i], i);
                }
                
                var selectedRelatedIds = SelectableItems.Where(s => s.IsSelected).Select(s => s.ItemId).ToList();
                if (selectedRelatedIds.Any())
                {
                    await _service.SetItemRelationshipsAsync(createdItem.Id, selectedRelatedIds);
                }
                
                _itemLookupList.Add(new ItemLookupEntry
                {
                    Id = createdItem.Id,
                    Name = createdItem.Name,
                    Type = createdItem.Type,
                    ItemNumber = createdItem.ItemNumber,
                    Subtype = createdItem.Subtype
                });
                
                LoggingService.LogInfo($"Item save completed successfully");
                _cachedItemsList = null;
                _itemsAreDirty = true;

                if (keepFormOpen)
                {
                    // Save & add another: reset the form fields and refresh the
                    // selectable-related-items list so the new item is in it,
                    // but stay on the Add Item page.
                    ResetNewItemForm();
                    await LoadSelectableItems();
                }
                else
                {
                    IsAddingItem = false;
                    SelectableItems = new ObservableCollection<SelectableItem>();
                    await LoadItems();
                    _mainVM.NavigateToInventoryCommand.Execute(null);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex);
                ErrorMessage = $"Failed to save item: {ex.Message}";
                System.Windows.MessageBox.Show($"Error saving item: {ex.Message}\n\nCheck logs at: {LoggingService.GetLogFolderPath()}", 
                    "Save Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        [RelayCommand]
        private void CancelAdd()
        {
            IsAddingItem = false;
            PendingSnips.Clear();
            SelectableItems = new ObservableCollection<SelectableItem>();
            _mainVM.NavigateToInventoryCommand.Execute(null);
        }

        [RelayCommand]
        private void OpenCatalogLookup()
        {
            if (!CatalogLookupService.IsAvailable)
            {
                ErrorMessage = "Catalog lookup is unavailable (check your internet connection).";
                return;
            }

            var dialog = new ItemLookupDialog
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true && dialog.SelectedResult is { } result)
            {
                // Fill in the add form from the catalog result
                NewItemName       = result.Name;
                NewItemNumber     = result.ItemNumber ?? string.Empty;
                NewItemPrice      = result.Price;
                // Site URL — drives the one-click "open product page" repurchase flow.
                NewItemSiteUrl    = result.Url ?? string.Empty;

                // Match type to ItemTypes list if possible, otherwise use raw value
                if (!string.IsNullOrEmpty(result.Type))
                    NewItemType = ItemTypes.FirstOrDefault(t =>
                        string.Equals(t, result.Type, StringComparison.OrdinalIgnoreCase)) ?? result.Type;

                // Match theme if available
                if (!string.IsNullOrEmpty(result.Theme))
                    NewItemTheme = result.Theme;

                // Use catalog image as the primary image
                if (!string.IsNullOrEmpty(result.ImageUrl))
                {
                    NewItemImageUrl = result.ImageUrl;
                    NewItemImages = new ObservableCollection<string>(result.AllImages);
                }

                ErrorMessage = string.Empty;
            }
        }
        
        [RelayCommand]
        private async Task DeleteItem(Item item)
        {
            var result = MessageBox.Show(
                $"Are you sure you want to delete \"{item.Name}\"? This cannot be undone.",
                "Delete Item", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            IsLoading = true;
            try
            {
                await _service.DeleteItemAsync(item.Id);
                CloseTabForItem(item.Id);
                _itemLookupList.RemoveAll(l => l.Id == item.Id);
                ThumbnailCacheService.InvalidateItem(item.Id);
                _cachedItemsList = null;
                _itemsAreDirty = true;
                await LoadItems();
                BackToList();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        [RelayCommand]
        private async Task NavigateToRelatedItem(Item item)
        {
            if (SelectedItem != null)
            {
                _navigationHistory.Push(SelectedItem);
            }
            await ViewItemDetails(item);
        }

        /// <summary>Persists the Sentiments text of the currently selected item without a full reload.</summary>
        public async Task SaveSentimentTextAsync()
        {
            if (SelectedItem == null) return;
            await _service.UpdateItemAsync(SelectedItem);
        }
    }

    public partial class SelectableItem : ObservableObject
    {
        [ObservableProperty]
        private int _itemId;
        
        [ObservableProperty]
        private string _itemName = string.Empty;
        
        [ObservableProperty]
        private string _itemType = string.Empty;
        
        [ObservableProperty]
        private string? _itemNumber;

        [ObservableProperty]
        private string? _itemSubtype;

        [ObservableProperty]
        private string? _itemTheme;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private int _relatedCount;
        
        [ObservableProperty]
        private bool _isExpanded;

        public Action? SelectionChanged { get; set; }

        partial void OnIsSelectedChanged(bool value)
        {
            SelectionChanged?.Invoke();
        }

    }
}