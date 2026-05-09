namespace MyCraftyStash.Models
{
    /// <summary>
    /// A lightweight result from the Azure catalog lookup.
    /// Not an EF entity - populated directly from SQL queries.
    /// </summary>
    public class CatalogLookupResult
    {
        public int      Id         { get; set; }
        public string   Name       { get; set; } = string.Empty;
        public string   Type       { get; set; } = string.Empty;
        public string?  ItemNumber { get; set; }
        public string?  Theme      { get; set; }
        public string?  Subtype    { get; set; }
        public decimal? Price      { get; set; }
        public string?  ImageUrl   { get; set; }

        /// <summary>Public product page URL (https://www.tayloredexpressions.com/products/...).
        /// Populated when results come from the live web scraper; used to populate the
        /// new item's SiteUrl for one-click repurchasing.</summary>
        public string?  Url        { get; set; }

        /// <summary>Internal Shopify handle (last URL segment), used to fetch the
        /// product detail JSON for SKU + extra images. Not shown in the UI.</summary>
        public string?  Handle     { get; set; }

        /// <summary>Display name of the catalog provider that produced this result
        /// (e.g. "Taylored Expressions", "Simon Says Stamp"). Surfaces as a small
        /// badge on result cards so the user can tell sources apart.</summary>
        public string?  Source     { get; set; }

        /// <summary>Stable provider ID (e.g. "te", "sss") used internally for
        /// dedupe and per-provider enrichment dispatch.</summary>
        public string?  SourceId   { get; set; }

        public List<string> ExtraImages { get; set; } = new();

        /// <summary>True when an item with this ItemNumber already exists in the local
        /// inventory. Set by ItemLookupDialog after each search so the UI can flag
        /// duplicates before the user copies the result into the new-item form.</summary>
        public bool IsAlreadyInInventory { get; set; }

        /// <summary>Id of the matching local Item, if IsAlreadyInInventory is true.</summary>
        public int? ExistingItemId { get; set; }

        /// <summary>Name of the matching local Item, if IsAlreadyInInventory is true.</summary>
        public string? ExistingItemName { get; set; }

        public List<string> AllImages
        {
            get
            {
                var list = new List<string>();
                if (!string.IsNullOrEmpty(ImageUrl)) list.Add(ImageUrl);
                list.AddRange(ExtraImages);
                return list;
            }
        }
    }
}
