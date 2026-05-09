using MyCraftyStash.Data;
using Microsoft.EntityFrameworkCore;

namespace MyCraftyStash.Services
{
    /// <summary>
    /// Lightweight helper for probing whether the SQL Server backing this app
    /// is reachable. Used at startup so the shell can warn the user (and
    /// individual views can show empty/offline states) instead of crashing
    /// the whole app on a connection failure.
    /// </summary>
    public static class DatabaseHealthService
    {
        // Cached result of the most recent probe. UI bindings can read this
        // synchronously after CheckAsync has run once during startup.
        public static bool IsAvailable { get; private set; } = true;
        public static string? LastError { get; private set; }

        /// <summary>
        /// Opens a short-timeout connection to the configured database. Logs
        /// the failure (if any) and returns the cached result. Safe to call
        /// from a background thread.
        /// </summary>
        public static async Task<bool> CheckAsync()
        {
            try
            {
                using var ctx = new InventoryDbContext();
                // CanConnectAsync issues a lightweight "SELECT 1" — fast when the
                // server is up, fails fast when it isn't (subject to the
                // connection-timeout in appsettings.json).
                IsAvailable = await ctx.Database.CanConnectAsync();
                LastError  = IsAvailable ? null : "Database CanConnect returned false";
                if (!IsAvailable)
                    LoggingService.LogWarning("DatabaseHealthService: CanConnect returned false — running in offline mode.");
                return IsAvailable;
            }
            catch (Exception ex)
            {
                IsAvailable = false;
                LastError   = ex.Message;
                LoggingService.LogDatabaseError(ex, "DatabaseHealthService.CheckAsync");
                return false;
            }
        }
    }
}
