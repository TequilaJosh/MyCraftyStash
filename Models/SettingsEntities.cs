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
}
