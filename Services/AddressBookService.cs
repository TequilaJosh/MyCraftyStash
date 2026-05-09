using MyCraftyStash.Data;
using MyCraftyStash.Models;
using Microsoft.EntityFrameworkCore;

namespace MyCraftyStash.Services
{
    public class AddressBookService
    {
        public async Task<List<AddressBookEntry>> GetAllEntriesAsync()
        {
            try
            {
                using var ctx = new InventoryDbContext();
                return await ctx.AddressBookEntries
                    .OrderBy(e => e.LastName)
                    .ThenBy(e => e.FirstName)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "GetAllEntriesAsync");
                throw;
            }
        }

        public async Task<List<AddressBookEntry>> SearchEntriesAsync(string query)
        {
            try
            {
                using var ctx = new InventoryDbContext();
                var lower = query.ToLower();
                return await ctx.AddressBookEntries
                    .Where(e =>
                        e.FirstName.ToLower().Contains(lower) ||
                        (e.LastName != null && e.LastName.ToLower().Contains(lower)) ||
                        (e.City != null && e.City.ToLower().Contains(lower)) ||
                        (e.State != null && e.State.ToLower().Contains(lower)) ||
                        (e.Email != null && e.Email.ToLower().Contains(lower)) ||
                        (e.Notes != null && e.Notes.ToLower().Contains(lower)))
                    .OrderBy(e => e.LastName)
                    .ThenBy(e => e.FirstName)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "SearchEntriesAsync");
                throw;
            }
        }

        public async Task<AddressBookEntry> AddEntryAsync(AddressBookEntry entry)
        {
            try
            {
                using var ctx = new InventoryDbContext();
                entry.CreatedAt = DateTime.Now;
                ctx.AddressBookEntries.Add(entry);
                await ctx.SaveChangesAsync();
                return entry;
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "AddEntryAsync");
                throw;
            }
        }

        public async Task<AddressBookEntry> UpdateEntryAsync(AddressBookEntry entry)
        {
            try
            {
                using var ctx = new InventoryDbContext();
                entry.UpdatedAt = DateTime.Now;
                ctx.AddressBookEntries.Update(entry);
                await ctx.SaveChangesAsync();
                return entry;
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "UpdateEntryAsync");
                throw;
            }
        }

        public async Task DeleteEntryAsync(int id)
        {
            try
            {
                using var ctx = new InventoryDbContext();
                var entry = await ctx.AddressBookEntries.FindAsync(id);
                if (entry != null)
                {
                    ctx.AddressBookEntries.Remove(entry);
                    await ctx.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "DeleteEntryAsync");
                throw;
            }
        }
    }
}
