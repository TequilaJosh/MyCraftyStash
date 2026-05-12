using CommunityToolkit.Mvvm.ComponentModel;
using MyCraftyStash.Models;
using System.Collections.ObjectModel;

namespace MyCraftyStash.ViewModels
{
    public partial class CalendarDayCell : ObservableObject
    {
        [ObservableProperty]
        private DateTime _date;

        [ObservableProperty]
        private bool _isCurrentMonth;

        [ObservableProperty]
        private bool _isToday;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private ObservableCollection<CalendarEvent> _events = new();

        public bool HasEvents => Events.Count > 0;

        // ── TE-published-calendar overlay (OCR'd from the storefront PDFs) ──
        /// <summary>Store hours for this day, e.g. "10:00 AM - 4:00 PM". Null when no hours line is shown.</summary>
        [ObservableProperty] private string? _teHours;
        /// <summary>"Closed", "Open: 10am - 3pm". Null when no banner.</summary>
        [ObservableProperty] private string? _teStatus;
        /// <summary>Event chips from the calendar image (separate from /classes events).</summary>
        [ObservableProperty] private ObservableCollection<TeDailyEventLine> _teEvents = new();

        public bool HasTeHours => !string.IsNullOrEmpty(TeHours);
        public bool HasTeStatus => !string.IsNullOrEmpty(TeStatus);
        public bool HasTeEvents => TeEvents.Count > 0;

        partial void OnTeHoursChanged(string? value) => OnPropertyChanged(nameof(HasTeHours));
        partial void OnTeStatusChanged(string? value) => OnPropertyChanged(nameof(HasTeStatus));
    }
}
