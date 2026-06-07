using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using MyCraftyStash.Services;
using MyCraftyStash.ViewModels;
namespace MyCraftyStash.Models
{
    /// <summary>
    /// Round-trip-able snapshot of CardBuildWizardViewModel's state. Captures
    /// enough state to rehydrate the wizard when re-editing an existing project's
    /// card build, instead of having to reverse-engineer it from the flat
    /// WizardBuildStep label list.
    /// </summary>
    /// <remarks>
    /// This is a tracked-shape DTO: every wizard collection / picker selection
    /// the user can configure has a slot here. Displays-only computed properties
    /// (DisplaySummary, EffectiveCardstockColor, etc.) on the wizard's existing
    /// types serialize through too — System.Text.Json simply doesn't write them
    /// back on load because they're get-only, so they regenerate from the
    /// underlying state.
    ///
    /// Versioning: bump <see cref="Version"/> if the wizard state surface changes
    /// in a backwards-incompatible way.
    /// </remarks>
    public class WizardBuildSnapshot
    {
        public string Version { get; set; } = "1";

        // ── Top-level wizard state ────────────────────────────────────────────
        public string SelectedCardBase { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;

        // ── Card Base ─────────────────────────────────────────────────────────
        public int? BaseRegularCardstockItemId { get; set; }
        public int? BaseFoilCardstockItemId { get; set; }
        public int? BaseGlitterCardstockItemId { get; set; }
        public string? BaseCardstockColor { get; set; }
        public bool BaseIsSelfBlended { get; set; }
        public string BaseSelfBlendDescription { get; set; } = string.Empty;
        public List<string> BaseBlendInkColors { get; set; } = new();
        public WizardFocalSection? CardBase { get; set; }
        public List<WizardDetailEntry> CardBaseAddedDetails { get; set; } = new();
        public List<WizardItemOption> CardBaseAddedAdhesives { get; set; } = new();

        // ── Outside Sections ─────────────────────────────────────────────────
        public List<WizardBgMatGroup> BgMats { get; set; } = new();
        public List<WizardBgMatGroup> AdditionalMats { get; set; } = new();
        public List<WizardBgMatGroup> FocalMatGroups { get; set; } = new();
        public List<WizardFocalSection> FocalParts { get; set; } = new();
        public List<WizardConfiguredSentiment> ConfiguredSentiments { get; set; } = new();
        public List<WizardEmbellishment> AddedEmbellishments { get; set; } = new();
        public WizardItemOption? SelectedEnvelopeItem { get; set; }
        public WizardItemOption? SelectedStorageBagItem { get; set; }

        // ── Inside Sections ──────────────────────────────────────────────────
        public List<WizardBgMat> InsideBgMats { get; set; } = new();
        public List<WizardBgMat> InsideAdditionalMats { get; set; } = new();
        public WizardFocalSection? InsideFocal { get; set; }
        public List<WizardConfiguredSentiment> ConfiguredInsideSentiments { get; set; } = new();
        public List<WizardEmbellishment> InsideAddedEmbellishments { get; set; } = new();

        // ── Inside hub: liner cardstock (the inside layer of the card) ───────
        public int? InsideLinerCardstockItemId { get; set; }
        public string? InsideLinerCardstockColor { get; set; }

        // ── Inside hub: top-level Details picks (stamps, dies, etc used on
        //    the inside but not tied to any specific Mat group). Shares the
        //    WizardDetailEntry shape so the same chip-strip UI works. ────────
        public List<WizardDetailEntry> InsideMiscDetails { get; set; } = new();

        public static JsonSerializerOptions JsonOptions { get; } = new()
        {
            WriteIndented = false,
            IgnoreReadOnlyProperties = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
            // Critical: many wizard types expose their collections as get-only
            // (e.g. ObservableCollection<T> Pieces { get; } = new()). Without
            // Populate the deserializer silently drops the JSON for those
            // collections — wizard round-trips with empty inner collections.
            // Populate is mutually exclusive with ReferenceHandler.IgnoreCycles,
            // but the wizard types don't actually contain cycles (no parent-VM
            // back-refs), so we don't need cycle handling here.
            PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
        };

        public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

        public static WizardBuildSnapshot? FromJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                return JsonSerializer.Deserialize<WizardBuildSnapshot>(json, JsonOptions);
            }
            catch (JsonException ex)
            {
                LoggingService.LogError(ex, "WizardBuildSnapshot.FromJson");
                return null;
            }
        }
    }
}
