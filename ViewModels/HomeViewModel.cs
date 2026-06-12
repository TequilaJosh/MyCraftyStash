using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyCraftyStash.Models;
using MyCraftyStash.Services;

namespace MyCraftyStash.ViewModels
{
    public partial class HomeStatItem : ObservableObject
    {
        [ObservableProperty] private int _id;
        [ObservableProperty] private string _name = string.Empty;
        [ObservableProperty] private string _sku = string.Empty;
        [ObservableProperty] private string _typeBadge = string.Empty;
        [ObservableProperty] private int? _currentStock;
        [ObservableProperty] private int _packSize = 1;
        [ObservableProperty] private double _stockPercent;
        [ObservableProperty] private string _stockLabel = "Low";
        [ObservableProperty] private bool _isOut;
    }

    public partial class HomeRecentProject : ObservableObject
    {
        [ObservableProperty] private int _id;
        [ObservableProperty] private string _name = string.Empty;
        [ObservableProperty] private string _dateLabel = string.Empty;
        [ObservableProperty] private int _supplyCount;
        [ObservableProperty] private string? _imageUrl;
    }

    public partial class HomeViewModel : BaseViewModel
    {
        private readonly InventoryService _service;
        private readonly MainViewModel _main;
        private bool _loaded;

        [ObservableProperty] private string _userGreeting = "Welcome back";
        [ObservableProperty] private int _totalItems;
        [ObservableProperty] private int _sentimentsIndexed;
        [ObservableProperty] private int _categoryCount;
        [ObservableProperty] private int _lowOrOutCount;
        [ObservableProperty] private int _projectCount;
        [ObservableProperty] private int _itemsAddedThisMonth;
        [ObservableProperty] private int _projectsAddedThisMonth;

        public ObservableCollection<HomeStatItem> RunningLow { get; } = new();
        public ObservableCollection<HomeRecentProject> RecentProjects { get; } = new();

        public HomeViewModel(InventoryService service, MainViewModel main)
        {
            _service = service;
            _main = main;
        }

        public async Task LoadAsync()
        {
            if (_loaded) return;
            _loaded = true;
            await RefreshAsync();
        }

        public async Task RefreshAsync()
        {
            try
            {
                IsLoading = true;
                var items = await _service.GetItemsAsync();
                TotalItems = items.Count;
                CategoryCount = items.Select(i => i.Type ?? "").Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().Count();

                var thisMonth = DateTime.Now;
                ItemsAddedThisMonth = items.Count(i => i.CreatedAt.Year == thisMonth.Year && i.CreatedAt.Month == thisMonth.Month);

                SentimentsIndexed = items.Count(i => !string.IsNullOrWhiteSpace(i.Sentiments));

                RunningLow.Clear();
                // Low/Out comes from the per-type warning levels the user set
                // in Settings -> Tracked Items (via GetStockLevel), not from a
                // pack-size percentage. The old percent-of-pack math silently
                // ignored every item without a pack size, so even 0-stock
                // items never showed up here.
                var lowList = items
                    .Where(i => i.CurrentStock.HasValue)
                    .Select(i => new
                    {
                        Item = i,
                        Level = InventoryService.GetStockLevel(i.Type, i.CurrentStock),
                    })
                    .Where(x => x.Level is InventoryService.StockLevel.Low
                             or InventoryService.StockLevel.Out)
                    .ToList();

                // The stat tile counts everything low or out; the list below
                // it previews the five most urgent.
                LowOrOutCount = lowList.Count;
                foreach (var x in lowList
                    .OrderBy(x => x.Level == InventoryService.StockLevel.Out ? 0 : 1)
                    .ThenBy(x => x.Item.CurrentStock)
                    .Take(5))
                {
                    var i = x.Item;
                    // Progress bar: fraction of the type's "good" threshold,
                    // falling back to pack size when no threshold is set.
                    double denom =
                        InventoryService.TrackedTypeConfigs.TryGetValue(i.Type, out var cfg)
                        && cfg.GoodThreshold is > 0
                            ? cfg.GoodThreshold.Value
                            : (i.PackSize is > 0 ? i.PackSize.Value : 1);
                    var pct = Math.Max(0, Math.Min(1, (i.CurrentStock ?? 0) / denom));
                    RunningLow.Add(new HomeStatItem
                    {
                        Id = i.Id,
                        Name = i.Name,
                        Sku = i.ItemNumber ?? string.Empty,
                        TypeBadge = ShortBadge(i.Type),
                        CurrentStock = i.CurrentStock,
                        PackSize = i.PackSize ?? 1,
                        StockPercent = pct * 100.0,
                        IsOut = x.Level == InventoryService.StockLevel.Out,
                        StockLabel = x.Level == InventoryService.StockLevel.Out ? "Out" : "Low"
                    });
                }

                var projects = await _service.GetProjectsAsync();
                ProjectCount = projects.Count;
                ProjectsAddedThisMonth = projects.Count(p => p.CreatedAt.Year == thisMonth.Year && p.CreatedAt.Month == thisMonth.Month);

                RecentProjects.Clear();
                foreach (var p in projects.OrderByDescending(p => p.CreatedAt).Take(4))
                {
                    RecentProjects.Add(new HomeRecentProject
                    {
                        Id = p.Id,
                        Name = p.Name,
                        DateLabel = p.CreatedAt.ToString("MMM d, yyyy"),
                        SupplyCount = p.ProjectItems?.Count ?? 0,
                        ImageUrl = p.ImageUrl
                    });
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "HomeViewModel.RefreshAsync");
                ErrorMessage = "Couldn't load home dashboard.";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task NavigateToProject(int id)
        {
            _main.NavigateToProjectsCommand.Execute(null);
            var project = await _service.GetProjectAsync(id);
            if (project == null) return;
            await _main.ProjectsVM.ViewProjectDetailsCommand.ExecuteAsync(project);
        }

        [RelayCommand]
        private async Task NavigateToItem(int id)
        {
            _main.NavigateToInventoryCommand.Execute(null);
            await _main.InventoryVM.SelectItemByIdAsync(id);
        }

        private static string ShortBadge(string? type)
        {
            if (string.IsNullOrWhiteSpace(type)) return "";
            var t = type.Trim().ToUpperInvariant();
            return t switch
            {
                var s when s.StartsWith("INK") => "INK",
                var s when s.StartsWith("PAPER") || s.StartsWith("CARDSTOCK") => "PAPER",
                var s when s.StartsWith("STAMP") => "STMP",
                var s when s.StartsWith("DIE") => "DIE",
                var s when s.StartsWith("EMBELL") => "EMB",
                _ => t.Length > 4 ? t.Substring(0, 4) : t
            };
        }
    }
}
