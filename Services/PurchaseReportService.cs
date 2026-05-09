using Microsoft.EntityFrameworkCore;
using MyCraftyStash.Data;
using MyCraftyStash.Models;

namespace MyCraftyStash.Services
{
    public class PurchaseReportRow
    {
        public string ItemName      { get; set; } = string.Empty;
        public string ItemType      { get; set; } = string.Empty;
        public string? ItemNumber   { get; set; }
        public string? PurchasedFrom { get; set; }
        public DateTime? Date       { get; set; }
        public int Quantity         { get; set; }
        public decimal PricePerItem { get; set; }
        public decimal LineTotal    { get; set; }
    }

    public class PurchaseReportService
    {
        private readonly Func<InventoryDbContext> _createContext;

        public PurchaseReportService(Func<InventoryDbContext> createContext)
        {
            _createContext = createContext;
        }

        /// <summary>All purchases joined with item info, within an optional date range.</summary>
        public async Task<List<PurchaseReportRow>> GetPurchasesAsync(DateTime? from, DateTime? to)
        {
            try
            {
                using var ctx = _createContext();
                var q = ctx.ItemPurchases
                    .Include(p => p.Item)
                    .AsQueryable();

                if (from.HasValue) q = q.Where(p => p.DatePurchased >= from.Value);
                if (to.HasValue)   q = q.Where(p => p.DatePurchased <= to.Value.AddDays(1));

                var rows = await q
                    .OrderByDescending(p => p.DatePurchased)
                    .Select(p => new PurchaseReportRow
                    {
                        ItemName     = p.Item != null ? p.Item.Name : "(deleted item)",
                        ItemType     = p.Item != null ? p.Item.Type : string.Empty,
                        ItemNumber   = p.Item != null ? p.Item.ItemNumber : null,
                        PurchasedFrom = p.Item != null ? p.Item.PurchasedFrom : null,
                        Date         = p.DatePurchased,
                        Quantity     = p.Quantity,
                        PricePerItem = p.PricePerItem,
                        LineTotal    = p.Quantity * p.PricePerItem,
                    })
                    .ToListAsync();

                return rows;
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "PurchaseReportService.GetPurchasesAsync");
                throw;
            }
        }
    }
}
