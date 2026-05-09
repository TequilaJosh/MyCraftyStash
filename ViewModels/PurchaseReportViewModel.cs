using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using MyCraftyStash.Services;

namespace MyCraftyStash.ViewModels
{
    public partial class PurchaseReportViewModel : BaseViewModel
    {
        private readonly PurchaseReportService _reportService;
        private readonly ExportService _exportService;

        [ObservableProperty] private ObservableCollection<PurchaseReportRow> _rows = new();
        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _errorMessage = string.Empty;
        [ObservableProperty] private DateTime _fromDate = DateTime.Today.AddMonths(-3);
        [ObservableProperty] private DateTime _toDate = DateTime.Today;
        [ObservableProperty] private string _selectedGroupBy = "Month";
        [ObservableProperty] private string _statusMessage = string.Empty;

        public List<string> GroupByOptions { get; } = new() { "Month", "Type", "Store", "None" };

        // Computed summary
        public decimal TotalSpend     => Rows.Sum(r => r.LineTotal);
        public int     TotalItems     => Rows.Sum(r => r.Quantity);
        public int     UniqueItems    => Rows.Select(r => r.ItemName).Distinct().Count();
        public decimal AveragePerItem => TotalItems > 0 ? TotalSpend / TotalItems : 0;

        public IEnumerable<GroupedPurchases> GroupedRows
        {
            get
            {
                if (!Rows.Any()) return Enumerable.Empty<GroupedPurchases>();
                return SelectedGroupBy switch
                {
                    "Month"  => Rows.GroupBy(r => r.Date.HasValue ? r.Date.Value.ToString("MMMM yyyy") : "Unknown")
                                    .OrderByDescending(g => g.Key)
                                    .Select(g => new GroupedPurchases(g.Key, g.ToList())),
                    "Type"   => Rows.GroupBy(r => string.IsNullOrEmpty(r.ItemType) ? "Unknown" : r.ItemType)
                                    .OrderBy(g => g.Key)
                                    .Select(g => new GroupedPurchases(g.Key, g.ToList())),
                    "Store"  => Rows.GroupBy(r => string.IsNullOrEmpty(r.PurchasedFrom) ? "Unknown" : r.PurchasedFrom)
                                    .OrderBy(g => g.Key)
                                    .Select(g => new GroupedPurchases(g.Key, g.ToList())),
                    _        => new[] { new GroupedPurchases("All Purchases", Rows.ToList()) },
                };
            }
        }

        public PurchaseReportViewModel(PurchaseReportService reportService, ExportService exportService)
        {
            _reportService = reportService;
            _exportService = exportService;
        }

        partial void OnSelectedGroupByChanged(string value) => OnPropertyChanged(nameof(GroupedRows));

        [RelayCommand]
        public async Task LoadReport()
        {
            IsLoading     = true;
            ErrorMessage  = string.Empty;
            StatusMessage = string.Empty;
            try
            {
                var data = await _reportService.GetPurchasesAsync(FromDate, ToDate);
                Rows = new ObservableCollection<PurchaseReportRow>(data);
                OnPropertyChanged(nameof(TotalSpend));
                OnPropertyChanged(nameof(TotalItems));
                OnPropertyChanged(nameof(UniqueItems));
                OnPropertyChanged(nameof(AveragePerItem));
                OnPropertyChanged(nameof(GroupedRows));
                StatusMessage = Rows.Count == 0 ? "No purchases found for this period." : string.Empty;
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to load purchase history.";
                LoggingService.LogError(ex, "PurchaseReportViewModel.LoadReport");
            }
            finally { IsLoading = false; }
        }

        [RelayCommand]
        private async Task ExportCsv()
        {
            if (!Rows.Any()) return;
            IsLoading = true;
            try
            {
                var path = await _exportService.ExportPurchaseReportCsvAsync(Rows.ToList(), FromDate, ToDate);
                StatusMessage = $"Exported to: {path}";
            }
            catch (Exception ex)
            {
                ErrorMessage = "Export failed.";
                LoggingService.LogError(ex, "PurchaseReportViewModel.ExportCsv");
            }
            finally { IsLoading = false; }
        }
    }

    public class GroupedPurchases
    {
        public string Header    { get; }
        public List<PurchaseReportRow> Rows { get; }
        public decimal Total    => Rows.Sum(r => r.LineTotal);
        public int Count        => Rows.Count;

        public GroupedPurchases(string header, List<PurchaseReportRow> rows)
        {
            Header = header;
            Rows   = rows;
        }
    }
}
