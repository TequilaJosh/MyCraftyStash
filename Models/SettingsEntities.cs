namespace MyCraftyStash.Models
{
    /// <summary>Generic key/value setting (CardSize, TextSize, DarkMode, etc.).</summary>
    public class KvSetting
    {
        public string Key { get; set; } = string.Empty;
        public string? Value { get; set; }
    }

    /// <summary>Per-item-type sort order tuple.</summary>
    public class TypeSortOrderEntry
    {
        public string Type { get; set; } = string.Empty;
        public string Sort1 { get; set; } = "Color";
        public string Sort2 { get; set; } = string.Empty;
        public string Sort3 { get; set; } = string.Empty;
    }

    /// <summary>Per-brush hex override (e.g. "PrimaryBrush" -> "#FF8800").</summary>
    public class CustomColor
    {
        public string BrushKey { get; set; } = string.Empty;
        public string Hex { get; set; } = string.Empty;
    }

    /// <summary>
    /// One row per shared config list (types, themes, locations, colors, …).
    /// Content is the raw text body — newline-separated lines for the line
    /// lists, JSON for the structured ones — so existing parsers stay unchanged.
    /// </summary>
    public class ConfigList
    {
        public string Name { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    /// <summary>
    /// One row per (external system + external code) -> TE color mapping.
    /// Most are 1:1 (one DMC# -> one TE color) but a few are many-to-1
    /// (multiple OLO codes match the same TE color), so the table is keyed
    /// on (system, external_code) rather than on TE color name.
    /// </summary>
    public class ColorMatch
    {
        public int Id { get; set; }
        /// <summary>Source system identifier: "DMC", "OLO", future "Copic", etc.</summary>
        public string System { get; set; } = string.Empty;
        /// <summary>External code from the source system (e.g. DMC "225", OLO "R0.1").</summary>
        public string ExternalCode { get; set; } = string.Empty;
        /// <summary>TE color name this external code matches (e.g. "Rose Water").</summary>
        public string TeColorName { get; set; } = string.Empty;
        /// <summary>Optional hex swatch for the external color (e.g. DMC fiber #).</summary>
        public string? ExternalHex { get; set; }
        /// <summary>Optional hex swatch for the TE color (mirrors inventory if known).</summary>
        public string? TeColorHex { get; set; }
        /// <summary>Free-form notes — e.g. "Retired", "Closest match", confidence.</summary>
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Cached event scraped from the Taylored Expressions Square Online site.
    /// Refreshed on app launch in the background; the calendar overlays
    /// these on top of the user's personal events with a distinct color.
    /// EventDate is a DateOnly stored as ISO-8601 TEXT (yyyy-MM-dd).
    /// </summary>
    public class TeEventCache
    {
        public int Id { get; set; }
        /// <summary>Stable identifier from the source page (URL slug or hash).
        /// Used to upsert without duplicating across refreshes.</summary>
        public string ExternalId { get; set; } = string.Empty;
        public DateTime EventDate { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Url { get; set; }
        public string? ImageUrl { get; set; }
        public DateTime FetchedAt { get; set; }
    }
}
