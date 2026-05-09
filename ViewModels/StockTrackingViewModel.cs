using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using MyCraftyStash.Models;
using MyCraftyStash.Services;

namespace MyCraftyStash.ViewModels
{
    /// <summary>One row in the stock tracking page - an item with its individual unit count.</summary>
    public partial class StockRowViewModel : ObservableObject
    {
        public int ItemId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? Subtype { get; set; }
        public string? Color { get; set; }
        public int? PackSize { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StockDisplay))]
        [NotifyPropertyChangedFor(nameof(StockColor))]
        private int _currentStock;

        [ObservableProperty]
        private bool _isEditing;

        [ObservableProperty]
        private int _editValue;

        public string UnitLabel => InventoryService.IsEnvelopeType(Type) ? "envelopes"
            : InventoryService.IsCardstockType(Type) ? "sheets"
            : Type.Contains("Foil-it", StringComparison.OrdinalIgnoreCase) ? "foil-its"
            : Type.Contains("Foil", StringComparison.OrdinalIgnoreCase) ? "foils"
            : "units";

        public string StockDisplay => $"{CurrentStock} {UnitLabel} remaining";

        public string StockColor => CurrentStock <= 0 ? "#EF4444"
            : CurrentStock <= 5 ? "#F59E0B"
            : "#22C55E";
    }

    /// <summary>Groups stock rows by type.</summary>
    public class StockGroupViewModel
    {
        public string TypeName { get; set; } = string.Empty;
        public List<StockRowViewModel> Items { get; set; } = new();
        public int TotalUnits => Items.Sum(i => i.CurrentStock);
        public string UnitLabel => Items.FirstOrDefault()?.UnitLabel ?? "units";
    }

    /// <summary>One row in the Project Inventory tab - a tracked project with a quantity on hand.</summary>
    public partial class ProjectInventoryRowViewModel : ObservableObject
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StockDisplay))]
        [NotifyPropertyChangedFor(nameof(StockColor))]
        private int _quantityOnHand;

        [ObservableProperty]
        private bool _isEditing;

        [ObservableProperty]
        private int _editValue;

        public string StockDisplay => $"{QuantityOnHand} on hand";

        public string StockColor => QuantityOnHand <= 0 ? "#EF4444"
            : QuantityOnHand <= 3 ? "#F59E0B"
            : "#22C55E";
    }

    public partial class StockTrackingViewModel : BaseViewModel
    {
        private readonly InventoryService _service;
        private readonly MainViewModel _mainVM;

        // ── Stock Tracker tab ──────────────────────────────────────────────────

        [ObservableProperty]
        private ObservableCollection<StockGroupViewModel> _stockGroups = new();

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        private string _successMessage = string.Empty;

        private List<StockRowViewModel> _allRows = new();

        // ── Project Inventory tab ──────────────────────────────────────────────

        [ObservableProperty]
        private ObservableCollection<ProjectInventoryRowViewModel> _projectRows = new();

        [ObservableProperty]
        private string _projectSearchText = string.Empty;

        private List<ProjectInventoryRowViewModel> _allProjectRows = new();

        [ObservableProperty]
        private string _activeStockTab = "Supply";

        [RelayCommand]
        private void ShowSupplyTab() => ActiveStockTab = "Supply";

        [RelayCommand]
        private void ShowProjectsTab() => ActiveStockTab = "Projects";

        public StockTrackingViewModel(InventoryService service, MainViewModel mainVM)
        {
            _service = service;
            _mainVM = mainVM;
        }

        [RelayCommand]
        public async Task LoadStock()
        {
            IsLoading = true;
            ErrorMessage = null;
            try
            {
                var allItems = await _service.GetItemsAsync();
                var tracked = allItems
                    .Where(i => InventoryService.IsTrackedType(i.Type))
                    .Select(i => new StockRowViewModel
                    {
                        ItemId = i.Id,
                        Name = i.Name,
                        Type = i.Type,
                        Subtype = i.Subtype,
                        PackSize = i.PackSize,
                        CurrentStock = i.CurrentStock ?? 0
                    })
                    .ToList();

                _allRows = tracked;
                BuildGroups(tracked);

                // Also reload project inventory
                await LoadProjectInventory();
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

        private async Task LoadProjectInventory()
        {
            try
            {
                var allProjects = await _service.GetProjectsAsync();
                var trackedConfigs = InventoryService.ProjectTrackedConfigs;

                var rows = allProjects
                    .Where(p => trackedConfigs.ContainsKey(p.Name))
                    .Select(p => new ProjectInventoryRowViewModel
                    {
                        ProjectId = p.Id,
                        ProjectName = p.Name,
                        QuantityOnHand = trackedConfigs.TryGetValue(p.Name, out var cfg) ? cfg.QuantityOnHand : 0
                    })
                    .OrderBy(r => r.ProjectName)
                    .ToList();

                _allProjectRows = rows;
                ApplyProjectFilter();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Project inventory error: {ex.Message}";
            }
        }

        partial void OnSearchTextChanged(string value) => ApplyFilter();
        partial void OnProjectSearchTextChanged(string value) => ApplyProjectFilter();

        private void ApplyFilter()
        {
            var search = SearchText.Trim().ToLower();
            var filtered = string.IsNullOrEmpty(search)
                ? _allRows
                : _allRows.Where(r =>
                    r.Name.ToLower().Contains(search) ||
                    r.Type.ToLower().Contains(search) ||
                    (r.Subtype?.ToLower().Contains(search) ?? false)).ToList();
            BuildGroups(filtered);
        }

        private void ApplyProjectFilter()
        {
            var search = ProjectSearchText.Trim().ToLower();
            var filtered = string.IsNullOrEmpty(search)
                ? _allProjectRows
                : _allProjectRows.Where(r => r.ProjectName.ToLower().Contains(search)).ToList();
            ProjectRows = new ObservableCollection<ProjectInventoryRowViewModel>(filtered);
        }

        private void BuildGroups(List<StockRowViewModel> rows)
        {
            var groups = rows
                .GroupBy(r => r.Type)
                .OrderBy(g => g.Key)
                .Select(g => new StockGroupViewModel
                {
                    TypeName = g.Key,
                    Items = g.OrderBy(r => r.Name).ToList()
                })
                .ToList();
            StockGroups = new ObservableCollection<StockGroupViewModel>(groups);
        }

        [RelayCommand]
        private void StartEditStock(StockRowViewModel row)
        {
            foreach (var group in StockGroups)
                foreach (var item in group.Items)
                    if (item != row) item.IsEditing = false;

            row.EditValue = row.CurrentStock;
            row.IsEditing = true;
        }

        [RelayCommand]
        private void CancelEditStock(StockRowViewModel row)
        {
            row.IsEditing = false;
        }

        [RelayCommand]
        private async Task SaveEditStock(StockRowViewModel row)
        {
            try
            {
                await _service.UpdateItemStockAsync(row.ItemId, row.EditValue);
                row.CurrentStock = row.EditValue;
                row.IsEditing = false;
                SuccessMessage = $"Updated {row.Name} stock to {row.EditValue} {row.UnitLabel}";
                _ = ClearSuccessAfterDelay();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to save: {ex.Message}";
            }
        }

        [RelayCommand]
        private void StartEditProjectStock(ProjectInventoryRowViewModel row)
        {
            foreach (var r in ProjectRows)
                if (r != row) r.IsEditing = false;

            row.EditValue = row.QuantityOnHand;
            row.IsEditing = true;
        }

        [RelayCommand]
        private void CancelEditProjectStock(ProjectInventoryRowViewModel row)
        {
            row.IsEditing = false;
        }

        [RelayCommand]
        private async Task SaveEditProjectStock(ProjectInventoryRowViewModel row)
        {
            try
            {
                await Task.Run(() => InventoryService.UpdateProjectQuantityOnHand(row.ProjectName, row.EditValue));
                row.QuantityOnHand = row.EditValue;
                row.IsEditing = false;
                SuccessMessage = $"Updated \"{row.ProjectName}\" quantity to {row.EditValue}";
                _ = ClearSuccessAfterDelay();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to save: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task GoToItem(StockRowViewModel row)
        {
            _mainVM.CurrentPage = "Inventory";
            _mainVM.CurrentView = _mainVM.InventoryVM;
            var items = await _service.GetItemsAsync();
            var item = items.FirstOrDefault(i => i.Id == row.ItemId);
            if (item != null)
                await _mainVM.InventoryVM.ViewItemDetailsCommand.ExecuteAsync(item);
        }

        [RelayCommand]
        private void GoToProject(ProjectInventoryRowViewModel row)
        {
            _mainVM.CurrentPage = "Projects";
            _mainVM.CurrentView = _mainVM.ProjectsVM;
        }

        private async Task ClearSuccessAfterDelay()
        {
            await Task.Delay(3000);
            SuccessMessage = string.Empty;
        }
    }
}
