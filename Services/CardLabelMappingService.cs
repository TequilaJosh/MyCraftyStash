using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyCraftyStash.Services
{
    /// <summary>
    /// One configurable mapping: a wizard "label" (e.g. "Foil Cardstock") and the
    /// item Type / Subtypes / NameContains it should resolve to. All matching is
    /// case-insensitive "contains".
    /// </summary>
    public class CardLabel
    {
        [JsonPropertyName("type")]         public string?       Type         { get; set; }
        [JsonPropertyName("subtypes")]     public List<string>? Subtypes     { get; set; }
        [JsonPropertyName("nameContains")] public string?       NameContains { get; set; }

        public CardLabel Clone() => new()
        {
            Type         = Type,
            Subtypes     = Subtypes == null ? null : new List<string>(Subtypes),
            NameContains = NameContains,
        };
    }

    public class CardLabelConfig
    {
        [JsonPropertyName("labels")]
        public Dictionary<string, CardLabel> Labels { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Loads/saves the wizard label → (type, subtypes) mapping from the network
    /// share. Provides defaults and a single resolution method that the wizard
    /// uses to populate every dropdown.
    /// </summary>
    public class CardLabelMappingService
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private CardLabelConfig _cache = new();
        private bool _loaded;
        private readonly object _lock = new();

        public static CardLabelMappingService Default { get; } = new();

        /// <summary>
        /// Built-in canonical label set. Each label maps to the type/subtype
        /// combination the wizard previously hardcoded.
        /// </summary>
        public static IReadOnlyList<string> CanonicalLabelOrder { get; } = new[]
        {
            "Cardstock",
            "Foil Cardstock",
            "Glitter Cardstock",
            "Insider Cardstock",
            "Foil-It Cardstock",
            "Frames Die",
            "Stamps",
            "Dies",
            "Stencils",
            "Embellishments",
            "Stacklets",
            "Embossing Folders",
            "OLO Markers",
            "Watercolor",
            "Envelopes",
            "Foils",
            "Mini Cube Inks",
            "Full Pad Inks",
            "Embossing Powder",
            "Storage Bags",
            "Adhesives",
            "Glue Adhesive",
            "Foam Adhesive",
            "Tape Runner Adhesive",
            "All Planned Out",
        };

        public static Dictionary<string, CardLabel> GetDefaults()
        {
            var d = new Dictionary<string, CardLabel>(StringComparer.OrdinalIgnoreCase);
            void Add(string label, string? type = null, string[]? subs = null, string? nameContains = null) =>
                d[label] = new CardLabel { Type = type, Subtypes = subs?.ToList(), NameContains = nameContains };

            Add("Cardstock",            type: "Cardstock", subs: new[] { "8.5x11" });
            Add("Foil Cardstock",       type: "Cardstock", subs: new[] { "Foil" });
            Add("Glitter Cardstock",    type: "Cardstock", subs: new[] { "Glitter" });
            Add("Insider Cardstock",    type: "Cardstock", subs: new[] { "Insider" });
            Add("Foil-It Cardstock",    type: "Cardstock", subs: new[] { "Foil-it" });
            Add("Frames Die",           type: "Dies",      subs: new[] { "Frames" });
            Add("Stamps",               type: "Stamps");
            Add("Dies",                 type: "Dies");
            Add("Stencils",             type: "Stencils");
            Add("Embellishments",       type: "Embellishments");
            Add("Stacklets",            type: "Stacklets");
            Add("Embossing Folders",    type: "Embossing Folders");
            Add("OLO Markers",          type: "OLO Markers");
            Add("Watercolor",           type: "Watercolor");
            Add("Envelopes",            type: "Envelopes");
            Add("Foils",                type: "Foils");
            Add("Mini Cube Inks",                            subs: new[] { "Mini Cube" });
            Add("Full Pad Inks",                             subs: new[] { "Full Pad" });
            Add("Embossing Powder",                          subs: new[] { "Embossing Powder" });
            Add("Storage Bags",         type: "Storage Bags");
            Add("Adhesives",            type: "Adhesive");
            Add("Glue Adhesive",        type: "Adhesive",  subs: new[] { "Glue" });
            Add("Foam Adhesive",        type: "Adhesive",  subs: new[] { "Foam" });
            Add("Tape Runner Adhesive", type: "Adhesive",  subs: new[] { "Tape Runner" });
            Add("All Planned Out",                           nameContains: "all planned out");
            return d;
        }

        public CardLabelConfig GetConfig()
        {
            EnsureLoaded();
            lock (_lock) return _cache;
        }

        public CardLabel GetMapping(string label)
        {
            EnsureLoaded();
            lock (_lock)
            {
                if (_cache.Labels.TryGetValue(label, out var m)) return m;
                if (GetDefaults().TryGetValue(label, out var def)) return def;
                return new CardLabel();
            }
        }

        public void SetMapping(string label, CardLabel mapping)
        {
            EnsureLoaded();
            lock (_lock) { _cache.Labels[label] = mapping; }
            Save();
        }

        public void ResetToDefaults()
        {
            lock (_lock)
            {
                _cache = new CardLabelConfig
                {
                    Labels = new Dictionary<string, CardLabel>(GetDefaults(), StringComparer.OrdinalIgnoreCase),
                };
                _loaded = true;
            }
            Save();
        }

        private void EnsureLoaded()
        {
            lock (_lock)
            {
                if (_loaded) return;
                Load();
                _loaded = true;
            }
        }

        private void Load()
        {
            try
            {
                var json = ConfigStore.GetText(ConfigStore.CardLabels);
                if (string.IsNullOrWhiteSpace(json) || json == "{}")
                {
                    _cache = new CardLabelConfig
                    {
                        Labels = new Dictionary<string, CardLabel>(GetDefaults(), StringComparer.OrdinalIgnoreCase),
                    };
                    return;
                }
                var loaded = JsonSerializer.Deserialize<CardLabelConfig>(json, _jsonOptions)
                             ?? new CardLabelConfig();
                _cache = new CardLabelConfig
                {
                    Labels = new Dictionary<string, CardLabel>(loaded.Labels, StringComparer.OrdinalIgnoreCase),
                };
                foreach (var (k, v) in GetDefaults())
                    _cache.Labels.TryAdd(k, v);
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "CardLabelMappingService.Load");
                _cache = new CardLabelConfig
                {
                    Labels = new Dictionary<string, CardLabel>(GetDefaults(), StringComparer.OrdinalIgnoreCase),
                };
            }
        }

        public void Save()
        {
            try
            {
                CardLabelConfig snapshot;
                lock (_lock) snapshot = _cache;

                var json = JsonSerializer.Serialize(snapshot, _jsonOptions);
                ConfigStore.SetText(ConfigStore.CardLabels, json);
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, "CardLabelMappingService.Save");
            }
        }

        /// <summary>
        /// Resolves a label to a list of wizard items by translating the mapping
        /// into a GetWizardItemsAsync call. Subtypes form an OR — an item matches
        /// if any of its (case-insensitive) subtype substrings is on the list.
        /// </summary>
        public async Task<List<WizardItemOption>> GetItemsForLabelAsync(string label, InventoryService inventory)
        {
            var mapping = GetMapping(label);

            List<WizardItemOption> rawResults;

            // No subtypes (or only one) — single query is enough.
            if (mapping.Subtypes == null || mapping.Subtypes.Count <= 1)
            {
                var single = mapping.Subtypes != null && mapping.Subtypes.Count == 1 ? mapping.Subtypes[0] : null;
                rawResults = await inventory.GetWizardItemsAsync(
                    nameContains:    mapping.NameContains,
                    type:            mapping.Type,
                    subtype:         single,
                    typeContains:    !string.IsNullOrEmpty(mapping.Type),
                    subtypeContains: true);
            }
            else
            {
                // Multiple subtypes → OR: union the per-subtype results, dedup by id.
                var bag = new Dictionary<int, WizardItemOption>();
                foreach (var s in mapping.Subtypes)
                {
                    var list = await inventory.GetWizardItemsAsync(
                        nameContains:    mapping.NameContains,
                        type:            mapping.Type,
                        subtype:         s,
                        typeContains:    !string.IsNullOrEmpty(mapping.Type),
                        subtypeContains: true);
                    foreach (var item in list) bag[item.Id] = item;
                }
                rawResults = bag.Values.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();
            }

            // Refine subtype matches: the SQL filter uses substring (Contains) so
            // a request for "Foil" also pulls in items whose subtype is "Foil-it".
            // Subtypes in this app are stored as comma-separated tag lists, so
            // re-check each result and require at least one token to EQUAL one of
            // the requested subtypes (case-insensitive).
            if (mapping.Subtypes != null && mapping.Subtypes.Count > 0)
            {
                var requested = mapping.Subtypes
                    .Select(s => s.Trim())
                    .Where(s => s.Length > 0)
                    .ToList();
                rawResults = rawResults.Where(item =>
                {
                    if (string.IsNullOrEmpty(item.Subtype)) return false;
                    var tokens = item.Subtype.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    return tokens.Any(t => requested.Any(r =>
                        string.Equals(t.Trim(), r, StringComparison.OrdinalIgnoreCase)));
                }).ToList();
            }
            return rawResults;
        }
    }

}
