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
    }
}
