using MyCraftyStash.Models;

namespace MyCraftyStash.Services.Catalog
{
    /// <summary>
    /// Placeholder for retailers we know about but haven't written a scraper
    /// for yet (custom platforms, anti-bot protection, etc.). Returns no
    /// results but stays in the registry so the user sees the source listed
    /// and disabled in the Settings → Catalog Sources tab.
    ///
    /// Each stub also opens the search page in the user's browser when
    /// invoked from the Item Lookup dialog's "Open in browser" path —
    /// graceful degradation rather than silent emptiness.
    /// </summary>
    public class StubCatalogProvider : ICatalogProvider
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string Domain { get; }
        public bool DefaultEnabled => false;

        public StubCatalogProvider(string id, string displayName, string domain)
        {
            Id = id;
            DisplayName = displayName;
            Domain = domain;
        }

        public Task<List<CatalogLookupResult>> SearchAsync(string query, CancellationToken ct = default)
        {
            LoggingService.LogInfo($"StubCatalogProvider.SearchAsync({Id}) — provider not yet implemented");
            return Task.FromResult(new List<CatalogLookupResult>());
        }

        public Task EnrichResultAsync(CatalogLookupResult result, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
