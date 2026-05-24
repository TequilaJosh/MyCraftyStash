using System.Collections.Concurrent;
using JandH.Core.Models;
using JandH.Core.Services;
using JandH.Core.Services.Catalog;

namespace MyCraftyStash.Services
{
    /// <summary>
    /// Multi-provider catalog lookup. Fans search out to every enabled
    /// <see cref="ICatalogProvider"/> in parallel, merges results, and
    /// dedupes (same SKU at the same source). Each result carries a
    /// <see cref="CatalogLookupResult.Source"/> badge so the UI can show
    /// which retailer it came from.
    ///
    /// Per-provider enable/disable is persisted in settings.db kv_settings
    /// under the key <c>Catalog.Provider.{id}.Enabled</c>; first launch picks
    /// up each provider's <see cref="ICatalogProvider.DefaultEnabled"/>.
    /// </summary>
    public static class CatalogLookupService
    {
        // The full registry of known providers. Order matters only for the
        // Settings UI display order — runtime fan-out is parallel.
        private static readonly List<ICatalogProvider> AllProviders = new()
        {
            new TaylorExpressionsCatalogProvider(),
            new ShopifyCatalogProvider("altenew",       "Altenew",            "https://altenew.com"),
            new ShopifyCatalogProvider("heroarts",      "Hero Arts",          "https://heroarts.com"),
            new ShopifyCatalogProvider("c9",            "Concord & 9th",      "https://concordand9th.com"),
            new ShopifyCatalogProvider("lawnfawn",      "Lawn Fawn",          "https://www.lawnfawn.com"),
            new ShopifyCatalogProvider("rangerink",     "Ranger Inks",        "https://rangerink.com"),
            new ShopifyCatalogProvider("catherinepooler","Catherine Pooler",  "https://www.catherinepooler.com"),
            new ShopifyCatalogProvider("trinitystamps", "Trinity Stamps",     "https://trinitystamps.com"),
            new ShopifyCatalogProvider("pinkandmain",   "Pink & Main",        "https://pinkandmain.com"),
            new ShopifyCatalogProvider("pinkfresh",     "Pinkfresh Studio",   "https://www.pinkfreshstudio.com"),
            // Custom platforms — stubbed for now, placeholders so they appear
            // in the Catalog Sources settings tab and can be enabled once a
            // proper scraper lands.
            new StubCatalogProvider("scrapbook",     "Scrapbook.com",     "scrapbook.com"),
            new StubCatalogProvider("simonsaysstamp","Simon Says Stamp",  "simonsaysstamp.com"),
            new StubCatalogProvider("ginakdesigns",  "Gina K Design",     "ginakdesigns.com"),
        };

        /// <summary>True if at least one provider is enabled.</summary>
        public static bool IsAvailable => EnabledProviders().Any();

        /// <summary>Enumerates every known provider for the Settings UI.</summary>
        public static IReadOnlyList<ICatalogProvider> Providers => AllProviders;

        /// <summary>Returns true if the given provider is currently enabled.</summary>
        public static bool IsEnabled(string providerId)
        {
            var p = AllProviders.FirstOrDefault(x => x.Id == providerId);
            if (p == null) return false;
            var saved = ConfigStore.GetText($"Catalog.Provider.{providerId}.Enabled");
            if (saved == null) return p.DefaultEnabled;
            return saved.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public static void SetEnabled(string providerId, bool enabled)
            => ConfigStore.SetText($"Catalog.Provider.{providerId}.Enabled",
                enabled ? "true" : "false");

        /// <summary>Compatibility shim — the old single-provider flow used to cache
        /// a known-broken state. Multi-provider keeps no cache; this is a no-op.</summary>
        public static void ResetAvailabilityCache() { }

        /// <summary>Search every enabled provider in parallel, merge results.
        /// Per-provider failures are logged and isolated — one site going down
        /// never breaks the rest.</summary>
        public static async Task<List<CatalogLookupResult>> SearchAsync(string query, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<CatalogLookupResult>();

            var providers = EnabledProviders().ToList();
            if (providers.Count == 0) return new List<CatalogLookupResult>();

            var bag = new ConcurrentBag<CatalogLookupResult>();
            var tasks = providers.Select(async p =>
            {
                try
                {
                    var rs = await p.SearchAsync(query, ct);
                    foreach (var r in rs) bag.Add(r);
                }
                catch (Exception ex)
                {
                    LoggingService.LogError(ex, $"CatalogLookupService.SearchAsync provider={p.Id}");
                }
            });
            await Task.WhenAll(tasks);

            // Dedupe by (provider, sku-or-url). Keeping per-provider duplicates
            // separate is intentional: the same item carried by both TE and
            // Hero Arts is two distinct purchasable listings.
            return bag
                .GroupBy(r => $"{r.SourceId}|{(r.ItemNumber ?? r.Url ?? r.Name)}",
                         StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(r => r.Source)
                .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>Dispatches to the source provider's enrichment pass for the
        /// full image gallery + SKU.</summary>
        public static async Task EnrichResultAsync(CatalogLookupResult? result, CancellationToken ct = default)
        {
            if (result == null) return;
            var provider = AllProviders.FirstOrDefault(p => p.Id == result.SourceId);
            // Fall back to the TE provider for legacy results that pre-date
            // the SourceId field (cached search runs from before the refactor).
            provider ??= AllProviders.OfType<TaylorExpressionsCatalogProvider>().FirstOrDefault();
            if (provider == null) return;
            try
            {
                await provider.EnrichResultAsync(result, ct);
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, $"CatalogLookupService.EnrichResultAsync provider={provider.Id} url={result.Url}");
            }
        }

        private static IEnumerable<ICatalogProvider> EnabledProviders()
            => AllProviders.Where(p => IsEnabled(p.Id));
    }
}
