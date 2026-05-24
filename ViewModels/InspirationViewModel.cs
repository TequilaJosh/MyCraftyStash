using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.IO;
using MyCraftyStash.Models;
using MyCraftyStash.Services;

namespace MyCraftyStash.ViewModels
{
    public partial class InspirationEntry : ObservableObject
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? BoardId { get; set; }

        [ObservableProperty]
        private string? _thumbnailUrl;

        [ObservableProperty]
        private bool _isOrgSelected;

        public bool ThumbnailLoaded => ThumbnailUrl != null;
    }

    public partial class SelectableStringItem : ObservableObject
    {
        public string Name { get; set; } = string.Empty;

        [ObservableProperty]
        private bool _isSelected;

        public Action? SelectionChanged { get; set; }

        partial void OnIsSelectedChanged(bool value) => SelectionChanged?.Invoke();
    }

    public partial class InspirationItemEntry : ObservableObject
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public string? ItemNumber { get; set; }
    }

    public partial class InspirationSelectableItem : ObservableObject
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public string? ItemNumber { get; set; }

        [ObservableProperty]
        private bool _isSelected;

        public Action? SelectionChanged { get; set; }

        partial void OnIsSelectedChanged(bool value)
        {
            SelectionChanged?.Invoke();
        }
    }

    public partial class SelectableBoardItem : ObservableObject
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        [ObservableProperty]
        private bool _isSelected;

        public Action? SelectionChanged { get; set; }

        partial void OnIsSelectedChanged(bool value) => SelectionChanged?.Invoke();
    }

    public class BoardEntry
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? ParentBoardId { get; set; }
        public int ImageCount { get; set; }
        public int ChildBoardCount { get; set; }
        public int CoverImageId { get; set; } // 0 = no cover
        public bool HasDefaults { get; set; }
        public string CountLabel => ImageCount == 1 ? "1 image"
            : ImageCount > 0 ? $"{ImageCount} images" : "No images";
        public string ChildLabel => ChildBoardCount == 1 ? "1 board"
            : ChildBoardCount > 0 ? $"{ChildBoardCount} boards" : "";
    }

    public class BreadcrumbEntry
    {
        public int? BoardId { get; set; } // null = root "All Images"
        public string Name { get; set; } = string.Empty;
        public bool IsLast { get; set; }
    }

    public class BoardTypeEntry
    {
        public string Type { get; set; } = string.Empty;
        public List<string> Subtypes { get; set; } = new();
        public int? SpecificItemId { get; set; }
        public string? SpecificItemName { get; set; }
        public string Display => SpecificItemId.HasValue
            ? $"{Type}: {SpecificItemName}"
            : (Subtypes.Count > 0 ? $"{Type} ({string.Join(", ", Subtypes)})" : Type);
    }

    public partial class InspirationViewModel : BaseViewModel
    {
        private readonly InventoryService _service;
        private readonly MainViewModel _mainVm;

        // ── Board navigation ─────────────────────────────────────────────────

        [ObservableProperty]
        private int? _currentBoardId; // null = root (all/uncategorized)

        [ObservableProperty]
        private string _currentBoardName = "Inspiration Station";

        [ObservableProperty]
        private ObservableCollection<BoardEntry> _currentBoards = new();

        [ObservableProperty]
        private ObservableCollection<BreadcrumbEntry> _breadcrumbPath = new();

        // ── Create / Edit board ──────────────────────────────────────────────

        [ObservableProperty]
        private bool _isCreatingBoard;

        [ObservableProperty]
        private bool _isEditingBoard;

        [ObservableProperty]
        private string _newBoardName = string.Empty;

        [ObservableProperty]
        private string _newBoardDescription = string.Empty;

        private int? _editingBoardId;

        public bool IsBoardModalOpen => IsCreatingBoard || IsEditingBoard;

        partial void OnIsCreatingBoardChanged(bool value)
        {
            OnPropertyChanged(nameof(IsBoardModalOpen));
            OnPropertyChanged(nameof(ShowBoardDefaults));
        }
        partial void OnIsEditingBoardChanged(bool value)
        {
            OnPropertyChanged(nameof(IsBoardModalOpen));
            OnPropertyChanged(nameof(ShowBoardDefaults));
        }

        // ── Board defaults (create / edit modal) ─────────────────────────────

        [ObservableProperty]
        private bool _newBoardSetDefaults;

        [ObservableProperty]
        private bool _hasExistingBoardDefaults;

        partial void OnNewBoardSetDefaultsChanged(bool value) => OnPropertyChanged(nameof(ShowBoardDefaults));
        partial void OnHasExistingBoardDefaultsChanged(bool value) => OnPropertyChanged(nameof(ShowBoardDefaults));

        /// <summary>Show the defaults section when checkbox is ticked OR board already has defaults.</summary>
        public bool ShowBoardDefaults => NewBoardSetDefaults || (IsEditingBoard && HasExistingBoardDefaults);

        [ObservableProperty]
        private ObservableCollection<SelectableStringItem> _newBoardDefaultTypeItems = new();

        [ObservableProperty]
        private bool _isDefaultTypePickerOpen;

        [ObservableProperty]
        private ObservableCollection<SelectableStringItem> _newBoardDefaultThemeItems = new();

        [ObservableProperty]
        private bool _isDefaultThemePickerOpen;

        [ObservableProperty]
        private ObservableCollection<SelectableStringItem> _newBoardDefaultColorItems = new();

        [ObservableProperty]
        private bool _isDefaultColorPickerOpen;

        [ObservableProperty]
        private ObservableCollection<SelectableStringItem> _newBoardDefaultTeColorItems = new();

        [ObservableProperty]
        private bool _isDefaultTeColorPickerOpen;

        [ObservableProperty]
        private string _newBoardDefaultSentiment = string.Empty;

        public string BoardDefaultTypesDisplay =>
            NewBoardDefaultTypeItems.Any(t => t.IsSelected)
                ? string.Join(", ", NewBoardDefaultTypeItems.Where(t => t.IsSelected).Select(t => t.Name))
                : "Select default types...";

        public string BoardDefaultThemesDisplay =>
            NewBoardDefaultThemeItems.Any(t => t.IsSelected)
                ? string.Join(", ", NewBoardDefaultThemeItems.Where(t => t.IsSelected).Select(t => t.Name))
                : "Select default themes...";

        public string BoardDefaultColorsDisplay =>
            NewBoardDefaultColorItems.Any(c => c.IsSelected)
                ? string.Join(", ", NewBoardDefaultColorItems.Where(c => c.IsSelected).Select(c => c.Name))
                : "Select default colors...";

        public string BoardDefaultTeColorsDisplay =>
            NewBoardDefaultTeColorItems.Any(c => c.IsSelected)
                ? string.Join(", ", NewBoardDefaultTeColorItems.Where(c => c.IsSelected).Select(c => c.Name))
                : "Select default TE colors...";

        // ── Board default type+subtype picker ─────────────────────────────────

        [ObservableProperty] private ObservableCollection<BoardTypeEntry> _boardTypeSubtypeList = new();
        [ObservableProperty] private bool _isBoardTypesPickerActive;
        [ObservableProperty] private bool _isAskingMoreBoardTypes;
        [ObservableProperty] private string? _boardPickerType;
        [ObservableProperty] private ObservableCollection<SubtypeCheckboxItem> _boardPickerSubtypes = new();
        [ObservableProperty] private ObservableCollection<WizardItemOption> _boardPickerItems = new();
        [ObservableProperty] private WizardItemOption? _boardPickerSelectedItem;

        public bool HasBoardPickerSubtypes => BoardPickerSubtypes.Count > 0;
        public bool HasBoardPickerItems => BoardPickerItems.Count > 0;
        public bool ShowAddBoardTypeButton => !IsBoardTypesPickerActive && !IsAskingMoreBoardTypes;
        public bool BoardPickerNeedsSpecificItem => !string.IsNullOrEmpty(BoardPickerType) && TypeNeedsSpecificItem(BoardPickerType);
        public bool CanConfirmBoardType => !string.IsNullOrEmpty(BoardPickerType) &&
            (!BoardPickerNeedsSpecificItem || BoardPickerSelectedItem != null);

        private static readonly HashSet<string> _specificItemTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Ink", "Cardstock", "Watercolor"
        };
        private static bool TypeNeedsSpecificItem(string type) => _specificItemTypes.Contains(type);

        // ── Organization mode ─────────────────────────────────────────────────

        [ObservableProperty]
        private bool _isOrganizing;

        private readonly HashSet<int> _orgSelectedIds = new();

        public int OrgSelectedCount => _orgSelectedIds.Count;

        public string OrgSelectedLabel => OrgSelectedCount == 1
            ? "1 image selected"
            : $"{OrgSelectedCount} images selected";

        [ObservableProperty]
        private int? _orgMoveTargetBoardId;

        // ── Move image ───────────────────────────────────────────────────────

        [ObservableProperty]
        private bool _isMovingImage;

        [ObservableProperty]
        private ObservableCollection<BoardEntry> _allBoardsFlat = new();

        // ── Image gallery ────────────────────────────────────────────────────

        [ObservableProperty]
        private ObservableCollection<InspirationEntry> _images = new();

        [ObservableProperty]
        private InspirationEntry? _selectedImage;

        [ObservableProperty]
        private string? _selectedImageFullUrl;

        [ObservableProperty]
        private ObservableCollection<InspirationItemEntry> _selectedImageItems = new();

        [ObservableProperty]
        private InspirationImage? _selectedImageDetail;

        [ObservableProperty]
        private bool _isImageDetailPopupOpen;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _isAddingImage;

        [ObservableProperty]
        private string? _newImageUrl;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string? _filterType;

        [ObservableProperty]
        private string? _filterTheme;

        [ObservableProperty]
        private ObservableCollection<InspirationSelectableItem> _addItemsList = new();

        [ObservableProperty]
        private string _addItemsSearchText = string.Empty;

        [ObservableProperty]
        private bool _isEditingItems;

        [ObservableProperty]
        private ObservableCollection<InspirationSelectableItem> _editItemsList = new();

        [ObservableProperty]
        private string _editItemsSearchText = string.Empty;

        // ── Add Item Used flow ────────────────────────────────────────────────

        [ObservableProperty] private bool _isAddingItemUsed;
        [ObservableProperty] private bool _isAskingMoreItems;
        [ObservableProperty] private string? _addItemType;
        [ObservableProperty] private ObservableCollection<SubtypeCheckboxItem> _addItemSubtypes = new();
        [ObservableProperty] private ObservableCollection<WizardItemOption> _addItemOptions = new();
        [ObservableProperty] private WizardItemOption? _addItemSelected;

        public bool HasAddItemSubtypes => AddItemSubtypes.Count > 0;
        public bool HasAddItemOptions => AddItemOptions.Count > 0;
        public bool CanConfirmAddItem => AddItemSelected != null;
        public bool ShowAddItemButton => !IsAddingItemUsed && !IsAskingMoreItems;

        // Board multi-select for "Add Image" form
        [ObservableProperty]
        private ObservableCollection<SelectableBoardItem> _newImageBoardItems = new();

        [ObservableProperty]
        private bool _isBoardPickerOpen;

        public string NewImageBoardsDisplay
        {
            get
            {
                var selected = NewImageBoardItems.Where(b => b.IsSelected).Select(b => b.Name).ToList();
                return selected.Count == 0 ? "No board (uncategorized)" : string.Join(", ", selected);
            }
        }

        // ── Add Image metadata fields ─────────────────────────────────────────

        [ObservableProperty]
        private string? _newImageTheme;

        [ObservableProperty]
        private string? _newImageSentiment;

        [ObservableProperty]
        private ObservableCollection<SelectableStringItem> _newImageColorItems = new();

        [ObservableProperty]
        private bool _isColorPickerOpen;

        [ObservableProperty]
        private ObservableCollection<SelectableStringItem> _newImageTeColorItems = new();

        [ObservableProperty]
        private bool _isTeColorPickerOpen;

        [ObservableProperty]
        private ObservableCollection<SelectableStringItem> _newImageTypeItems = new();

        [ObservableProperty]
        private bool _isTypePickerOpen;

        [ObservableProperty]
        private ObservableCollection<SelectableStringItem> _newImageThemeItems = new();

        [ObservableProperty]
        private bool _isThemePickerOpen;

        public string NewImageColorsDisplay
        {
            get
            {
                var selected = NewImageColorItems.Where(c => c.IsSelected).Select(c => c.Name).ToList();
                return selected.Count == 0 ? "Select colors..." : string.Join(", ", selected);
            }
        }

        public string NewImageTeColorsDisplay
        {
            get
            {
                var selected = NewImageTeColorItems.Where(c => c.IsSelected).Select(c => c.Name).ToList();
                return selected.Count == 0 ? "Select TE colors..." : string.Join(", ", selected);
            }
        }

        public string NewImageTypesDisplay
        {
            get
            {
                var selected = NewImageTypeItems.Where(t => t.IsSelected).Select(t => t.Name).ToList();
                return selected.Count == 0 ? "Select types..." : string.Join(", ", selected);
            }
        }

        public string NewImageThemesDisplay
        {
            get
            {
                var selected = NewImageThemeItems.Where(t => t.IsSelected).Select(t => t.Name).ToList();
                return selected.Count == 0 ? "Select themes..." : string.Join(", ", selected);
            }
        }

        private List<InspirationEntry> _allImages = new();

        public List<string> ItemTypes => _service.GetItemTypes();

        public List<string> ThemeOptions
        {
            get
            {
                var themes = InventoryViewModel.GetThemeOptions();
                return themes ?? new List<string>();
            }
        }

        public IEnumerable<InspirationSelectableItem> FilteredAddItems
        {
            get
            {
                if (string.IsNullOrWhiteSpace(AddItemsSearchText))
                    return AddItemsList;

                var search = AddItemsSearchText.ToLower();
                return AddItemsList.Where(s =>
                    s.ItemName.ToLower().Contains(search) ||
                    s.ItemType.ToLower().Contains(search) ||
                    (s.ItemNumber?.ToLower().Contains(search) ?? false));
            }
        }

        public IEnumerable<InspirationSelectableItem> SelectedAddItems =>
            AddItemsList.Where(s => s.IsSelected);

        public bool HasSelectedAddItems =>
            AddItemsList.Any(s => s.IsSelected);

        public IEnumerable<InspirationSelectableItem> FilteredEditItems
        {
            get
            {
                if (string.IsNullOrWhiteSpace(EditItemsSearchText))
                    return EditItemsList;

                var search = EditItemsSearchText.ToLower();
                return EditItemsList.Where(s =>
                    s.ItemName.ToLower().Contains(search) ||
                    s.ItemType.ToLower().Contains(search) ||
                    (s.ItemNumber?.ToLower().Contains(search) ?? false));
            }
        }

        public IEnumerable<InspirationSelectableItem> SelectedEditItems =>
            EditItemsList.Where(s => s.IsSelected);

        public InspirationViewModel(InventoryService service, MainViewModel mainVm)
        {
            _service = service;
            _mainVm = mainVm;
        }

        partial void OnAddItemsSearchTextChanged(string value) =>
            OnPropertyChanged(nameof(FilteredAddItems));

        partial void OnEditItemsSearchTextChanged(string value) =>
            OnPropertyChanged(nameof(FilteredEditItems));

        partial void OnIsAddingItemUsedChanged(bool value) => OnPropertyChanged(nameof(ShowAddItemButton));
        partial void OnIsAskingMoreItemsChanged(bool value) => OnPropertyChanged(nameof(ShowAddItemButton));
        partial void OnAddItemSelectedChanged(WizardItemOption? value) => OnPropertyChanged(nameof(CanConfirmAddItem));

        partial void OnIsBoardTypesPickerActiveChanged(bool value) => OnPropertyChanged(nameof(ShowAddBoardTypeButton));
        partial void OnIsAskingMoreBoardTypesChanged(bool value) => OnPropertyChanged(nameof(ShowAddBoardTypeButton));
        partial void OnBoardPickerSelectedItemChanged(WizardItemOption? value) => OnPropertyChanged(nameof(CanConfirmBoardType));

        partial void OnBoardPickerTypeChanged(string? value)
        {
            BoardPickerSubtypes.Clear();
            BoardPickerItems.Clear();
            BoardPickerSelectedItem = null;
            if (!string.IsNullOrEmpty(value))
            {
                var subs = UserSettingsService.GetSubtypesForType(value);
                foreach (var s in subs)
                {
                    var cb = new SubtypeCheckboxItem { Label = s };
                    if (TypeNeedsSpecificItem(value))
                        cb.PropertyChanged += (_, _) => _ = RefreshBoardPickerItemsAsync();
                    BoardPickerSubtypes.Add(cb);
                }
                if (TypeNeedsSpecificItem(value))
                    _ = RefreshBoardPickerItemsAsync();
            }
            OnPropertyChanged(nameof(HasBoardPickerSubtypes));
            OnPropertyChanged(nameof(HasBoardPickerItems));
            OnPropertyChanged(nameof(BoardPickerNeedsSpecificItem));
            OnPropertyChanged(nameof(CanConfirmBoardType));
        }

        partial void OnAddItemTypeChanged(string? value)
        {
            AddItemSubtypes.Clear();
            AddItemOptions.Clear();
            AddItemSelected = null;
            if (!string.IsNullOrEmpty(value))
            {
                var subs = UserSettingsService.GetSubtypesForType(value);
                foreach (var s in subs)
                {
                    var cb = new SubtypeCheckboxItem { Label = s };
                    cb.PropertyChanged += (_, _) => _ = RefreshAddItemOptionsAsync();
                    AddItemSubtypes.Add(cb);
                }
                _ = RefreshAddItemOptionsAsync();
            }
            OnPropertyChanged(nameof(HasAddItemSubtypes));
            OnPropertyChanged(nameof(HasAddItemOptions));
            OnPropertyChanged(nameof(CanConfirmAddItem));
        }

        partial void OnSearchTextChanged(string value) => _ = ApplyFiltersAsync();
        partial void OnFilterTypeChanged(string? value) => _ = ApplyFiltersAsync();
        partial void OnFilterThemeChanged(string? value) => _ = ApplyFiltersAsync();

        private async Task ApplyFiltersAsync()
        {
            try
            {
                if (_allImages.Count == 0)
                {
                    Images = new ObservableCollection<InspirationEntry>();
                    return;
                }

                bool hasTypeOrTheme = !string.IsNullOrWhiteSpace(FilterType) || !string.IsNullOrWhiteSpace(FilterTheme);

                if (!hasTypeOrTheme && string.IsNullOrWhiteSpace(SearchText))
                {
                    Images = new ObservableCollection<InspirationEntry>(_allImages);
                    return;
                }

                var filtered = _allImages.AsEnumerable();

                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    var search = SearchText.ToLower();
                    filtered = filtered.Where(i => i.Title?.ToLower().Contains(search) ?? false);
                }

                if (hasTypeOrTheme)
                {
                    try
                    {
                        var matchingImageIds = await _service.GetInspirationImageIdsByItemFilterAsync(FilterType, FilterTheme);
                        var matchSet = new HashSet<int>(matchingImageIds);
                        filtered = filtered.Where(i => matchSet.Contains(i.Id));
                    }
                    catch (Exception ex)
                    {
                        LoggingService.LogError(ex);
                    }
                }

                Images = new ObservableCollection<InspirationEntry>(filtered);
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "InspirationViewModel.ApplyFiltersAsync");
            }
        }

        // ── Board navigation ─────────────────────────────────────────────────

        [RelayCommand]
        public async Task LoadImages()
        {
            IsLoading = true;
            try
            {
                await LoadBoardViewAsync(CurrentBoardId);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadBoardViewAsync(int? boardId)
        {
            // Load child boards
            var boards = await _service.GetBoardsAtLevelAsync(boardId);
            var boardEntries = new List<BoardEntry>();
            foreach (var b in boards)
            {
                var (imgCount, childCount, coverId) = await _service.GetBoardStatsAsync(b.Id);
                boardEntries.Add(new BoardEntry
                {
                    Id = b.Id,
                    Name = b.Name,
                    Description = b.Description,
                    ParentBoardId = b.ParentBoardId,
                    ImageCount = imgCount,
                    ChildBoardCount = childCount,
                    CoverImageId = coverId,
                    HasDefaults = b.HasDefaults
                });
            }
            CurrentBoards = new ObservableCollection<BoardEntry>(boardEntries);

            // Load images in this board
            var images = await _service.GetImagesForBoardLightAsync(boardId);
            var entries = images.Select(img => new InspirationEntry
            {
                Id = img.Id,
                Title = img.Title,
                CreatedAt = img.CreatedAt,
                BoardId = img.BoardId
            }).ToList();

            _allImages = entries;
            InspirationThumbnailCacheService.PreloadAsync(entries.Select(e => e.Id));
            // Preload board cover images
            InspirationThumbnailCacheService.PreloadAsync(boardEntries
                .Where(b => b.CoverImageId > 0)
                .Select(b => b.CoverImageId));

            _ = ApplyFiltersAsync();

            // Update breadcrumb
            await UpdateBreadcrumbAsync(boardId);
        }

        private async Task UpdateBreadcrumbAsync(int? boardId)
        {
            var path = new List<BreadcrumbEntry>();

            if (boardId != null)
            {
                // Root entry is a clickable nav link back to the top level
                path.Add(new BreadcrumbEntry { BoardId = null, Name = "Inspiration Station", IsLast = false });

                var boardPath = await _service.GetBoardPathAsync(boardId.Value);
                for (int i = 0; i < boardPath.Count; i++)
                {
                    path.Add(new BreadcrumbEntry
                    {
                        BoardId = boardPath[i].Id,
                        Name = boardPath[i].Name,
                        IsLast = i == boardPath.Count - 1
                    });
                }
                CurrentBoardName = boardPath.LastOrDefault()?.Name ?? "Board";
            }
            else
            {
                CurrentBoardName = "Inspiration Station";
            }

            BreadcrumbPath = new ObservableCollection<BreadcrumbEntry>(path);
        }

        [RelayCommand]
        public async Task NavigateToBoard(int? boardId)
        {
            ClearSelection();
            IsMovingImage = false;
            CurrentBoardId = boardId;
            IsLoading = true;
            try
            {
                await LoadBoardViewAsync(boardId);
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ── Board CRUD ────────────────────────────────────────────────────────

        [RelayCommand]
        private void StartCreateBoard()
        {
            NewBoardName = string.Empty;
            NewBoardDescription = string.Empty;
            _editingBoardId = null;
            NewBoardSetDefaults = false;
            HasExistingBoardDefaults = false;
            InitBoardDefaultPickers(null);
            BoardTypeSubtypeList.Clear();
            IsBoardTypesPickerActive = false;
            IsAskingMoreBoardTypes = false;
            BoardPickerType = null;
            BoardPickerSubtypes.Clear();
            BoardPickerItems.Clear();
            BoardPickerSelectedItem = null;
            IsCreatingBoard = true;
            IsEditingBoard = false;
        }

        [RelayCommand]
        private async Task StartEditBoard(BoardEntry board)
        {
            NewBoardName = board.Name;
            NewBoardDescription = board.Description ?? string.Empty;
            _editingBoardId = board.Id;

            var fullBoard = await _service.GetBoardAsync(board.Id);
            HasExistingBoardDefaults = fullBoard?.HasDefaults ?? false;
            NewBoardSetDefaults = HasExistingBoardDefaults;
            InitBoardDefaultPickers(fullBoard);

            BoardTypeSubtypeList.Clear();
            IsBoardTypesPickerActive = false;
            IsAskingMoreBoardTypes = false;
            BoardPickerType = null;
            BoardPickerSubtypes.Clear();
            BoardPickerItems.Clear();
            BoardPickerSelectedItem = null;

            // Restore type+subtype entries ("Type:Sub1,Sub2|Type2" format)
            if (!string.IsNullOrEmpty(fullBoard?.DefaultSubtypes))
            {
                foreach (var entry in fullBoard.DefaultSubtypes.Split('|', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = entry.Split(':', 2);
                    var type = parts[0].Trim();
                    var subs = parts.Length > 1
                        ? parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList()
                        : new List<string>();
                    if (!string.IsNullOrEmpty(type))
                        BoardTypeSubtypeList.Add(new BoardTypeEntry { Type = type, Subtypes = subs });
                }
            }

            // Restore specific-item entries
            if (!string.IsNullOrEmpty(fullBoard?.DefaultItemIds))
            {
                var ids = fullBoard.DefaultItemIds.Split(',')
                    .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
                    .Where(id => id > 0);
                var loaded = await _service.GetWizardItemsByIdsAsync(ids);
                foreach (var item in loaded)
                    BoardTypeSubtypeList.Add(new BoardTypeEntry
                    {
                        Type = item.ItemType ?? "",
                        SpecificItemId = item.Id,
                        SpecificItemName = item.Name
                    });
            }

            IsEditingBoard = true;
            IsCreatingBoard = false;
        }

        [RelayCommand]
        private void CancelBoard()
        {
            IsCreatingBoard = false;
            IsEditingBoard = false;
            NewBoardName = string.Empty;
            NewBoardDescription = string.Empty;
            NewBoardSetDefaults = false;
            HasExistingBoardDefaults = false;
            _editingBoardId = null;
            IsDefaultTypePickerOpen = false;
            IsDefaultThemePickerOpen = false;
            IsDefaultColorPickerOpen = false;
            IsDefaultTeColorPickerOpen = false;
            BoardTypeSubtypeList.Clear();
            IsBoardTypesPickerActive = false;
            IsAskingMoreBoardTypes = false;
            BoardPickerType = null;
            BoardPickerSubtypes.Clear();
            BoardPickerItems.Clear();
            BoardPickerSelectedItem = null;
        }

        [RelayCommand]
        private async Task SaveBoard()
        {
            if (string.IsNullOrWhiteSpace(NewBoardName)) return;

            try
            {
                string? defaultTypes = null, defaultThemes = null, defaultColors = null,
                        defaultSentiment = null, defaultTeColors = null, defaultSubtypes = null;

                string? defaultItemIds = null;

                if (NewBoardSetDefaults)
                {
                    var typeEntries = BoardTypeSubtypeList.Where(e => !e.SpecificItemId.HasValue).ToList();
                    var itemEntries = BoardTypeSubtypeList.Where(e => e.SpecificItemId.HasValue).ToList();

                    if (typeEntries.Count > 0)
                        defaultSubtypes = string.Join("|", typeEntries.Select(e =>
                            e.Subtypes.Count > 0 ? $"{e.Type}:{string.Join(",", e.Subtypes)}" : e.Type));

                    if (itemEntries.Count > 0)
                        defaultItemIds = string.Join(",", itemEntries.Select(e => e.SpecificItemId!.Value));

                    // defaultTypes carries all type names (both paths) for cascade to image.Types
                    var allTypes = BoardTypeSubtypeList.Select(e => e.Type).Distinct().ToList();
                    if (allTypes.Count > 0)
                        defaultTypes = string.Join(",", allTypes);

                    defaultThemes = PickerSelected(NewBoardDefaultThemeItems);
                    defaultColors = PickerSelected(NewBoardDefaultColorItems);
                    defaultTeColors = PickerSelected(NewBoardDefaultTeColorItems);
                    defaultSentiment = string.IsNullOrWhiteSpace(NewBoardDefaultSentiment)
                        ? null : NewBoardDefaultSentiment.Trim();
                }

                if (IsEditingBoard && _editingBoardId.HasValue)
                {
                    await _service.UpdateBoardAsync(_editingBoardId.Value, NewBoardName, NewBoardDescription,
                        defaultTypes, defaultThemes, defaultColors, defaultSentiment, defaultTeColors, defaultSubtypes, defaultItemIds);

                    bool hasAny = defaultTypes != null || defaultThemes != null ||
                                  defaultColors != null || defaultSentiment != null ||
                                  defaultTeColors != null || defaultItemIds != null;
                    if (hasAny)
                    {
                        var boardDefaults = new Models.InspirationBoard
                        {
                            DefaultTypes = defaultTypes,
                            DefaultThemes = defaultThemes,
                            DefaultColors = defaultColors,
                            DefaultSentiment = defaultSentiment,
                            DefaultTeColors = defaultTeColors,
                            DefaultItemIds = defaultItemIds
                        };
                        await _service.CascadeApplyBoardDefaultsAsync(_editingBoardId.Value, boardDefaults);
                    }
                }
                else
                {
                    await _service.CreateBoardAsync(NewBoardName, NewBoardDescription, CurrentBoardId,
                        defaultTypes, defaultThemes, defaultColors, defaultSentiment, defaultTeColors, defaultSubtypes, defaultItemIds);
                }

                IsCreatingBoard = false;
                IsEditingBoard = false;
                NewBoardName = string.Empty;
                NewBoardDescription = string.Empty;
                NewBoardSetDefaults = false;
                HasExistingBoardDefaults = false;
                _editingBoardId = null;

                await LoadBoardViewAsync(CurrentBoardId);
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex);
            }
        }

        [RelayCommand]
        private async Task DeleteBoard(BoardEntry board)
        {
            var result = MessageBox.Show(
                $"Delete board \"{board.Name}\"?\n\nImages in this board will be moved to the parent level. Sub-boards will be promoted to the parent level.",
                "Delete Board",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _service.DeleteBoardAsync(board.Id);
                await LoadBoardViewAsync(CurrentBoardId);
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex);
            }
        }

        // ── Move Image ────────────────────────────────────────────────────────

        [RelayCommand]
        private async Task StartMoveImage()
        {
            if (SelectedImage == null) return;
            IsMovingImage = true;
            IsEditingItems = false;

            try
            {
                var all = await _service.GetAllBoardsFlatAsync();
                AllBoardsFlat = new ObservableCollection<BoardEntry>(
                    all.Select(b => new BoardEntry
                    {
                        Id = b.Id,
                        Name = b.Name,
                        Description = b.Description,
                        ParentBoardId = b.ParentBoardId
                    }));
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex);
            }
        }

        [RelayCommand]
        private void CancelMoveImage()
        {
            IsMovingImage = false;
        }

        [RelayCommand]
        private async Task MoveImageToBoard(int? boardId)
        {
            if (SelectedImage == null) return;

            try
            {
                await _service.MoveImageToBoardAsync(SelectedImage.Id, boardId);
                InspirationThumbnailCacheService.InvalidateImage(SelectedImage.Id);

                IsMovingImage = false;
                ClearSelection();

                await LoadBoardViewAsync(CurrentBoardId);
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex);
            }
        }

        // ── Image CRUD ────────────────────────────────────────────────────────

        public async Task LoadThumbnailAsync(InspirationEntry entry)
        {
            if (entry.ThumbnailLoaded) return;

            try
            {
                var imageUrl = await _service.GetInspirationImageUrlAsync(entry.Id);
                if (imageUrl != null)
                    entry.ThumbnailUrl = imageUrl;
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex);
            }
        }

        [RelayCommand]
        private async Task SelectImage(InspirationEntry? entry)
        {
            if (entry == null) return;

            SelectedImage = entry;
            SelectedImageDetail = null;
            IsEditingItems = false;
            IsMovingImage = false;
            IsImageDetailPopupOpen = true;

            try
            {
                var imageUrl = await _service.GetInspirationImageUrlAsync(entry.Id);
                SelectedImageFullUrl = imageUrl;

                var meta = await _service.GetInspirationImageMetaAsync(entry.Id);
                SelectedImageDetail = meta;

                var linkedItems = await _service.GetInspirationImageItemsAsync(entry.Id);
                SelectedImageItems = new ObservableCollection<InspirationItemEntry>(
                    linkedItems.Select(li => new InspirationItemEntry
                    {
                        ItemId = li.ItemId,
                        ItemName = li.Item?.Name ?? "Unknown",
                        ItemType = li.Item?.Type ?? "",
                        ItemNumber = li.Item?.ItemNumber
                    }));
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex);
            }
        }

        [RelayCommand]
        private void ClearSelection()
        {
            IsImageDetailPopupOpen = false;
            SelectedImage = null;
            SelectedImageDetail = null;
            SelectedImageFullUrl = null;
            SelectedImageItems = new ObservableCollection<InspirationItemEntry>();
            IsEditingItems = false;
            IsMovingImage = false;
        }

        [RelayCommand]
        private void CloseImageDetailPopup() => IsImageDetailPopupOpen = false;

        [RelayCommand]
        private void ToggleBoardPicker() => IsBoardPickerOpen = !IsBoardPickerOpen;

        [RelayCommand]
        private void ToggleColorPicker() => IsColorPickerOpen = !IsColorPickerOpen;

        [RelayCommand]
        private void ToggleTeColorPicker() => IsTeColorPickerOpen = !IsTeColorPickerOpen;

        [RelayCommand]
        private void ToggleTypePicker() => IsTypePickerOpen = !IsTypePickerOpen;

        [RelayCommand]
        private void ToggleThemePicker() => IsThemePickerOpen = !IsThemePickerOpen;

        [RelayCommand]
        private async Task StartAddImage()
        {
            IsAddingImage = true;
            NewImageUrl = null;
            AddItemsSearchText = string.Empty;
            AddItemsList = new ObservableCollection<InspirationSelectableItem>();
            NewImageTheme = null;
            NewImageSentiment = null;
            IsColorPickerOpen = false;
            IsTeColorPickerOpen = false;
            IsTypePickerOpen = false;
            IsThemePickerOpen = false;
            IsAddingItemUsed = false;
            IsAskingMoreItems = false;
            AddItemType = null;
            AddItemSubtypes.Clear();
            AddItemOptions.Clear();
            AddItemSelected = null;

            // Initialize color checkboxes
            var colorItems = InventoryService.InspirationColors.Select(c =>
            {
                var item = new SelectableStringItem { Name = c };
                item.SelectionChanged = () => OnPropertyChanged(nameof(NewImageColorsDisplay));
                return item;
            }).ToList();
            NewImageColorItems = new ObservableCollection<SelectableStringItem>(colorItems);

            // Initialize TE color checkboxes (ColorOrder.txt, sorted alphabetically)
            var teColorItems = InventoryService.ColorOrder
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .Select(c =>
                {
                    var item = new SelectableStringItem { Name = c };
                    item.SelectionChanged = () => OnPropertyChanged(nameof(NewImageTeColorsDisplay));
                    return item;
                }).ToList();
            NewImageTeColorItems = new ObservableCollection<SelectableStringItem>(teColorItems);

            // Initialize type checkboxes
            var typeItems = ItemTypes.Select(t =>
            {
                var item = new SelectableStringItem { Name = t };
                item.SelectionChanged = () => OnPropertyChanged(nameof(NewImageTypesDisplay));
                return item;
            }).ToList();
            NewImageTypeItems = new ObservableCollection<SelectableStringItem>(typeItems);

            // Initialize theme checkboxes
            var themeItems = ThemeOptions.Select(t =>
            {
                var item = new SelectableStringItem { Name = t };
                item.SelectionChanged = () => OnPropertyChanged(nameof(NewImageThemesDisplay));
                return item;
            }).ToList();
            NewImageThemeItems = new ObservableCollection<SelectableStringItem>(themeItems);

            // Load boards for multi-select picker
            IsBoardPickerOpen = false;
            try
            {
                var all = await _service.GetAllBoardsFlatAsync();
                var boardItems = all.Select(b =>
                {
                    var item = new SelectableBoardItem { Id = b.Id, Name = b.Name };
                    // Default-select the board the user is currently viewing
                    item.IsSelected = CurrentBoardId.HasValue && b.Id == CurrentBoardId.Value;
                    item.SelectionChanged = () => OnPropertyChanged(nameof(NewImageBoardsDisplay));
                    return item;
                }).ToList();
                NewImageBoardItems = new ObservableCollection<SelectableBoardItem>(boardItems);
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex);
            }
        }

        private async Task LoadSelectableItemsForAdd()
        {
            try
            {
                var items = await _service.GetItemsLightForSearchAsync();
                var selectables = items.Select(i =>
                {
                    var si = new InspirationSelectableItem
                    {
                        ItemId = i.Id,
                        ItemName = i.Name,
                        ItemType = i.Type,
                        ItemNumber = i.ItemNumber
                    };
                    si.SelectionChanged = () =>
                    {
                        OnPropertyChanged(nameof(SelectedAddItems));
                        OnPropertyChanged(nameof(HasSelectedAddItems));
                    };
                    return si;
                }).ToList();

                AddItemsList = new ObservableCollection<InspirationSelectableItem>(selectables);
                OnPropertyChanged(nameof(FilteredAddItems));
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex);
            }
        }

        [RelayCommand]
        private void CancelAddImage()
        {
            IsAddingImage = false;
            NewImageUrl = null;
            AddItemsList = new ObservableCollection<InspirationSelectableItem>();
            AddItemsSearchText = string.Empty;
            NewImageBoardItems = new ObservableCollection<SelectableBoardItem>();
            NewImageColorItems = new ObservableCollection<SelectableStringItem>();
            NewImageTeColorItems = new ObservableCollection<SelectableStringItem>();
            NewImageTheme = null;
            NewImageSentiment = null;
            NewImageTypeItems = new ObservableCollection<SelectableStringItem>();
            NewImageThemeItems = new ObservableCollection<SelectableStringItem>();
            IsBoardPickerOpen = false;
            IsColorPickerOpen = false;
            IsTeColorPickerOpen = false;
            IsTypePickerOpen = false;
            IsThemePickerOpen = false;
            IsAddingItemUsed = false;
            IsAskingMoreItems = false;
            AddItemType = null;
            AddItemSubtypes.Clear();
            AddItemOptions.Clear();
            AddItemSelected = null;
        }

        [RelayCommand]
        private void BrowseImage()
        {
            var dialog = new OpenFileDialog
            {
                Filter = ImageLoadService.OpenFileFilter,
                Title = "Select an inspiration image"
            };

            if (dialog.ShowDialog() == true)
                LoadImageFromPath(dialog.FileName);
        }

        public void LoadImageFromPath(string filePath)
        {
            try
            {
                NewImageUrl = ImageLoadService.LoadAsDataUri(filePath);
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex);
            }
        }

        [RelayCommand]
        private async Task SaveImage()
        {
            if (string.IsNullOrEmpty(NewImageUrl)) return;

            try
            {
                var selectedColors = NewImageColorItems.Where(c => c.IsSelected).Select(c => c.Name).ToList();
                var selectedTeColors = NewImageTeColorItems.Where(c => c.IsSelected).Select(c => c.Name).ToList();
                var selectedTypes = NewImageTypeItems.Where(t => t.IsSelected).Select(t => t.Name).ToList();
                var selectedThemes = NewImageThemeItems.Where(t => t.IsSelected).Select(t => t.Name).ToList();
                var selectedBoardIds = NewImageBoardItems.Where(b => b.IsSelected).Select(b => (int?)b.Id).ToList();

                // If no boards selected, save once as uncategorized
                if (selectedBoardIds.Count == 0)
                    selectedBoardIds.Add(null);

                var selectedItemIds = AddItemsList
                    .Where(s => s.IsSelected)
                    .Select(s => s.ItemId)
                    .ToList();

                foreach (var boardId in selectedBoardIds)
                {
                    var image = new InspirationImage
                    {
                        ImageUrl = NewImageUrl,
                        CreatedAt = DateTime.Now,
                        BoardId = boardId,
                        Color = selectedColors.Count > 0 ? string.Join(",", selectedColors) : null,
                        TeColor = selectedTeColors.Count > 0 ? string.Join(",", selectedTeColors) : null,
                        Types = selectedTypes.Count > 0 ? string.Join(",", selectedTypes) : null,
                        Theme = selectedThemes.Count > 0 ? string.Join(",", selectedThemes) : null,
                        Sentiment = string.IsNullOrWhiteSpace(NewImageSentiment) ? null : NewImageSentiment,
                    };
                    var saved = await _service.AddInspirationImageAsync(image);

                    if (selectedItemIds.Count > 0)
                        await _service.SetInspirationImageItemsAsync(saved.Id, selectedItemIds);
                }

                IsAddingImage = false;
                NewImageUrl = null;
                AddItemsList = new ObservableCollection<InspirationSelectableItem>();
                AddItemsSearchText = string.Empty;
                NewImageBoardItems = new ObservableCollection<SelectableBoardItem>();
                NewImageColorItems = new ObservableCollection<SelectableStringItem>();
                NewImageTeColorItems = new ObservableCollection<SelectableStringItem>();
                NewImageTheme = null;
                NewImageSentiment = null;
                NewImageTypeItems = new ObservableCollection<SelectableStringItem>();
                NewImageThemeItems = new ObservableCollection<SelectableStringItem>();
                IsBoardPickerOpen = false;
                IsColorPickerOpen = false;
                IsTeColorPickerOpen = false;
                IsTypePickerOpen = false;
                IsThemePickerOpen = false;

                await LoadBoardViewAsync(CurrentBoardId);
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex);
            }
        }

        [RelayCommand]
        private async Task DeleteImage()
        {
            if (SelectedImage == null) return;

            var label = string.IsNullOrWhiteSpace(SelectedImage.Title)
                ? "this photo"
                : $"\"{SelectedImage.Title}\"";

            var result = MessageBox.Show(
                $"Permanently delete {label}? This cannot be undone.",
                "Delete Photo",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _service.DeleteInspirationImageAsync(SelectedImage.Id);
                InspirationThumbnailCacheService.InvalidateImage(SelectedImage.Id);
                ClearSelection();
                await LoadBoardViewAsync(CurrentBoardId);
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex);
            }
        }

        // ── Add Item Used flow ────────────────────────────────────────────────

        [RelayCommand]
        private void StartAddItemUsed()
        {
            IsAddingItemUsed = true;
            IsAskingMoreItems = false;
            AddItemType = null;
            AddItemSubtypes.Clear();
            AddItemOptions.Clear();
            AddItemSelected = null;
            OnPropertyChanged(nameof(HasAddItemSubtypes));
            OnPropertyChanged(nameof(HasAddItemOptions));
            OnPropertyChanged(nameof(CanConfirmAddItem));
        }

        private async Task RefreshAddItemOptionsAsync()
        {
            if (string.IsNullOrEmpty(AddItemType))
            {
                AddItemOptions.Clear();
                AddItemSelected = null;
                OnPropertyChanged(nameof(HasAddItemOptions));
                OnPropertyChanged(nameof(CanConfirmAddItem));
                return;
            }

            try
            {
                var items = await _service.GetWizardItemsAsync(type: AddItemType);
                var checkedSubs = AddItemSubtypes.Where(s => s.IsChecked).Select(s => s.Label).ToList();
                if (checkedSubs.Count > 0)
                    items = items.Where(i => i.Subtype != null &&
                        i.Subtype.Split(',').Select(p => p.Trim())
                            .Any(p => checkedSubs.Contains(p, StringComparer.OrdinalIgnoreCase))).ToList();

                AddItemOptions.Clear();
                foreach (var i in items) AddItemOptions.Add(i);
                AddItemSelected = null;
                OnPropertyChanged(nameof(HasAddItemOptions));
                OnPropertyChanged(nameof(CanConfirmAddItem));
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex);
            }
        }

        [RelayCommand]
        private void ConfirmAddItemUsed()
        {
            if (AddItemSelected == null) return;

            if (!AddItemsList.Any(i => i.ItemId == AddItemSelected.Id))
            {
                var si = new InspirationSelectableItem
                {
                    ItemId = AddItemSelected.Id,
                    ItemName = AddItemSelected.Name,
                    ItemType = AddItemSelected.ItemType ?? "",
                    IsSelected = true
                };
                si.SelectionChanged = () =>
                {
                    OnPropertyChanged(nameof(SelectedAddItems));
                    OnPropertyChanged(nameof(HasSelectedAddItems));
                };
                AddItemsList.Add(si);
            }
            else
            {
                AddItemsList.First(i => i.ItemId == AddItemSelected.Id).IsSelected = true;
            }

            OnPropertyChanged(nameof(SelectedAddItems));
            OnPropertyChanged(nameof(HasSelectedAddItems));

            IsAddingItemUsed = false;
            IsAskingMoreItems = true;
        }

        [RelayCommand]
        private void AddMoreItems()
        {
            IsAskingMoreItems = false;
            StartAddItemUsed();
        }

        [RelayCommand]
        private void FinishAddItems()
        {
            IsAskingMoreItems = false;
        }

        [RelayCommand]
        private void CancelAddItemUsed()
        {
            IsAddingItemUsed = false;
            IsAskingMoreItems = false;
            AddItemType = null;
            AddItemSubtypes.Clear();
            AddItemOptions.Clear();
            AddItemSelected = null;
            OnPropertyChanged(nameof(HasAddItemSubtypes));
            OnPropertyChanged(nameof(HasAddItemOptions));
            OnPropertyChanged(nameof(CanConfirmAddItem));
        }

        private async Task RefreshBoardPickerItemsAsync()
        {
            if (string.IsNullOrEmpty(BoardPickerType))
            {
                BoardPickerItems.Clear();
                BoardPickerSelectedItem = null;
                OnPropertyChanged(nameof(HasBoardPickerItems));
                OnPropertyChanged(nameof(CanConfirmBoardType));
                return;
            }

            try
            {
                var items = await _service.GetWizardItemsAsync(type: BoardPickerType);
                var checkedSubs = BoardPickerSubtypes.Where(s => s.IsChecked).Select(s => s.Label).ToList();
                if (checkedSubs.Count > 0)
                    items = items.Where(i => i.Subtype != null &&
                        i.Subtype.Split(',').Select(p => p.Trim())
                            .Any(p => checkedSubs.Contains(p, StringComparer.OrdinalIgnoreCase))).ToList();

                BoardPickerItems.Clear();
                foreach (var i in items) BoardPickerItems.Add(i);
                BoardPickerSelectedItem = null;
                OnPropertyChanged(nameof(HasBoardPickerItems));
                OnPropertyChanged(nameof(CanConfirmBoardType));
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex);
            }
        }

        // ── Board default type+subtype commands ───────────────────────────────

        [RelayCommand]
        private void StartAddBoardType()
        {
            IsBoardTypesPickerActive = true;
            IsAskingMoreBoardTypes = false;
            BoardPickerType = null;
            BoardPickerSubtypes.Clear();
            BoardPickerItems.Clear();
            BoardPickerSelectedItem = null;
            OnPropertyChanged(nameof(HasBoardPickerSubtypes));
            OnPropertyChanged(nameof(HasBoardPickerItems));
            OnPropertyChanged(nameof(BoardPickerNeedsSpecificItem));
            OnPropertyChanged(nameof(CanConfirmBoardType));
        }

        [RelayCommand]
        private void ConfirmAddBoardType()
        {
            if (string.IsNullOrEmpty(BoardPickerType)) return;

            if (TypeNeedsSpecificItem(BoardPickerType))
            {
                if (BoardPickerSelectedItem == null) return;
                // Deduplicate by item ID
                if (!BoardTypeSubtypeList.Any(e => e.SpecificItemId == BoardPickerSelectedItem.Id))
                    BoardTypeSubtypeList.Add(new BoardTypeEntry
                    {
                        Type = BoardPickerType,
                        SpecificItemId = BoardPickerSelectedItem.Id,
                        SpecificItemName = BoardPickerSelectedItem.Name
                    });
            }
            else
            {
                var checkedSubs = BoardPickerSubtypes.Where(s => s.IsChecked).Select(s => s.Label).ToList();
                // Deduplicate by type (only one type+subtype entry per type)
                if (!BoardTypeSubtypeList.Any(e => e.Type == BoardPickerType && !e.SpecificItemId.HasValue))
                    BoardTypeSubtypeList.Add(new BoardTypeEntry { Type = BoardPickerType, Subtypes = checkedSubs });
            }

            IsBoardTypesPickerActive = false;
            IsAskingMoreBoardTypes = true;
        }

        [RelayCommand]
        private void AddMoreBoardTypes()
        {
            IsAskingMoreBoardTypes = false;
            StartAddBoardType();
        }

        [RelayCommand]
        private void FinishAddBoardTypes()
        {
            IsAskingMoreBoardTypes = false;
        }

        [RelayCommand]
        private void CancelAddBoardType()
        {
            IsBoardTypesPickerActive = false;
            IsAskingMoreBoardTypes = false;
            BoardPickerType = null;
            BoardPickerSubtypes.Clear();
            BoardPickerItems.Clear();
            BoardPickerSelectedItem = null;
            OnPropertyChanged(nameof(HasBoardPickerSubtypes));
            OnPropertyChanged(nameof(HasBoardPickerItems));
            OnPropertyChanged(nameof(BoardPickerNeedsSpecificItem));
            OnPropertyChanged(nameof(CanConfirmBoardType));
        }

        [RelayCommand]
        private void RemoveBoardTypeEntry(BoardTypeEntry entry)
        {
            BoardTypeSubtypeList.Remove(entry);
        }

        [RelayCommand]
        private async Task StartEditItems()
        {
            if (SelectedImage == null) return;

            IsEditingItems = true;
            IsMovingImage = false;
            EditItemsSearchText = string.Empty;

            try
            {
                var existingItemIds = await _service.GetItemIdsForInspirationImageAsync(SelectedImage.Id);
                var items = await _service.GetItemsLightForSearchAsync();
                var selectables = items.Select(i =>
                {
                    var si = new InspirationSelectableItem
                    {
                        ItemId = i.Id,
                        ItemName = i.Name,
                        ItemType = i.Type,
                        ItemNumber = i.ItemNumber,
                        IsSelected = existingItemIds.Contains(i.Id)
                    };
                    si.SelectionChanged = () => OnPropertyChanged(nameof(SelectedEditItems));
                    return si;
                }).ToList();

                EditItemsList = new ObservableCollection<InspirationSelectableItem>(selectables);
                OnPropertyChanged(nameof(FilteredEditItems));
                OnPropertyChanged(nameof(SelectedEditItems));
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex);
            }
        }

        [RelayCommand]
        private void CancelEditItems()
        {
            IsEditingItems = false;
            EditItemsList = new ObservableCollection<InspirationSelectableItem>();
            EditItemsSearchText = string.Empty;
        }

        [RelayCommand]
        private async Task SaveEditItems()
        {
            if (SelectedImage == null) return;

            try
            {
                var selectedItemIds = EditItemsList
                    .Where(s => s.IsSelected)
                    .Select(s => s.ItemId)
                    .ToList();

                await _service.SetInspirationImageItemsAsync(SelectedImage.Id, selectedItemIds);

                IsEditingItems = false;
                EditItemsList = new ObservableCollection<InspirationSelectableItem>();
                EditItemsSearchText = string.Empty;

                await SelectImage(SelectedImage);
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex);
            }
        }

        [RelayCommand]
        private void ClearFilters()
        {
            SearchText = string.Empty;
            FilterType = null;
            FilterTheme = null;
        }

        // ── Board-defaults picker toggles ─────────────────────────────────────

        [RelayCommand] private void ToggleDefaultTypePicker() => IsDefaultTypePickerOpen = !IsDefaultTypePickerOpen;
        [RelayCommand] private void ToggleDefaultThemePicker() => IsDefaultThemePickerOpen = !IsDefaultThemePickerOpen;
        [RelayCommand] private void ToggleDefaultColorPicker() => IsDefaultColorPickerOpen = !IsDefaultColorPickerOpen;
        [RelayCommand] private void ToggleDefaultTeColorPicker() => IsDefaultTeColorPickerOpen = !IsDefaultTeColorPickerOpen;

        // ── Organization mode commands ────────────────────────────────────────

        [RelayCommand]
        private async Task ToggleOrganizing()
        {
            if (IsOrganizing)
            {
                // Exit org mode
                foreach (var img in _allImages) img.IsOrgSelected = false;
                _orgSelectedIds.Clear();
                NotifyOrgChanged();
                IsOrganizing = false;
            }
            else
            {
                ClearSelection();
                _orgSelectedIds.Clear();
                NotifyOrgChanged();
                OrgMoveTargetBoardId = null;
                IsOrganizing = true;
                try
                {
                    var all = await _service.GetAllBoardsFlatAsync();
                    AllBoardsFlat = new ObservableCollection<BoardEntry>(
                        all.Select(b => new BoardEntry { Id = b.Id, Name = b.Name, ParentBoardId = b.ParentBoardId }));
                }
                catch (Exception ex) { LoggingService.LogError(ex); }
            }
        }

        [RelayCommand]
        private void ToggleOrgImageSelection(InspirationEntry entry)
        {
            if (_orgSelectedIds.Contains(entry.Id))
            {
                _orgSelectedIds.Remove(entry.Id);
                entry.IsOrgSelected = false;
            }
            else
            {
                _orgSelectedIds.Add(entry.Id);
                entry.IsOrgSelected = true;
            }
            NotifyOrgChanged();
        }

        [RelayCommand]
        private void SelectAllOrgImages()
        {
            foreach (var img in Images)
            {
                if (!_orgSelectedIds.Contains(img.Id))
                {
                    _orgSelectedIds.Add(img.Id);
                    img.IsOrgSelected = true;
                }
            }
            NotifyOrgChanged();
        }

        [RelayCommand]
        private void ClearOrgSelection()
        {
            foreach (var img in _allImages) img.IsOrgSelected = false;
            _orgSelectedIds.Clear();
            NotifyOrgChanged();
        }

        [RelayCommand]
        private async Task BulkMoveToBoard()
        {
            if (_orgSelectedIds.Count == 0 || OrgMoveTargetBoardId == null) return;
            try
            {
                IsLoading = true;
                foreach (var id in _orgSelectedIds.ToList())
                {
                    await _service.MoveImageToBoardAsync(id, OrgMoveTargetBoardId);
                    InspirationThumbnailCacheService.InvalidateImage(id);
                }
                foreach (var img in _allImages) img.IsOrgSelected = false;
                _orgSelectedIds.Clear();
                NotifyOrgChanged();
                IsOrganizing = false;
                await LoadBoardViewAsync(CurrentBoardId);
            }
            catch (Exception ex) { LoggingService.LogError(ex); }
            finally { IsLoading = false; }
        }

        private void NotifyOrgChanged()
        {
            OnPropertyChanged(nameof(OrgSelectedCount));
            OnPropertyChanged(nameof(OrgSelectedLabel));
        }

        // ── Drag-move helper (called from code-behind) ────────────────────────

        public async Task DragMoveImageToBoardAsync(int imageId, int targetBoardId)
        {
            try
            {
                await _service.MoveImageToBoardAsync(imageId, targetBoardId);
                InspirationThumbnailCacheService.InvalidateImage(imageId);
                await LoadBoardViewAsync(CurrentBoardId);
            }
            catch (Exception ex) { LoggingService.LogError(ex); }
        }

        // ── Board-defaults helpers ────────────────────────────────────────────

        private void InitBoardDefaultPickers(Models.InspirationBoard? board)
        {
            var existingTypes   = board?.DefaultTypes?.Split(',',   StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
            var existingThemes  = board?.DefaultThemes?.Split(',',  StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
            var existingColors  = board?.DefaultColors?.Split(',',  StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
            var existingTeColors= board?.DefaultTeColors?.Split(',',StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();

            NewBoardDefaultTypeItems = new ObservableCollection<SelectableStringItem>(
                ItemTypes.Select(t =>
                {
                    var item = new SelectableStringItem { Name = t, IsSelected = existingTypes.Contains(t) };
                    item.SelectionChanged = () => OnPropertyChanged(nameof(BoardDefaultTypesDisplay));
                    return item;
                }));

            NewBoardDefaultThemeItems = new ObservableCollection<SelectableStringItem>(
                ThemeOptions.Select(t =>
                {
                    var item = new SelectableStringItem { Name = t, IsSelected = existingThemes.Contains(t) };
                    item.SelectionChanged = () => OnPropertyChanged(nameof(BoardDefaultThemesDisplay));
                    return item;
                }));

            NewBoardDefaultColorItems = new ObservableCollection<SelectableStringItem>(
                InventoryService.InspirationColors.Select(c =>
                {
                    var item = new SelectableStringItem { Name = c, IsSelected = existingColors.Contains(c) };
                    item.SelectionChanged = () => OnPropertyChanged(nameof(BoardDefaultColorsDisplay));
                    return item;
                }));

            NewBoardDefaultTeColorItems = new ObservableCollection<SelectableStringItem>(
                InventoryService.ColorOrder
                    .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                    .Select(c =>
                    {
                        var item = new SelectableStringItem { Name = c, IsSelected = existingTeColors.Contains(c) };
                        item.SelectionChanged = () => OnPropertyChanged(nameof(BoardDefaultTeColorsDisplay));
                        return item;
                    }));

            NewBoardDefaultSentiment = board?.DefaultSentiment ?? string.Empty;
        }

        /// <summary>Returns comma-joined selected names, or null if none selected.</summary>
        private static string? PickerSelected(ObservableCollection<SelectableStringItem> items)
        {
            var sel = items.Where(i => i.IsSelected).Select(i => i.Name).ToList();
            return sel.Count > 0 ? string.Join(",", sel) : null;
        }

        public async Task NavigateToItem(int itemId)
        {
            try
            {
                _mainVm.NavigateToInventoryCommand.Execute(null);
                await _mainVm.InventoryVM.SelectItemByIdAsync(itemId);
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "InspirationViewModel.NavigateToItem");
            }
        }
    }
}
