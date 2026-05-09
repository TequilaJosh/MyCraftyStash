namespace MyCraftyStash.Services
{
    /// <summary>
    /// Curated palette of dot colors for wishlists. Hex strings without alpha.
    /// </summary>
    public static class WishlistColorPalette
    {
        public static readonly IReadOnlyList<string> Colors = new[]
        {
            "#A82C32", // crimson (default brand)
            "#C77A2A", // amber
            "#3F8F62", // green
            "#3A6F8F", // blue
            "#7B4FA8", // purple
            "#3FA89A", // teal
            "#C75A8A", // pink
            "#7A7264", // slate
        };

        public const string Default = "#A82C32";

        public static string Normalize(string? color) =>
            string.IsNullOrWhiteSpace(color) ? Default : color!;
    }
}
