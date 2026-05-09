using MyCraftyStash.Models;

namespace MyCraftyStash.Services.Catalog
{
    /// <summary>
    /// One craft-supply retailer Claude can search. Implementations are usually
    /// thin wrappers over a shared engine adapter (Shopify / BigCommerce /
    /// WooCommerce) that knows the platform-level URL patterns and HTML shape.
    /// </summary>
    public interface ICatalogProvider
    {
        /// <summary>Stable id (e.g. "te", "sss", "lawnfawn"). Used for dedupe + settings.</summary>
        string Id { get; }

        /// <summary>Human-readable display name shown as a result badge.</summary>
        string DisplayName { get; }

        /// <summary>Bare domain — used by the UI for an external-link icon and tooltip.</summary>
        string Domain { get; }

        /// <summary>Off by default? Lets the registry omit half-implemented providers
        /// from the user's enabled set on first launch.</summary>
        bool DefaultEnabled { get; }

        Task<List<CatalogLookupResult>> SearchAsync(string query, CancellationToken ct = default);

        /// <summary>Fetches the detail page and pulls down the full image gallery
        /// as base64 (so the existing inventory flow doesn't have to change).
        /// May be a no-op for providers that surface enough data in SearchAsync.</summary>
        Task EnrichResultAsync(CatalogLookupResult result, CancellationToken ct = default);
    }
}
