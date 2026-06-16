using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyCraftyStash.Models;
using MyCraftyStash.Services;
using System.Collections.ObjectModel;

namespace MyCraftyStash.ViewModels
{
    public partial class CalendarViewModel : BaseViewModel
    {
        private readonly CalendarService _service;
        private readonly TeEventScraperService _teEvents = new();
        private readonly TeCalendarOcrService _teCalendarOcr = new();
        // FIX #11: Removed unused _mainVm field - was preventing GC of MainViewModel reference
        private List<CalendarEvent> _monthEvents = new();
        private Dictionary<DateTime, TeDailyCalendarCache> _teDailyLookup = new();

        // ── Calendar navigation ──────────────────────────────────────────
        [ObservableProperty] private int _currentYear;
        [ObservableProperty] private int _currentMonth;
        [ObservableProperty] private string _currentMonthLabel = string.Empty;
        [ObservableProperty] private ObservableCollection<CalendarDayCell> _dayCells = new();
        [ObservableProperty] private CalendarDayCell? _selectedDay;

        // ── Selected day events panel ────────────────────────────────────
        [ObservableProperty] private ObservableCollection<CalendarEvent> _selectedDayEvents = new();
        [ObservableProperty] private CalendarEvent? _selectedEvent;

        // ── TE shop hours (scraped from the storefront homepage) ─────────
        [ObservableProperty] private ObservableCollection<TeShopHoursLine> _shopHours = new();

        // ── Form state ───────────────────────────────────────────────────
        [ObservableProperty] private bool _isAdding;
        [ObservableProperty] private bool _isEditing;
        [ObservableProperty] private string _statusMessage = string.Empty;

        // ── Edit fields ──────────────────────────────────────────────────
        [ObservableProperty] private string _editTitle = string.Empty;
        [ObservableProperty] private string _editDescription = string.Empty;
        [ObservableProperty] private DateTime _editDate = DateTime.Today;
        [ObservableProperty] private bool _editIsAllDay = true;
        [ObservableProperty] private string _editTimeHour = "12";
        [ObservableProperty] private string _editTimeMinute = "00";
        [ObservableProperty] private string _editTimeAmPm = "PM";
        [ObservableProperty] private string _editColor = "#D61F26";
        [ObservableProperty] private int _editReminderMinutes = 1440;
        [ObservableProperty] private string _editReminderLabel = "1 Day Before";

        public List<string> ReminderOptions { get; } = new()
        {
            "At Event Time",
            "15 Minutes Before",
            "30 Minutes Before",
            "1 Hour Before",
            "2 Hours Before",
            "1 Day Before",
            "2 Days Before",
            "1 Week Before"
        };

        public List<string> ColorOptions { get; } = new()
        {
            "#D61F26", // Red (app primary)
            "#2563EB", // Blue
            "#16A34A", // Green
            "#D97706", // Amber
            "#7C3AED", // Purple
            "#DB2777", // Pink
            "#0891B2", // Cyan
            "#374151"  // Dark gray
        };

        public List<string> AmPmOptions { get; } = new() { "AM", "PM" };

        public CalendarViewModel(MainViewModel mainVm)
        {
            // FIX #11: mainVm parameter kept for API compatibility but not stored
            _service = new CalendarService();
            var now = DateTime.Today;
            _currentYear = now.Year;
            _currentMonth = now.Month;
            UpdateMonthLabel();
        }

        // FIX #12: Auto-clear status message after 4 seconds so it doesn't stay on screen forever
        private CancellationTokenSource? _statusCts;

        private void SetStatusMessage(string message)
        {
            _statusCts?.Cancel();
            _statusCts = new CancellationTokenSource();
            StatusMessage = message;
            var token = _statusCts.Token;
            Task.Delay(4000, token).ContinueWith(t =>
            {
                if (!t.IsCanceled)
                    System.Windows.Application.Current?.Dispatcher.Invoke(() => StatusMessage = string.Empty);
            }, TaskScheduler.Default);
        }
        [RelayCommand]
        public async Task LoadCalendar()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;
                _monthEvents = await _service.GetEventsForMonthAsync(CurrentYear, CurrentMonth);

                // Overlay TE events scraped from the Square site. Read-only,
                // distinct color, badge in the day cell. Cache is kept fresh
                // by the background refresh kicked off at app start.
                var firstOfMonth = new DateTime(CurrentYear, CurrentMonth, 1);
                var lastOfMonth = firstOfMonth.AddMonths(1).AddDays(-1);
                var teEvents = _teEvents.GetCached(firstOfMonth, lastOfMonth);
                foreach (var te in teEvents)
                {
                    _monthEvents.Add(new CalendarEvent
                    {
                        Id          = -te.Id, // negative so it can never collide with a real event id
                        Title       = te.Title,
                        Description = te.Description,
                        EventDate   = te.EventDate,
                        IsAllDay    = true,
                        Color       = "#5F7A5C", // cozy-emblem green
                        IsFromTe    = true,
                        TeUrl       = te.Url,
                    });
                }

                // Pull OCR'd per-day data for the month being rendered so the
                // grid can show TE's published hours/status/events per cell.
                var dailyRange = _teCalendarOcr.GetCachedForRange(
                    firstOfMonth.AddDays(-7),    // include some prev-month carry-over rows
                    lastOfMonth.AddDays(7));
                // GroupBy+Last, not ToDictionary: overlapping OCR'd month
                // images can produce two cache rows for the same Date, and a
                // plain ToDictionary throws on the duplicate key, failing the
                // whole calendar load. Last() keeps the most recently written.
                _teDailyLookup = dailyRange
                    .GroupBy(d => d.Date)
                    .ToDictionary(g => g.Key, g => g.Last());

                BuildCalendarGrid();
                if (SelectedDay != null)
                {
                    var cell = DayCells.FirstOrDefault(c => c.Date == SelectedDay.Date);
                    if (cell != null) SelectDay(cell);
                }

                // Shop hours are global, not per-month, but loading them here
                // keeps the calendar self-contained — and is cheap (single
                // KvSetting row read).
                ShopHours = new ObservableCollection<TeShopHoursLine>(_teEvents.GetCachedShopHours());
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load calendar: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ── Navigation ────────────────────────────────────────────────────
        [RelayCommand]
        private async Task PreviousMonth()
        {
            CurrentMonth--;
            if (CurrentMonth < 1) { CurrentMonth = 12; CurrentYear--; }
            UpdateMonthLabel();
            await LoadCalendar();
        }

        [RelayCommand]
        private async Task NextMonth()
        {
            CurrentMonth++;
            if (CurrentMonth > 12) { CurrentMonth = 1; CurrentYear++; }
            UpdateMonthLabel();
            await LoadCalendar();
        }

        [RelayCommand]
        private async Task GoToToday()
        {
            CurrentYear = DateTime.Today.Year;
            CurrentMonth = DateTime.Today.Month;
            UpdateMonthLabel();
            await LoadCalendar();
            var todayCell = DayCells.FirstOrDefault(c => c.IsToday);
            if (todayCell != null) SelectDay(todayCell);
        }

        // ── Day selection ─────────────────────────────────────────────────
        [RelayCommand]
        private void SelectDayCell(CalendarDayCell cell)
        {
            SelectDay(cell);
        }

        private void SelectDay(CalendarDayCell cell)
        {
            if (SelectedDay != null) SelectedDay.IsSelected = false;
            SelectedDay = cell;
            cell.IsSelected = true;
            SelectedDayEvents = new ObservableCollection<CalendarEvent>(cell.Events);
            SelectedEvent = null;
            IsAdding = false;
            IsEditing = false;
        }

        // ── Add / Edit / Delete ───────────────────────────────────────────
        [RelayCommand]
        private void StartAdd()
        {
            IsAdding = true;
            IsEditing = false;
            SelectedEvent = null;
            ClearForm();
            if (SelectedDay != null) EditDate = SelectedDay.Date;
        }

        [RelayCommand]
        private void StartEdit(CalendarEvent? evt = null)
        {
            if (evt != null) SelectedEvent = evt;
            if (SelectedEvent == null) return;
            // TE-sourced events are read-only mirrors of the scraped cache.
            if (SelectedEvent.IsFromTe)
            {
                SetStatusMessage("Taylored Expressions events are read-only.");
                return;
            }
            IsEditing = true;
            IsAdding = false;
            PopulateForm(SelectedEvent);
        }

        [RelayCommand]
        private void CancelForm()
        {
            IsAdding = false;
            IsEditing = false;
        }

        [RelayCommand]
        private void SetColor(string color)
        {
            EditColor = color;
        }

        /// <summary>
        /// Opens a Taylored Expressions registration URL in the user's default browser.
        /// Bound to the "Sign up" link rendered on TE-sourced events in the day panel.
        /// </summary>
        [RelayCommand]
        private void OpenEventUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = uri.AbsoluteUri,
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, $"CalendarViewModel.OpenEventUrl({url})");
            }
        }

        [RelayCommand]
        private async Task SaveEvent()
        {
            if (string.IsNullOrWhiteSpace(EditTitle))
            {
                ErrorMessage = "Event title is required.";
                return;
            }

            try
            {
                IsLoading = true;
                ErrorMessage = null;
                var reminderMins = ReminderLabelToMinutes(EditReminderLabel);

                TimeSpan? time = null;
                if (!EditIsAllDay)
                {
                    if (int.TryParse(EditTimeHour, out var h) && int.TryParse(EditTimeMinute, out var m))
                    {
                        if (EditTimeAmPm == "PM" && h < 12) h += 12;
                        if (EditTimeAmPm == "AM" && h == 12) h = 0;
                        time = new TimeSpan(h, m, 0);
                    }
                }

                if (IsAdding)
                {
                    var newEvt = new CalendarEvent
                    {
                        Title = EditTitle.Trim(),
                        Description = string.IsNullOrWhiteSpace(EditDescription) ? null : EditDescription.Trim(),
                        EventDate = EditDate.Date,
                        IsAllDay = EditIsAllDay,
                        EventTime = time,
                        Color = EditColor,
                        ReminderMinutesBefore = reminderMins
                    };
                    await _service.AddEventAsync(newEvt);
                    SetStatusMessage($"Event \"{newEvt.Title}\" added!");
                }
                else if (IsEditing && SelectedEvent != null)
                {
                    SelectedEvent.Title = EditTitle.Trim();
                    SelectedEvent.Description = string.IsNullOrWhiteSpace(EditDescription) ? null : EditDescription.Trim();
                    SelectedEvent.EventDate = EditDate.Date;
                    SelectedEvent.IsAllDay = EditIsAllDay;
                    SelectedEvent.EventTime = time;
                    SelectedEvent.Color = EditColor;
                    SelectedEvent.ReminderMinutesBefore = reminderMins;
                    SelectedEvent.ReminderDismissed = false;
                    await _service.UpdateEventAsync(SelectedEvent);
                    SetStatusMessage($"Event \"{SelectedEvent.Title}\" updated!");
                }

                IsAdding = false;
                IsEditing = false;
                await LoadCalendar();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to save: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task DeleteEvent(CalendarEvent? evt = null)
        {
            var target = evt ?? SelectedEvent;
            if (target == null) return;
            if (target.IsFromTe)
            {
                SetStatusMessage("Taylored Expressions events are read-only.");
                return;
            }
            try
            {
                IsLoading = true;
                var title = target.Title;
                await _service.DeleteEventAsync(target.Id);
                SelectedEvent = null;
                IsEditing = false;
                SetStatusMessage($"Deleted \"{title}\".");
                await LoadCalendar();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to delete: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────
        private void BuildCalendarGrid()
        {
            var cells = new ObservableCollection<CalendarDayCell>();
            var firstOfMonth = new DateTime(CurrentYear, CurrentMonth, 1);
            var daysInMonth = DateTime.DaysInMonth(CurrentYear, CurrentMonth);

            // Day of week offset (Sunday=0)
            int startOffset = (int)firstOfMonth.DayOfWeek;

            // Fill blanks from previous month
            var prevMonthEnd = firstOfMonth.AddDays(-1);
            for (int i = startOffset - 1; i >= 0; i--)
            {
                var d = prevMonthEnd.AddDays(-i);
                cells.Add(new CalendarDayCell { Date = d, IsCurrentMonth = false, IsToday = d.Date == DateTime.Today });
            }

            // Current month days
            for (int day = 1; day <= daysInMonth; day++)
            {
                var d = new DateTime(CurrentYear, CurrentMonth, day);
                var events = _monthEvents.Where(e => e.EventDate.Date == d.Date).ToList();
                var cell = new CalendarDayCell
                {
                    Date = d,
                    IsCurrentMonth = true,
                    IsToday = d.Date == DateTime.Today,
                    Events = new System.Collections.ObjectModel.ObservableCollection<CalendarEvent>(events)
                };
                ApplyTeDailyOverlay(cell);
                cells.Add(cell);
            }

            // Fill remaining cells to complete grid (always 6 rows = 42 cells)
            int remaining = 42 - cells.Count;
            var nextMonthStart = firstOfMonth.AddMonths(1);
            for (int i = 0; i < remaining; i++)
            {
                var d = nextMonthStart.AddDays(i);
                cells.Add(new CalendarDayCell { Date = d, IsCurrentMonth = false, IsToday = d.Date == DateTime.Today });
            }

            DayCells = cells;
        }

        private void UpdateMonthLabel()
        {
            CurrentMonthLabel = new DateTime(CurrentYear, CurrentMonth, 1).ToString("MMMM yyyy");
        }

        /// <summary>
        /// Paint a day cell with whatever the OCR'd published calendar said
        /// about that date. No-op when there's no row in the daily cache.
        /// </summary>
        private void ApplyTeDailyOverlay(CalendarDayCell cell)
        {
            if (!_teDailyLookup.TryGetValue(cell.Date.Date, out var daily)) return;
            cell.TeHours = daily.Hours;
            cell.TeStatus = daily.Status;
            if (!string.IsNullOrWhiteSpace(daily.EventsJson))
            {
                try
                {
                    var list = System.Text.Json.JsonSerializer.Deserialize<List<TeDailyEventLine>>(daily.EventsJson);
                    if (list != null && list.Count > 0)
                        cell.TeEvents = new System.Collections.ObjectModel.ObservableCollection<TeDailyEventLine>(list);
                }
                catch (Exception ex)
                {
                    LoggingService.LogError(ex, "CalendarViewModel.ApplyTeDailyOverlay.DeserializeEvents");
                }
            }
        }

        private void ClearForm()
        {
            EditTitle = string.Empty;
            EditDescription = string.Empty;
            EditDate = SelectedDay?.Date ?? DateTime.Today;
            EditIsAllDay = true;
            EditTimeHour = "12";
            EditTimeMinute = "00";
            EditTimeAmPm = "PM";
            EditColor = "#D61F26";
            EditReminderLabel = "1 Day Before";
            EditReminderMinutes = 1440;
        }

        private void PopulateForm(CalendarEvent evt)
        {
            EditTitle = evt.Title;
            EditDescription = evt.Description ?? string.Empty;
            EditDate = evt.EventDate.Date;
            EditIsAllDay = evt.IsAllDay;
            EditColor = evt.Color ?? "#D61F26";
            EditReminderLabel = MinutesToReminderLabel(evt.ReminderMinutesBefore);
            EditReminderMinutes = evt.ReminderMinutesBefore;

            if (evt.EventTime.HasValue)
            {
                var t = evt.EventTime.Value;
                int h = t.Hours;
                EditTimeMinute = t.Minutes.ToString("D2");
                if (h == 0) { EditTimeHour = "12"; EditTimeAmPm = "AM"; }
                else if (h < 12) { EditTimeHour = h.ToString(); EditTimeAmPm = "AM"; }
                else if (h == 12) { EditTimeHour = "12"; EditTimeAmPm = "PM"; }
                else { EditTimeHour = (h - 12).ToString(); EditTimeAmPm = "PM"; }
            }
            else
            {
                EditTimeHour = "12"; EditTimeMinute = "00"; EditTimeAmPm = "PM";
            }
        }

        private static int ReminderLabelToMinutes(string label) => label switch
        {
            "At Event Time" => 0,
            "15 Minutes Before" => 15,
            "30 Minutes Before" => 30,
            "1 Hour Before" => 60,
            "2 Hours Before" => 120,
            "1 Day Before" => 1440,
            "2 Days Before" => 2880,
            "1 Week Before" => 10080,
            _ => 1440
        };

        private static string MinutesToReminderLabel(int mins) => mins switch
        {
            0 => "At Event Time",
            15 => "15 Minutes Before",
            30 => "30 Minutes Before",
            60 => "1 Hour Before",
            120 => "2 Hours Before",
            1440 => "1 Day Before",
            2880 => "2 Days Before",
            10080 => "1 Week Before",
            _ => "1 Day Before"
        };
    }
}
