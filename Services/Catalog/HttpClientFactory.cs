using System.Net.Http;

namespace MyCraftyStash.Services.Catalog
{
    /// <summary>
    /// One shared HttpClient per process for all catalog providers. Handlers
    /// are expensive to spin up and HttpClient is thread-safe; the platform
    /// guidance is to share a single instance.
    /// </summary>
    internal static class CatalogHttpClient
    {
        public static readonly HttpClient Instance;

        static CatalogHttpClient()
        {
            Instance = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true })
            {
                Timeout = TimeSpan.FromSeconds(20),
            };
            Instance.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            Instance.DefaultRequestHeaders.Accept.ParseAdd(
                "text/html,application/xhtml+xml,application/xml;q=0.9,application/json,*/*;q=0.8");
        }
    }
}
