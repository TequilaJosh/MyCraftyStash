using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyCraftyStash.Models
{
    public class CalendarEvent
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        public DateTime EventDate { get; set; }

        public TimeSpan? EventTime { get; set; }

        /// <summary>How many minutes before the event to show the reminder popup (0 = on the day at startup)</summary>
        public int ReminderMinutesBefore { get; set; } = 1440; // Default: 1 day before

        [MaxLength(50)]
        public string? Color { get; set; } = "#D61F26";

        public bool IsAllDay { get; set; } = true;

        public bool ReminderDismissed { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        [NotMapped]
        public DateTime EventDateTime => IsAllDay
            ? EventDate.Date
            : EventDate.Date + (EventTime ?? TimeSpan.Zero);

        [NotMapped]
        public bool IsUpcoming => EventDateTime >= DateTime.Now;

        [NotMapped]
        public string DisplayTime => IsAllDay
            ? EventDate.ToString("MMM d, yyyy")
            : EventDateTime.ToString("MMM d, yyyy h:mm tt");

        [NotMapped]
        public bool ShouldRemind
        {
            get
            {
                if (ReminderDismissed) return false;
                var reminderTime = EventDateTime.AddMinutes(-ReminderMinutesBefore);
                return DateTime.Now >= reminderTime && EventDateTime >= DateTime.Now.Date;
            }
        }

        // ── Overlay fields for TE-sourced events ────────────────────────────
        // TE events live in a separate cache table but are merged into the
        // calendar render layer as read-only CalendarEvent instances flagged
        // here. The view binds these to colour/icon/badge differently and
        // disables the edit/delete commands when IsFromTe is true.

        [NotMapped] public bool IsFromTe { get; set; }
        [NotMapped] public string? TeUrl { get; set; }
    }
}
