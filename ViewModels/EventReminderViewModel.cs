using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyCraftyStash.Models;
using MyCraftyStash.Services;
using System.Collections.ObjectModel;

namespace MyCraftyStash.ViewModels
{
    /// <summary>
    /// A thin wrapper around a CalendarEvent that adds a display-only "days until" string.
    /// </summary>
    public class ReminderEventItem : CalendarEvent
    {
        public string DaysUntilDisplay
        {
            get
            {
                var diff = (EventDate.Date - DateTime.Today).Days;
                return diff switch
                {
                    0 => "📅 Today!",
                    1 => "⏰ Tomorrow",
                    _ => $"⏳ In {diff} day(s)"
                };
            }
        }
    }

    public partial class EventReminderViewModel : ObservableObject
    {
        private readonly CalendarService _service;

        [ObservableProperty]
        private ObservableCollection<ReminderEventItem> _events = new();

        [ObservableProperty]
        private int _eventCount;

        public event Action? CloseRequested;

        public EventReminderViewModel(List<CalendarEvent> reminders)
        {
            _service = new CalendarService();
            var items = reminders.Select(e => new ReminderEventItem
            {
                Id = e.Id,
                Title = e.Title,
                Description = e.Description,
                EventDate = e.EventDate,
                EventTime = e.EventTime,
                IsAllDay = e.IsAllDay,
                Color = e.Color,
                ReminderMinutesBefore = e.ReminderMinutesBefore,
                ReminderDismissed = e.ReminderDismissed,
                CreatedAt = e.CreatedAt
            }).ToList();
            Events = new ObservableCollection<ReminderEventItem>(items);
            EventCount = Events.Count;
        }

        [RelayCommand]
        private async Task Dismiss(ReminderEventItem item)
        {
            try
            {
                await _service.DismissReminderAsync(item.Id);
                Events.Remove(item);
                EventCount = Events.Count;
                if (Events.Count == 0)
                    CloseRequested?.Invoke();
            }
            catch { /* silent – don't block UI */ }
        }

        [RelayCommand]
        private async Task DismissAll()
        {
            try
            {
                var tasks = Events.Select(e => _service.DismissReminderAsync(e.Id));
                await Task.WhenAll(tasks);
                Events.Clear();
                EventCount = 0;
            }
            catch { /* silent */ }
            finally
            {
                CloseRequested?.Invoke();
            }
        }

        [RelayCommand]
        private void Close() => CloseRequested?.Invoke();
    }
}
