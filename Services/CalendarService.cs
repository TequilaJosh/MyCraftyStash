using MyCraftyStash.Data;
using MyCraftyStash.Models;
using Microsoft.EntityFrameworkCore;

namespace MyCraftyStash.Services
{
    public class CalendarService
    {
        public async Task<List<CalendarEvent>> GetAllEventsAsync()
        {
            try
            {
                using var ctx = new InventoryDbContext();
                return await ctx.CalendarEvents
                    .OrderBy(e => e.EventDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "GetAllEventsAsync");
                throw;
            }
        }

        public async Task<List<CalendarEvent>> GetEventsForMonthAsync(int year, int month)
        {
            try
            {
                // FIX #8: Use a date range instead of Year()/Month() SQL functions.
                // Date range queries use the IX_calendar_events_event_date index efficiently.
                // Year()/Month() functions force a full table scan on older SQL Server versions.
                var firstDay = new DateTime(year, month, 1);
                var firstDayNextMonth = firstDay.AddMonths(1);

                using var ctx = new InventoryDbContext();
                // SQLite can't translate ORDER BY on TimeSpan (it's stored as
                // TEXT, no native ordering), so sort by date in SQL and then
                // by time in memory after the rows are materialised.
                var rows = await ctx.CalendarEvents
                    .Where(e => e.EventDate >= firstDay && e.EventDate < firstDayNextMonth)
                    .OrderBy(e => e.EventDate)
                    .ToListAsync();
                return rows
                    .OrderBy(e => e.EventDate)
                    .ThenBy(e => e.EventTime ?? TimeSpan.Zero)
                    .ToList();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "GetEventsForMonthAsync");
                throw;
            }
        }

        public async Task<List<CalendarEvent>> GetUpcomingRemindersAsync()
        {
            try
            {
                using var ctx = new InventoryDbContext();
                // Fetch only undismissed future events - ShouldRemind filtering happens in memory
                // because it uses computed DateTime logic that can't be expressed in SQL
                var allUpcoming = await ctx.CalendarEvents
                    .Where(e => !e.ReminderDismissed && e.EventDate >= DateTime.Today)
                    .ToListAsync();

                return allUpcoming.Where(e => e.ShouldRemind).ToList();
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "GetUpcomingRemindersAsync");
                throw;
            }
        }

        public async Task<CalendarEvent> AddEventAsync(CalendarEvent evt)
        {
            try
            {
                using var ctx = new InventoryDbContext();
                evt.CreatedAt = DateTime.Now;
                ctx.CalendarEvents.Add(evt);
                await ctx.SaveChangesAsync();
                return evt;
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "AddEventAsync");
                throw;
            }
        }

        public async Task<CalendarEvent> UpdateEventAsync(CalendarEvent evt)
        {
            try
            {
                using var ctx = new InventoryDbContext();
                evt.UpdatedAt = DateTime.Now;
                ctx.CalendarEvents.Update(evt);
                await ctx.SaveChangesAsync();
                return evt;
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "UpdateEventAsync");
                throw;
            }
        }

        public async Task DismissReminderAsync(int id)
        {
            try
            {
                using var ctx = new InventoryDbContext();
                var evt = await ctx.CalendarEvents.FindAsync(id);
                if (evt != null)
                {
                    evt.ReminderDismissed = true;
                    evt.UpdatedAt = DateTime.Now;
                    await ctx.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "DismissReminderAsync");
                throw;
            }
        }

        public async Task DeleteEventAsync(int id)
        {
            try
            {
                using var ctx = new InventoryDbContext();
                var evt = await ctx.CalendarEvents.FindAsync(id);
                if (evt != null)
                {
                    ctx.CalendarEvents.Remove(evt);
                    await ctx.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogDatabaseError(ex, "DeleteEventAsync");
                throw;
            }
        }
    }
}
