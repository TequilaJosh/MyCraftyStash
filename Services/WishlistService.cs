using Microsoft.EntityFrameworkCore;
using MyCraftyStash.Data;
using MyCraftyStash.Models;

namespace MyCraftyStash.Services
{
    public class WishlistService
    {
        private readonly Func<InventoryDbContext> _createContext;

        public WishlistService(Func<InventoryDbContext> createContext)
        {
            _createContext = createContext;
        }

        // Schema is owned by EF Core migrations now; this stub keeps existing
        // call sites compiling without forcing a refactor.
        public Task EnsureTableAsync() => Task.CompletedTask;

        // ── Wishlist CRUD ────────────────────────────────────────────────────

        public async Task<List<Wishlist>> GetWishlistsAsync()
        {
            try
            {
                using var context = _createContext();
                return await context.Wishlists
                    .OrderBy(w => w.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "WishlistService.GetWishlistsAsync");
                throw;
            }
        }

        public async Task<Wishlist> CreateWishlistAsync(string name, string? color = null, string? description = null)
        {
            try
            {
                using var context = _createContext();
                var wishlist = new Wishlist
                {
                    Name = name.Trim(),
                    Color = string.IsNullOrWhiteSpace(color) ? null : color,
                    Description = string.IsNullOrWhiteSpace(description) ? null : description,
                    CreatedAt = DateTime.Now,
                };
                context.Wishlists.Add(wishlist);
                await context.SaveChangesAsync();
                return wishlist;
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"WishlistService.CreateWishlistAsync (name: {name})");
                throw;
            }
        }

        public async Task UpdateWishlistAsync(Wishlist wishlist)
        {
            try
            {
                using var context = _createContext();
                context.Wishlists.Update(wishlist);
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"WishlistService.UpdateWishlistAsync (id: {wishlist.Id})");
                throw;
            }
        }

        public async Task DeleteWishlistAsync(int id)
        {
            try
            {
                using var context = _createContext();
                // Items become unassigned (wishlist_id SET NULL) via FK cascade
                var wishlist = await context.Wishlists.FindAsync(id);
                if (wishlist != null)
                {
                    context.Wishlists.Remove(wishlist);
                    await context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"WishlistService.DeleteWishlistAsync (id: {id})");
                throw;
            }
        }

        // ── WishlistItem CRUD ────────────────────────────────────────────────

        /// <summary>
        /// Returns all items, or only items belonging to a specific wishlist when wishlistId is provided.
        /// </summary>
        public async Task<List<WishlistItem>> GetAllAsync(int? wishlistId = null)
        {
            try
            {
                using var context = _createContext();
                var query = context.WishlistItems.AsQueryable();
                if (wishlistId.HasValue)
                    query = query.Where(w => w.WishlistId == wishlistId.Value);
                return await query
                    .OrderByDescending(w => w.Priority)
                    .ThenBy(w => w.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "WishlistService.GetAllAsync");
                throw;
            }
        }

        public async Task<WishlistItem> AddAsync(WishlistItem item)
        {
            try
            {
                using var context = _createContext();
                item.CreatedAt = DateTime.Now;
                context.WishlistItems.Add(item);
                await context.SaveChangesAsync();
                return item;
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"WishlistService.AddAsync (name: {item.Name})");
                throw;
            }
        }

        public async Task UpdateAsync(WishlistItem item)
        {
            try
            {
                using var context = _createContext();
                context.WishlistItems.Update(item);
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"WishlistService.UpdateAsync (id: {item.Id})");
                throw;
            }
        }

        public async Task DeleteAsync(int id)
        {
            try
            {
                using var context = _createContext();
                var item = await context.WishlistItems.FindAsync(id);
                if (item != null)
                {
                    context.WishlistItems.Remove(item);
                    await context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, $"WishlistService.DeleteAsync (id: {id})");
                throw;
            }
        }

        /// <summary>
        /// Converts a wishlist item to a full inventory Item and deletes it from the wishlist.
        /// Pass <paramref name="typeOverride"/> to use a specific inventory type
        /// (e.g. the result of <see cref="WishlistTypeMatcher.FindBestMatch"/> or a
        /// user choice from <c>TypePickerDialog</c>). When null, falls back to the
        /// wishlist item's raw Type, then "Stamp".
        /// </summary>
        public async Task<Item> MoveToInventoryAsync(
            WishlistItem wishlistItem,
            InventoryService inventoryService,
            string? typeOverride = null)
        {
            var newItem = new Item
            {
                Name          = wishlistItem.Name,
                Type          = !string.IsNullOrWhiteSpace(typeOverride)
                                    ? typeOverride
                                    : (wishlistItem.Type ?? "Stamp"),
                ItemNumber    = wishlistItem.ItemNumber,
                Theme         = wishlistItem.Theme,
                Price         = wishlistItem.Price,
                ImageUrl      = wishlistItem.ImageUrl,
                Notes         = wishlistItem.Notes,
                PurchasedFrom = wishlistItem.PurchasedFrom,
                DatePurchased = DateTime.Today,
            };

            var created = await inventoryService.CreateItemAsync(newItem);
            await DeleteAsync(wishlistItem.Id);
            return created;
        }
    }
}
