using System;
using System.Collections.Generic;
using System.Linq;

namespace MyCraftyStash.Services
{
    /// <summary>
    /// Maps a free-form wishlist item type string (e.g. from a TE import or a
    /// user-typed value like "Die") to the closest entry in the canonical
    /// inventory type list (e.g. "Dies").
    ///
    /// Matching order:
    ///   1. Exact case-insensitive match.
    ///   2. Common singular/plural variants (Die ↔ Dies, Stencil ↔ Stencils,
    ///      Story ↔ Stories, Box ↔ Boxes, etc.).
    ///   3. Single unambiguous substring match in either direction.
    /// If none of those produce a confident match, returns null so the caller
    /// can prompt the user.
    /// </summary>
    public static class WishlistTypeMatcher
    {
        public static string? FindBestMatch(string? wishlistType, IReadOnlyList<string> availableTypes)
        {
            if (string.IsNullOrWhiteSpace(wishlistType) || availableTypes == null || availableTypes.Count == 0)
                return null;

            var input = wishlistType.Trim();

            // 1. Exact (case-insensitive)
            var exact = availableTypes.FirstOrDefault(
                t => string.Equals(t, input, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;

            // 2. Singular/plural variants
            foreach (var candidate in GetPluralSingularVariants(input))
            {
                var match = availableTypes.FirstOrDefault(
                    t => string.Equals(t, candidate, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }

            // 3. Substring match — accept only if unambiguous
            var contains = availableTypes
                .Where(t => t.Contains(input, StringComparison.OrdinalIgnoreCase)
                         || input.Contains(t, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (contains.Count == 1) return contains[0];

            return null;
        }

        /// <summary>
        /// Yields common English singular/plural transformations of the input
        /// in priority order. Caller stops at the first one that matches.
        /// </summary>
        private static IEnumerable<string> GetPluralSingularVariants(string input)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { input };

            string[] variants;

            if (input.EndsWith("ies", StringComparison.OrdinalIgnoreCase) && input.Length > 3)
            {
                // stories → story
                variants = new[] { input[..^3] + "y" };
            }
            else if (input.EndsWith("es", StringComparison.OrdinalIgnoreCase) && input.Length > 2)
            {
                // boxes → box, classes → class
                variants = new[] { input[..^2], input[..^1] };
            }
            else if (input.EndsWith("s", StringComparison.OrdinalIgnoreCase) && input.Length > 1)
            {
                // dies → die, stamps → stamp
                variants = new[] { input[..^1] };
            }
            else if (input.EndsWith("y", StringComparison.OrdinalIgnoreCase) && input.Length > 1
                     && !"aeiou".Contains(char.ToLowerInvariant(input[^2])))
            {
                // story → stories (consonant + y → -ies)
                variants = new[] { input[..^1] + "ies", input + "s" };
            }
            else if (input.EndsWith("sh", StringComparison.OrdinalIgnoreCase)
                  || input.EndsWith("ch", StringComparison.OrdinalIgnoreCase)
                  || input.EndsWith("x", StringComparison.OrdinalIgnoreCase)
                  || input.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            {
                // box → boxes, dish → dishes
                variants = new[] { input + "es", input + "s" };
            }
            else
            {
                // die → dies, stamp → stamps
                variants = new[] { input + "s", input + "es" };
            }

            foreach (var v in variants)
                if (seen.Add(v))
                    yield return v;
        }
    }
}
