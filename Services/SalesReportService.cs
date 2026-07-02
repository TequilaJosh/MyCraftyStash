using Microsoft.EntityFrameworkCore;
using MyCraftyStash.Data;

namespace MyCraftyStash.Services
{
    /// <summary>One sale line for the Sales report — the revenue-side mirror
    /// of <see cref="PurchaseReportRow"/>.</summary>
    public class SalesReportRow
    {
        public string ItemName    { get; set; } = string.Empty;
        public string ItemType    { get; set; } = string.Empty;
        public string? ItemNumber { get; set; }
        public DateTime? Date     { get; set; }
        public int Quantity       { get; set; }
        public decimal SalePrice  { get; set; }
        public decimal LineTotal  { get; set; }
    }

    public class SalesReportService
    {
        private readonly Func<InventoryDbContext> _createContext;

        public SalesReportService(Func<InventoryDbContext> createContext)
        {
            _createContext = createContext;
        }

        /// <summary>All sales joined with item info, within an optional date range.</summary>
        public async Task<List<SalesReportRow>> GetSalesAsync(DateTime? from, DateTime? to)
        {
            try
            {
                using var ctx = _createContext();
                var q = ctx.ItemSales
                    .Include(s => s.Item)
                    .AsQueryable();

                if (from.HasValue) q = q.Where(s => s.DateSold >= from.Value);
                // Strict < next day so a sale dated the day AFTER `to`
                // (stored at midnight) isn't pulled into the range.
                if (to.HasValue)   q = q.Where(s => s.DateSold < to.Value.Date.AddDays(1));

                var rows = await q
                    .OrderByDescending(s => s.DateSold)
                    .Select(s => new SalesReportRow
                    {
                        ItemName   = s.Item != null ? s.Item.Name : "(deleted item)",
                        ItemType   = s.Item != null ? s.Item.Type : string.Empty,
                        ItemNumber = s.Item != null ? s.Item.ItemNumber : null,
                        Date       = s.DateSold,
                        Quantity   = s.Quantity,
                        SalePrice  = s.SalePrice,
                        LineTotal  = s.Quantity * s.SalePrice,
                    })
                    .ToListAsync();

                return rows;
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "SalesReportService.GetSalesAsync");
                throw;
            }
        }
    }
}
