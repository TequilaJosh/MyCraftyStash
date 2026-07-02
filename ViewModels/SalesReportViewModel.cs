using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using MyCraftyStash.Services;

namespace MyCraftyStash.ViewModels
{
    public partial class SalesReportViewModel : BaseViewModel
    {
        private readonly SalesReportService _reportService;
        private readonly ExportService _exportService;

        [ObservableProperty] private ObservableCollection<SalesReportRow> _rows = new();
        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _errorMessage = string.Empty;
        [ObservableProperty] private DateTime _fromDate = DateTime.Today.AddMonths(-3);
        [ObservableProperty] private DateTime _toDate = DateTime.Today;
        [ObservableProperty] private string _selectedGroupBy = "Month";
        [ObservableProperty] private string _statusMessage = string.Empty;

        public List<string> GroupByOptions { get; } = new() { "Month", "Type", "None" };

        // Computed summary
        public decimal TotalRevenue   => Rows.Sum(r => r.LineTotal);
        public int     TotalItems     => Rows.Sum(r => r.Quantity);
        public int     UniqueItems    => Rows.Select(r => r.ItemName).Distinct().Count();
        public decimal AveragePerItem => TotalItems > 0 ? TotalRevenue / TotalItems : 0;

        public IEnumerable<GroupedSales> GroupedRows
        {
            get
            {
                if (!Rows.Any()) return Enumerable.Empty<GroupedSales>();
                return SelectedGroupBy switch
                {
                    // Order by the actual month, not the formatted label.
                    "Month"  => Rows.GroupBy(r => r.Date.HasValue
                                        ? new DateTime(r.Date.Value.Year, r.Date.Value.Month, 1)
                                        : DateTime.MinValue)
                                    .OrderByDescending(g => g.Key)
                                    .Select(g => new GroupedSales(
                                        g.Key == DateTime.MinValue ? "Unknown" : g.Key.ToString("MMMM yyyy"),
                                        g.ToList())),
                    "Type"   => Rows.GroupBy(r => string.IsNullOrEmpty(r.ItemType) ? "Unknown" : r.ItemType)
                                    .OrderBy(g => g.Key)
                                    .Select(g => new GroupedSales(g.Key, g.ToList())),
                    _        => new[] { new GroupedSales("All Sales", Rows.ToList()) },
                };
            }
        }

        public SalesReportViewModel(SalesReportService reportService, ExportService exportService)
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
                var data = await _reportService.GetSalesAsync(FromDate, ToDate);
                Rows = new ObservableCollection<SalesReportRow>(data);
                OnPropertyChanged(nameof(TotalRevenue));
                OnPropertyChanged(nameof(TotalItems));
                OnPropertyChanged(nameof(UniqueItems));
                OnPropertyChanged(nameof(AveragePerItem));
                OnPropertyChanged(nameof(GroupedRows));
                StatusMessage = Rows.Count == 0 ? "No sales found for this period." : string.Empty;
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to load sales history.";
                LoggingService.LogError(ex, "SalesReportViewModel.LoadReport");
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
                var path = await _exportService.ExportSalesReportCsvAsync(Rows.ToList(), FromDate, ToDate);
                StatusMessage = $"Exported to: {path}";
            }
            catch (Exception ex)
            {
                ErrorMessage = "Export failed.";
                LoggingService.LogError(ex, "SalesReportViewModel.ExportCsv");
            }
            finally { IsLoading = false; }
        }
    }

    public class GroupedSales
    {
        public string Header { get; }
        public List<SalesReportRow> Rows { get; }
        public decimal Total => Rows.Sum(r => r.LineTotal);
        public int Count     => Rows.Count;

        public GroupedSales(string header, List<SalesReportRow> rows)
        {
            Header = header;
            Rows   = rows;
        }
    }
}
