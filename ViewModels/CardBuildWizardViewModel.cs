using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using MyCraftyStash.Models;
using MyCraftyStash.Services;
namespace MyCraftyStash.ViewModels
{
    // ── Wizard helper types ────────────────────────────────────────────────────

    public class WizardStencilLayer
    {
        public int LayerNumber { get; set; }
        public List<string> InkColors { get; set; } = new();
        // Per-layer special-media flags. Populated by the new stencil layer
        // stepper in WizardDetailEntry so the summary + used-item logs can
        // attribute Glitter / Happy Medium / Astro Paste to the exact layer
        // they were applied on.
        public bool UsedGlitter { get; set; }
        public bool UsedHappyMedium { get; set; }
        public bool UsedAstroPaste { get; set; }
        // The specific glitter / Happy Medium / Astro Paste inventory items the
        // user picked for this layer. Multi-select per layer; flow into the
        // used-items log via WizardDetailEntry.GetItemIds().
        public List<WizardItemOption> GlitterItems { get; set; } = new();
        public List<WizardItemOption> HappyMediumItems { get; set; } = new();
        public List<WizardItemOption> AstroPasteItems { get; set; } = new();
        public string DisplaySummary
        {
            get
            {
                var bits = new List<string>();
                if (InkColors.Count > 0) bits.Add(string.Join(", ", InkColors));
                if (UsedGlitter)
                {
                    var label = "Glitter";
                    if (GlitterItems.Count > 0) label += $" ({string.Join(", ", GlitterItems.Select(i => i.Name))})";
                    bits.Add(label);
                }
                if (UsedHappyMedium)
                {
                    var label = "Happy Medium";
                    if (HappyMediumItems.Count > 0) label += $" ({string.Join(", ", HappyMediumItems.Select(i => i.Name))})";
                    bits.Add(label);
                }
                if (UsedAstroPaste)
                {
                    var label = "Astro Paste";
                    if (AstroPasteItems.Count > 0) label += $" ({string.Join(", ", AstroPasteItems.Select(i => i.Name))})";
                    bits.Add(label);
                }
                return bits.Count == 0
                    ? $"Layer {LayerNumber}: (no entries)"
                    : $"Layer {LayerNumber}: {string.Join(" + ", bits)}";
            }
        }
    }

    public class WizardMatDecoration
    {
        public WizardItemOption Item { get; set; } = null!;
        public WizardItemOption? StampItem { get; set; }
        public List<string> StampInkColors { get; } = new();
        public List<string> EmbossingInkColors { get; } = new();
        public List<WizardStencilLayer> StencilInkLayers { get; } = new();

        public string DisplaySummary
        {
            get
            {
                var s = Item.Name;
                if (StampInkColors.Count > 0) s += $" [{string.Join(", ", StampInkColors)}]";
                if (StampItem != null) s += $" (stamp: {StampItem.Name})";
                if (EmbossingInkColors.Count > 0) s += $" [{string.Join(", ", EmbossingInkColors)}]";
                if (StencilInkLayers.Count > 0)
                    s += $" [{string.Join(" / ", StencilInkLayers.Select(l => string.Join("+", l.InkColors)))}]";
                return s;
            }
        }
    }

    public partial class WizardBgMat : ObservableObject
    {
        public static List<string> CuttingMethodOptions { get; } = new()
            { "Stacklets", "All Planned Out", "Frames", "Insider", "Foil-It", "Custom", "None" };

        public int Layer { get; set; }

        // Default to empty so the new How-Was-Cut sub-page's visibility-gated follow-ups
        // (Cut details panel) stay hidden until the user actually picks a method. The
        // legacy form, which had this defaulting to "Stacklets", is no longer the
        // primary path and renders fine with an empty initial value.
        [ObservableProperty] private string _cuttingMethod = string.Empty;

        // All Planned Out
        [ObservableProperty] private WizardItemOption? _plannedOutItem;

        // Frames (uses Dies items filtered to subtype "Frames")
        [ObservableProperty] private WizardItemOption? _framesItem;
        [ObservableProperty] private string _framesDieNumber = string.Empty;
        public bool HasFramesItem => FramesItem != null;

        // Stacklet
        [ObservableProperty] private WizardItemOption? _stackletItem;
        [ObservableProperty] private string _stackletDieNumber = string.Empty;
        public bool HasStackletItem => StackletItem != null;

        // ── How-Was-Cut follow-ups (new hub) ──────────────────────────────────
        // Generic per-piece die index (1 = largest in set) and layer count for whichever
        // method is active. Stacklets and Frames use both; All Planned Out uses only
        // CutLayers; Custom / Insider / Foil-It / None don't use either.
        [ObservableProperty] private int _cutDieIndex = 1;
        [ObservableProperty] private int _cutLayers   = 1;
        partial void OnCutDieIndexChanged(int v) => OnPropertyChanged(nameof(DisplaySummary));
        partial void OnCutLayersChanged(int v)   => OnPropertyChanged(nameof(DisplaySummary));

        // Foil item used (only when CuttingMethod is Foil-It or Insider). Drawn from
        // type="Foils" inventory and stored separately from the primary cut item so the
        // user can pick e.g. "Insider X" + foil sheet "Y" together.
        [ObservableProperty] private WizardItemOption? _foilsItem;
        partial void OnFoilsItemChanged(WizardItemOption? v) => OnPropertyChanged(nameof(DisplaySummary));

        // Note: a "secondary cut item" is no longer stored separately — the user
        // picks the cut tool from the top-level Stacklets / Frames / All Planned Out
        // dropdowns alongside an Insider or Foil-It variant (mutual exclusion split
        // into "cut tools" and "cardstock variants" groups).

        // Insider
        [ObservableProperty] private WizardItemOption? _insiderItem;
        [ObservableProperty] private string? _insiderSentiment;

        // Foil-It
        [ObservableProperty] private WizardItemOption? _foilItItem;

        // Mat Decoration
        [ObservableProperty] private bool _hasDecoration;
        [ObservableProperty] private WizardItemOption? _decorationItem;
        [ObservableProperty] private WizardItemOption? _decorationStampItem;
        public List<WizardStencilLayer> StencilInkLayers { get; } = new();
        public List<string> StampInkColors { get; } = new();
        public List<string> EmbossingInkColors { get; } = new();
        public List<string> Adhesives { get; } = new();
        public ObservableCollection<WizardMatDecoration> Decorations { get; } = new();
        // Captured from the new Details sub-page (one entry per Save & Add Another).
        // Coexists with the legacy Decorations field — both are read by summary/build-step code.
        // Alternative considered: translate WizardDetailEntry → WizardMatDecoration on save
        // so there's only one storage shape; rejected because WizardDetailEntry carries
        // richer per-picker follow-up answers we'd lose in translation.
        public ObservableCollection<WizardDetailEntry> AddedDetails { get; } = new();

        // Cardstock color for this mat
        [ObservableProperty] private string? _selectedCardstockColor;
        [ObservableProperty] private string _otherCardstockText = string.Empty;

        // Self-blended cardstock
        [ObservableProperty] private bool _isSelfBlended;
        [ObservableProperty] private string _selfBlendDescription = string.Empty;
        public List<string> BlendInkColors { get; } = new();

        public string EffectiveCardstockColor =>
            SelectedCardstockColor == "Other" ? OtherCardstockText : SelectedCardstockColor ?? string.Empty;

        public bool ShowDecorationStampSection =>
            DecorationItem?.Subtype?.Contains("Embossing Powder", StringComparison.OrdinalIgnoreCase) ?? false;

        partial void OnIsSelfBlendedChanged(bool value) => OnPropertyChanged(nameof(DisplaySummary));
        partial void OnSelfBlendDescriptionChanged(string value) => OnPropertyChanged(nameof(DisplaySummary));

        partial void OnStackletItemChanged(WizardItemOption? value)
        {
            OnPropertyChanged(nameof(HasStackletItem));
            OnPropertyChanged(nameof(DisplaySummary));
        }

        partial void OnFramesItemChanged(WizardItemOption? value)
        {
            OnPropertyChanged(nameof(HasFramesItem));
            OnPropertyChanged(nameof(DisplaySummary));
        }
        partial void OnFramesDieNumberChanged(string value) => OnPropertyChanged(nameof(DisplaySummary));

        partial void OnDecorationItemChanged(WizardItemOption? value)
        {
            DecorationStampItem = null;
            OnPropertyChanged(nameof(ShowDecorationStampSection));
            OnPropertyChanged(nameof(DisplaySummary));
        }

        partial void OnCuttingMethodChanged(string value) => OnPropertyChanged(nameof(DisplaySummary));
        partial void OnPlannedOutItemChanged(WizardItemOption? value) => OnPropertyChanged(nameof(DisplaySummary));
        partial void OnStackletDieNumberChanged(string value) => OnPropertyChanged(nameof(DisplaySummary));
        partial void OnInsiderItemChanged(WizardItemOption? value) => OnPropertyChanged(nameof(DisplaySummary));
        partial void OnInsiderSentimentChanged(string? value) => OnPropertyChanged(nameof(DisplaySummary));
        partial void OnFoilItItemChanged(WizardItemOption? value) => OnPropertyChanged(nameof(DisplaySummary));
        partial void OnHasDecorationChanged(bool value) => OnPropertyChanged(nameof(DisplaySummary));
        partial void OnDecorationStampItemChanged(WizardItemOption? value) => OnPropertyChanged(nameof(DisplaySummary));
        partial void OnSelectedCardstockColorChanged(string? value)
        {
            OnPropertyChanged(nameof(EffectiveCardstockColor));
            OnPropertyChanged(nameof(DisplaySummary));
        }
        partial void OnOtherCardstockTextChanged(string value)
        {
            if (SelectedCardstockColor == "Other") OnPropertyChanged(nameof(DisplaySummary));
            OnPropertyChanged(nameof(EffectiveCardstockColor));
        }

        public string DisplaySummary
        {
            get
            {
                // New hub: a piece can have multiple cut-tool picks at the top
                // (Stacklets / Frames / All Planned Out are mutually exclusive among
                // each other; Insider and Foil-It are independent and may coexist
                // alongside any of the cut tools to express "Insider cardstock cut
                // with a Stacklet die"). Enumerate each picked item rather than
                // collapsing to a single CuttingMethod.
                var picks = new List<string>();
                if (PlannedOutItem != null) picks.Add($"All Planned Out: {PlannedOutItem.Name}");
                if (FramesItem != null)
                    picks.Add(string.IsNullOrEmpty(FramesDieNumber)
                        ? $"Frames: {FramesItem.Name}"
                        : $"Frames: {FramesItem.Name} (Die #{FramesDieNumber})");
                if (StackletItem != null)
                    picks.Add(string.IsNullOrEmpty(StackletDieNumber)
                        ? $"Stacklets: {StackletItem.Name}"
                        : $"Stacklets: {StackletItem.Name} (Die #{StackletDieNumber})");
                if (InsiderItem != null)
                    picks.Add(string.IsNullOrEmpty(InsiderSentiment)
                        ? $"Insider: {InsiderItem.Name}"
                        : $"Insider: {InsiderItem.Name} \"{InsiderSentiment}\"");
                if (FoilItItem != null) picks.Add($"Foil-It: {FoilItItem.Name}");
                if (FoilsItem != null)  picks.Add($"Foil sheet: {FoilsItem.Name}");
                if (CuttingMethod == "Custom" && picks.Count == 0) picks.Add("Custom");
                if (CuttingMethod == "None"   && picks.Count == 0) picks.Add("None");

                // Append "die N, X layers" to the cut-tool line if applicable.
                if (CutLayers > 1 || CutDieIndex > 1)
                {
                    var bits = new List<string>();
                    if (CutDieIndex > 1) bits.Add($"die #{CutDieIndex}");
                    if (CutLayers > 1)   bits.Add($"{CutLayers} layers");
                    if (bits.Count > 0 && picks.Count > 0)
                        picks[0] += $" ({string.Join(", ", bits)})";
                }

                string cutting = picks.Count == 0 ? "(no method selected)" : string.Join(" + ", picks);

                foreach (var d in Decorations)
                    cutting += $" + {d.DisplaySummary}";
                if (Adhesives.Count > 0)
                    cutting += $" | Attached: {string.Join(", ", Adhesives)}";

                string main;
                if (!string.IsNullOrEmpty(EffectiveCardstockColor))
                {
                    var cs = EffectiveCardstockColor;
                    if (IsSelfBlended)
                    {
                        cs += " (custom color";
                        if (!string.IsNullOrEmpty(SelfBlendDescription)) cs += $": {SelfBlendDescription}";
                        if (BlendInkColors.Count > 0) cs += $"; inks: {string.Join(", ", BlendInkColors)}";
                        cs += ")";
                    }
                    main = $"{cs} | {cutting}";
                }
                else
                {
                    main = cutting;
                }
                return main;
            }
        }

        public IEnumerable<int> GetItemIds()
        {
            // The new BG-mat hub lets the user combine multiple cut-tool picks on a
            // single piece (e.g. Stacklets die + Insider variant + foil sheet). The
            // summary code (DisplaySummary above) iterates each picker independently
            // rather than gating on a single CuttingMethod — yield items the same
            // way so every picked item lands in the project's items-used list.
            if (PlannedOutItem != null) yield return PlannedOutItem.Id;
            if (FramesItem     != null) yield return FramesItem.Id;
            if (StackletItem   != null) yield return StackletItem.Id;
            if (InsiderItem    != null) yield return InsiderItem.Id;
            if (FoilItItem     != null) yield return FoilItItem.Id;
            if (FoilsItem      != null) yield return FoilsItem.Id;   // foil sheet item, missed before
            foreach (var d in Decorations)
            {
                yield return d.Item.Id;
                if (d.StampItem != null) yield return d.StampItem.Id;
            }
            // Per-piece detail entries (Stamps/Dies/Embell/Stacklets/EF/Stencils/OLO/Foils/etc.
            // each carry their own follow-up picks like glitter items, foil-stencil items,
            // etc.). Iterating them here means every picked item is rolled up into the
            // project's items-used list, in the order the user added them.
            foreach (var det in AddedDetails)
                foreach (var id in det.GetItemIds())
                    yield return id;
        }
    }

    public partial class WizardFocalSection : ObservableObject
    {
        public static List<string> CuttingMethodOptions { get; } = new()
            { "Stacklet", "All Planned Out", "Frames", "Insider", "Foil-It", "Dies", "Custom", "None" };

        public int PartNumber { get; set; }

        // Per-piece detail entries (Stamps/Dies/Embell/Stacklets/EF/Stencils +
        // their follow-ups, OLO, Foils with stencil + ink + glitter follow-ups,
        // watercolors, ink colors). Same shape as WizardBgMat.AddedDetails so
        // CollectAllItemIds + the Details panel can roll them up uniformly.
        public ObservableCollection<WizardDetailEntry> AddedDetails { get; } = new();

        [ObservableProperty] private string? _selectedCardstockColor;
        [ObservableProperty] private string _otherCardstockText = string.Empty;

        // Self-blended cardstock
        [ObservableProperty] private bool _isSelfBlended;
        [ObservableProperty] private string _selfBlendDescription = string.Empty;
        public List<string> BlendInkColors { get; } = new();

        public string EffectiveCardstockColor =>
            SelectedCardstockColor == "Other" ? OtherCardstockText : SelectedCardstockColor ?? string.Empty;

        [ObservableProperty] private string _cuttingMethod = "Stacklet";

        // All Planned Out
        [ObservableProperty] private WizardItemOption? _plannedOutItem;

        // Frames (uses Dies items filtered to subtype "Frames")
        [ObservableProperty] private WizardItemOption? _framesItem;
        [ObservableProperty] private string _framesDieNumber = string.Empty;
        public bool HasFramesItem => FramesItem != null;

        // Stacklet
        [ObservableProperty] private WizardItemOption? _stackletItem;
        [ObservableProperty] private string _stackletDieNumber = string.Empty;
        public bool HasStackletItem => StackletItem != null;

        // Insider
        [ObservableProperty] private WizardItemOption? _insiderItem;
        [ObservableProperty] private string? _insiderSentiment;

        // Foil-It
        [ObservableProperty] private WizardItemOption? _foilItItem;

        // Dies
        [ObservableProperty] private WizardItemOption? _selectedDie;

        // Decoration
        [ObservableProperty] private bool _hasDecoration;
        [ObservableProperty] private WizardItemOption? _decorationItem;
        [ObservableProperty] private WizardItemOption? _decorationStampItem;
        public List<WizardStencilLayer> StencilInkLayers { get; } = new();
        public List<string> StampInkColors { get; } = new();
        public List<string> EmbossingInkColors { get; } = new();
        public List<string> Adhesives { get; } = new();
        public ObservableCollection<WizardMatDecoration> Decorations { get; } = new();

        public bool ShowDecorationStampSection =>
            DecorationItem?.Subtype?.Contains("Embossing Powder", StringComparison.OrdinalIgnoreCase) ?? false;

        // Backer
        [ObservableProperty] private bool _hasBacker;
        [ObservableProperty] private WizardItemOption? _backerItem;
        [ObservableProperty] private string? _backerCardstockColor;
        [ObservableProperty] private string _otherBackerCardstockText = string.Empty;

        public string EffectiveBackerCardstockColor =>
            BackerCardstockColor == "Other" ? OtherBackerCardstockText : BackerCardstockColor ?? string.Empty;

        partial void OnIsSelfBlendedChanged(bool v) => OnPropertyChanged(nameof(DisplaySummary));
        partial void OnSelfBlendDescriptionChanged(string v) => OnPropertyChanged(nameof(DisplaySummary));
        partial void OnSelectedCardstockColorChanged(string? v)
        {
            OnPropertyChanged(nameof(EffectiveCardstockColor));
            OnPropertyChanged(nameof(DisplaySummary));
        }
        partial void OnOtherCardstockTextChanged(string v)
        {
            if (SelectedCardstockColor == "Other") OnPropertyChanged(nameof(DisplaySummary));
            OnPropertyChanged(nameof(EffectiveCardstockColor));
        }
        partial void OnCuttingMethodChanged(string v) => OnPropertyChanged(nameof(DisplaySummary));
        partial void OnPlannedOutItemChanged(WizardItemOption? v) => OnPropertyChanged(nameof(DisplaySummary));
        partial void OnStackletItemChanged(WizardItemOption? v) { OnPropertyChanged(nameof(HasStackletItem)); OnPropertyChanged(nameof(DisplaySummary)); }
        partial void OnStackletDieNumberChanged(string v) => OnPropertyChanged(nameof(DisplaySummary));
        partial void OnFramesItemChanged(WizardItemOption? v) { OnPropertyChanged(nameof(HasFramesItem)); OnPropertyChanged(nameof(DisplaySummary)); }
        partial void OnFramesDieNumberChanged(string v) => OnPropertyChanged(nameof(DisplaySummary));
        partial void OnInsiderItemChanged(WizardItemOption? v) => OnPropertyChanged(nameof(DisplaySummary));
        partial void OnInsiderSentimentChanged(string? v) => OnPropertyChanged(nameof(DisplaySummary));
        partial void OnFoilItItemChanged(WizardItemOption? v) => OnPropertyChanged(nameof(DisplaySummary));
        partial void OnSelectedDieChanged(WizardItemOption? v) => OnPropertyChanged(nameof(DisplaySummary));
        partial void OnHasDecorationChanged(bool v) => OnPropertyChanged(nameof(DisplaySummary));
        partial void OnDecorationItemChanged(WizardItemOption? value)
        {
            DecorationStampItem = null;
            OnPropertyChanged(nameof(ShowDecorationStampSection));
            OnPropertyChanged(nameof(DisplaySummary));
        }
        partial void OnDecorationStampItemChanged(WizardItemOption? v) => OnPropertyChanged(nameof(DisplaySummary));
        partial void OnHasBackerChanged(bool v) => OnPropertyChanged(nameof(DisplaySummary));
        partial void OnBackerItemChanged(WizardItemOption? v) => OnPropertyChanged(nameof(DisplaySummary));
        partial void OnBackerCardstockColorChanged(string? v)
        {
            OnPropertyChanged(nameof(EffectiveBackerCardstockColor));
            OnPropertyChanged(nameof(DisplaySummary));
        }
        partial void OnOtherBackerCardstockTextChanged(string v)
        {
            if (BackerCardstockColor == "Other") OnPropertyChanged(nameof(DisplaySummary));
            OnPropertyChanged(nameof(EffectiveBackerCardstockColor));
        }

        public string DisplaySummary
        {
            get
            {
                string main = CuttingMethod switch
                {
                    "All Planned Out" => PlannedOutItem?.Name ?? "(none selected)",
                    "Frames" when !string.IsNullOrEmpty(FramesDieNumber) => $"{FramesItem?.Name} (Die #{FramesDieNumber})",
                    "Frames" => FramesItem?.Name ?? "(none selected)",
                    "Stacklet" when !string.IsNullOrEmpty(StackletDieNumber) => $"{StackletItem?.Name} (Die #{StackletDieNumber})",
                    "Stacklet" => StackletItem?.Name ?? "(none selected)",
                    "Insider" when !string.IsNullOrEmpty(InsiderSentiment) => $"{InsiderItem?.Name} \"{InsiderSentiment}\"",
                    "Insider" => InsiderItem?.Name ?? "(none selected)",
                    "Foil-It" => FoilItItem?.Name ?? "(none selected)",
                    "Dies" => SelectedDie?.Name ?? "(none selected)",
                    "Custom" => "Custom",
                    _ => CuttingMethod
                };
                if (!string.IsNullOrEmpty(EffectiveCardstockColor))
                {
                    var cs = EffectiveCardstockColor;
                    if (IsSelfBlended)
                    {
                        cs += " (custom color";
                        if (!string.IsNullOrEmpty(SelfBlendDescription)) cs += $": {SelfBlendDescription}";
                        if (BlendInkColors.Count > 0) cs += $"; inks: {string.Join(", ", BlendInkColors)}";
                        cs += ")";
                    }
                    main = $"{cs} | {main}";
                }
                foreach (var d in Decorations)
                    main += $" + {d.DisplaySummary}";
                if (HasBacker && BackerItem != null)
                {
                    main += $" | Backer: {BackerItem.Name}";
                    if (!string.IsNullOrEmpty(EffectiveBackerCardstockColor)) main += $" on {EffectiveBackerCardstockColor}";
                }
                if (Adhesives.Count > 0)
                    main += $" | Attached: {string.Join(", ", Adhesives)}";
                return main;
            }
        }

        public IEnumerable<int> GetItemIds()
        {
            if (CuttingMethod == "All Planned Out" && PlannedOutItem != null) yield return PlannedOutItem.Id;
            if (CuttingMethod == "Frames" && FramesItem != null) yield return FramesItem.Id;
            if (CuttingMethod == "Stacklet" && StackletItem != null) yield return StackletItem.Id;
            if (CuttingMethod == "Insider" && InsiderItem != null) yield return InsiderItem.Id;
            if (CuttingMethod == "Foil-It" && FoilItItem != null) yield return FoilItItem.Id;
            if (CuttingMethod == "Dies" && SelectedDie != null) yield return SelectedDie.Id;
            foreach (var d in Decorations)
            {
                yield return d.Item.Id;
                if (d.StampItem != null) yield return d.StampItem.Id;
            }
            if (HasBacker && BackerItem != null) yield return BackerItem.Id;
            foreach (var det in AddedDetails)
                foreach (var id in det.GetItemIds())
                    yield return id;
        }
    }

    public partial class WizardBgMatGroup : ObservableObject
    {
        public int GroupNumber { get; set; }
        public string TypeLabel { get; set; } = "Background";
        public ObservableCollection<WizardBgMat> Pieces { get; } = new();
        // Tagged true when the user added this group while IsInsideMode was active.
        // Drives the "Inside " prefix in summary lines + lets reports distinguish
        // outside vs inside selections without a parallel collection.
        public bool IsInside { get; set; }

        public string DisplaySummary
        {
            get
            {
                if (Pieces.Count == 0) return "(empty)";
                var header = $"{TypeLabel} Mat {GroupNumber}:";
                var pieces = string.Join("\n", Pieces.Select((p, i) => $"Piece {i + 1}: {p.DisplaySummary}"));
                return $"{header}\n{pieces}";
            }
        }

        public void NotifyDisplaySummaryChanged() => OnPropertyChanged(nameof(DisplaySummary));
    }

    public partial class WizardSentimentSelection : ObservableObject
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string? Subtype { get; set; }
        public string? ItemType { get; set; }
        public string? ThumbnailBase64 { get; set; }
        public string? SentimentPreview { get; set; }
        [ObservableProperty] private bool _isSelected;
    }

    public class WizardConfiguredSentimentPart
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string? ThumbnailBase64 { get; set; }
        public bool IsStampType { get; set; }
        public string? CardstockColor { get; set; }
        public bool IsSelfBlended { get; set; }
        public string SelfBlendDescription { get; set; } = string.Empty;
        public List<string> BlendInkColors { get; set; } = new();
        public List<string> StampInkColors { get; } = new();
        public bool IsEmbossed { get; set; }
        public string? EmbossingPowderName { get; set; }
        /// <summary>Inventory ID of the embossing powder used on this sentiment part,
        /// captured at save time so the powder gets counted in the project's items-used
        /// roll-up. Was previously dropped because only the name was stored.</summary>
        public int? EmbossingPowderItemId { get; set; }
        public List<string> Adhesives { get; } = new();
        public List<WizardMatDecoration> Decorations { get; } = new();
        // Captured from the new Details sub-page when the sentiment hub routes there.
        // Coexists with legacy Decorations (kept for the old in-line sentiment form).
        public ObservableCollection<WizardDetailEntry> AddedDetails { get; } = new();

        public string DisplaySummary
        {
            get
            {
                var sb = new System.Text.StringBuilder(ItemName);
                if (!string.IsNullOrEmpty(CardstockColor))
                {
                    var cs = CardstockColor;
                    if (IsSelfBlended)
                    {
                        cs += " (self blended";
                        if (!string.IsNullOrEmpty(SelfBlendDescription)) cs += $": {SelfBlendDescription}";
                        if (BlendInkColors.Count > 0) cs += $"; inks: {string.Join(", ", BlendInkColors)}";
                        cs += ")";
                    }
                    sb.Append(IsStampType ? $" on {cs}" : $" with {cs}");
                }
                if (IsStampType)
                {
                    var withParts = new List<string>();
                    if (StampInkColors.Count > 0) withParts.Add(string.Join(", ", StampInkColors));
                    if (IsEmbossed)
                        withParts.Add(!string.IsNullOrEmpty(EmbossingPowderName)
                            ? $"embossed with {EmbossingPowderName}"
                            : "embossed");
                    if (withParts.Count > 0) sb.Append($" with {string.Join(" and ", withParts)}");
                }
                if (Decorations.Count > 0)
                    sb.Append($" + {string.Join(" + ", Decorations.Select(d => d.DisplaySummary))}");
                if (Adhesives.Count > 0) sb.Append($" | Attached: {string.Join(", ", Adhesives)}");
                return sb.ToString();
            }
        }
    }

    public class WizardConfiguredSentiment
    {
        public List<WizardConfiguredSentimentPart> Parts { get; } = new();
        // Tagged true when the user finalized this sentiment while IsInsideMode was on.
        public bool IsInside { get; set; }

        public string DisplaySummary =>
            Parts.Count == 0 ? "(empty)" :
            Parts.Count == 1 ? Parts[0].DisplaySummary :
            string.Join(" + ", Parts.Select((p, i) => $"[{i + 1}] {p.DisplaySummary}"));
    }

    /// <summary>
    /// One captured "detail entry" from the Details sub-page — a snapshot of all
    /// 8 picker selections at the moment Save was clicked. The Details page can
    /// hold any number of these per parent context.
    /// </summary>
    public class WizardDetailEntry
    {
        // Stamp + its follow-ups
        public WizardItemOption? Stamp { get; set; }
        public List<string> StampInkColors { get; set; } = new();
        public bool StampWasEmbossed { get; set; }
        public WizardItemOption? StampEmbossingPowder { get; set; }
        public bool StampUsedAsCombo { get; set; }      // only meaningful when stamp's subtype contains "Die Combo"
        public int StampComboLayers { get; set; } = 1;  // layers when stamp+die combo was used

        // Die + its follow-ups
        public WizardItemOption? Die { get; set; }
        public bool DieIsMultiLayer { get; set; }
        public int DieLayers { get; set; } = 1;

        // Embellishment + (only when Embossing Powder subtype) follow-ups
        public WizardItemOption? Embellishment { get; set; }
        public List<string> EmbellEmbossingInkColors { get; set; } = new();
        public WizardItemOption? EmbellEmbossingStamp { get; set; }

        // Stacklet + its follow-ups
        public WizardItemOption? Stacklet { get; set; }
        public int StackletDieNumber { get; set; } = 1; // 1 = largest
        public int StackletLayers { get; set; } = 1;

        // No-follow-up pickers
        public WizardItemOption? EmbossingFolder { get; set; }
        /// <summary>OLO markers used on this mat detail. Multi-select picker —
        /// stored as a list so multiple markers can be applied to a single mat.</summary>
        public List<WizardItemOption> OloMarkers { get; set; } = new();
        public WizardItemOption? Watercolor { get; set; }
        public string? InkColor { get; set; }

        // Stencil + its follow-ups: ink colors (multi-select, mirrors stamps) plus three
        // special-media toggles (Glitter / Happy Medium / Astro Paste), each with a
        // comma-separated list of stencil layer numbers it was applied to (e.g. "1,3").
        public WizardItemOption? Stencil { get; set; }
        // Per-layer captures from the stencil layer stepper. One entry per physical
        // layer of the picked stencil; each carries its own inks + special-media
        // flags. StencilInkColors below is kept as a flat aggregate for any callers
        // that haven't been migrated to per-layer reads yet.
        public List<WizardStencilLayer> StencilLayerEntries { get; set; } = new();
        public List<string> StencilInkColors { get; set; } = new();
        public bool StencilUsedGlitter { get; set; }
        public string StencilGlitterLayers { get; set; } = string.Empty;
        public bool StencilUsedHappyMedium { get; set; }
        public string StencilHappyMediumLayers { get; set; } = string.Empty;
        public bool StencilUsedAstroPaste { get; set; }
        public string StencilAstroPasteLayers { get; set; } = string.Empty;
        /// <summary>Specific embellishments (subtype Glitter) the user applied to the
        /// stencil. Multi-select — populated only when StencilUsedGlitter is true.</summary>
        public List<WizardItemOption> StencilGlitterItems { get; set; } = new();
        public List<WizardItemOption> StencilHappyMediumItems { get; set; } = new();
        public List<WizardItemOption> StencilAstroPasteItems { get; set; } = new();

        // Foil + its application-method follow-up. Method is "GlitterGrab" or "Toner".
        // GlitterGrab borrows the stencil + ink + glitter/HM/AP pattern from above.
        // Toner takes free text + a font name (with custom-font fallback).
        public WizardItemOption? Foil { get; set; }
        public string FoilApplicationMethod { get; set; } = string.Empty;
        public WizardItemOption? FoilStencil { get; set; }
        public List<string> FoilStencilInkColors { get; set; } = new();
        public bool FoilStencilUsedGlitter { get; set; }
        public bool FoilStencilUsedHappyMedium { get; set; }
        public bool FoilStencilUsedAstroPaste { get; set; }
        public string FoilStencilGlitterLayers     { get; set; } = string.Empty;
        public string FoilStencilHappyMediumLayers { get; set; } = string.Empty;
        public string FoilStencilAstroPasteLayers  { get; set; } = string.Empty;
        public List<WizardItemOption> FoilStencilGlitterItems { get; set; } = new();
        public List<WizardItemOption> FoilStencilHappyMediumItems { get; set; } = new();
        public List<WizardItemOption> FoilStencilAstroPasteItems { get; set; } = new();
        public string FoilTonerText { get; set; } = string.Empty;
        public string FoilTonerFont { get; set; } = string.Empty;

        public IEnumerable<int> GetItemIds()
        {
            if (Stamp != null) yield return Stamp.Id;
            if (StampEmbossingPowder != null) yield return StampEmbossingPowder.Id;
            if (Die != null) yield return Die.Id;
            if (Embellishment != null) yield return Embellishment.Id;
            if (EmbellEmbossingStamp != null) yield return EmbellEmbossingStamp.Id;
            if (Stacklet != null) yield return Stacklet.Id;
            if (EmbossingFolder != null) yield return EmbossingFolder.Id;
            if (Stencil != null) yield return Stencil.Id;
            foreach (var g in StencilGlitterItems) yield return g.Id;
            foreach (var h in StencilHappyMediumItems) yield return h.Id;
            foreach (var a in StencilAstroPasteItems) yield return a.Id;
            // Per-layer stencil items (new layer-stepper). Each layer's
            // Glitter / Happy Medium / Astro Paste picks log every selected
            // inventory item against this build for the used-items report.
            foreach (var layer in StencilLayerEntries)
            {
                foreach (var g in layer.GlitterItems)     yield return g.Id;
                foreach (var h in layer.HappyMediumItems) yield return h.Id;
                foreach (var a in layer.AstroPasteItems)  yield return a.Id;
            }
            foreach (var m in OloMarkers) yield return m.Id;
            if (Foil != null) yield return Foil.Id;
            if (FoilStencil != null) yield return FoilStencil.Id;
            foreach (var g in FoilStencilGlitterItems) yield return g.Id;
            foreach (var h in FoilStencilHappyMediumItems) yield return h.Id;
            foreach (var a in FoilStencilAstroPasteItems) yield return a.Id;
            if (Watercolor != null) yield return Watercolor.Id;
        }

        // Compact one-line description for chips and summary lines.
        public string DisplaySummary
        {
            get
            {
                var parts = new List<string>();
                if (Stamp != null)
                {
                    var s = $"Stamp: {Stamp.Name}";
                    if (StampInkColors.Count > 0) s += $" [{string.Join(", ", StampInkColors)}]";
                    if (StampWasEmbossed && StampEmbossingPowder != null) s += $" + emb. powder: {StampEmbossingPowder.Name}";
                    if (StampUsedAsCombo) s += $" + die ({StampComboLayers} layer{(StampComboLayers != 1 ? "s" : "")})";
                    parts.Add(s);
                }
                if (Die != null)
                {
                    var s = $"Die: {Die.Name}";
                    if (DieIsMultiLayer) s += $" ({DieLayers} layer{(DieLayers != 1 ? "s" : "")})";
                    parts.Add(s);
                }
                if (Embellishment != null)
                {
                    var s = $"Embell: {Embellishment.Name}";
                    if (EmbellEmbossingInkColors.Count > 0) s += $" [{string.Join(", ", EmbellEmbossingInkColors)}]";
                    if (EmbellEmbossingStamp != null) s += $" w/ stamp: {EmbellEmbossingStamp.Name}";
                    parts.Add(s);
                }
                if (Stacklet != null)
                {
                    parts.Add($"Stacklet: {Stacklet.Name} (die #{StackletDieNumber}, {StackletLayers} layer{(StackletLayers != 1 ? "s" : "")})");
                }
                if (EmbossingFolder != null)  parts.Add($"EF: {EmbossingFolder.Name}");
                if (Stencil != null)
                {
                    var s = $"Stencil: {Stencil.Name}";
                    // Prefer the per-layer breakdown (new stepper); fall back to
                    // the legacy flat StencilInkColors list when no per-layer data
                    // was captured (older entries).
                    var nonEmptyLayers = StencilLayerEntries
                        .Where(le => le.InkColors.Count > 0 || le.UsedGlitter || le.UsedHappyMedium || le.UsedAstroPaste)
                        .ToList();
                    if (nonEmptyLayers.Count > 0)
                    {
                        s += " [" + string.Join(" / ", nonEmptyLayers.Select(le => le.DisplaySummary)) + "]";
                    }
                    else if (StencilInkColors.Count > 0)
                    {
                        s += $" [{string.Join(", ", StencilInkColors)}]";
                    }
                    if (StencilUsedGlitter)
                    {
                        s += " + Glitter";
                        if (!string.IsNullOrWhiteSpace(StencilGlitterLayers)) s += $" (layers {StencilGlitterLayers})";
                    }
                    if (StencilUsedHappyMedium)
                    {
                        s += " + Happy Medium";
                        if (!string.IsNullOrWhiteSpace(StencilHappyMediumLayers)) s += $" (layers {StencilHappyMediumLayers})";
                    }
                    if (StencilUsedAstroPaste)
                    {
                        s += " + Astro Paste";
                        if (!string.IsNullOrWhiteSpace(StencilAstroPasteLayers)) s += $" (layers {StencilAstroPasteLayers})";
                    }
                    parts.Add(s);
                }
                if (OloMarkers.Count > 0)     parts.Add($"OLO: {string.Join(", ", OloMarkers.Select(m => m.Name))}");
                if (Watercolor != null)       parts.Add($"Watercolor: {Watercolor.Name}");
                if (!string.IsNullOrEmpty(InkColor)) parts.Add($"Ink: {InkColor}");
                return parts.Count == 0 ? "(empty)" : string.Join(" • ", parts);
            }
        }
    }

    /// <summary>
    /// Toggleable color chip used in multi-select ink color pickers
    /// (stamp inks, embossing-powder inks for embellishments).
    /// </summary>
    public partial class InkColorChip : ObservableObject
    {
        public string Color { get; init; } = string.Empty;
        // Mini Cube / Full Pad ink item id used by LazyThumbnailConverter.
        // 0 means no thumbnail available - the row falls back to a colored placeholder.
        public int ItemId { get; init; }
        [ObservableProperty] private bool _isSelected;
    }

    /// <summary>
    /// Backing VM for the reusable multi-select ink dropdown. Holds the full chip
    /// list and a click-ordered list of selected colors. Subscribe each chip's
    /// PropertyChanged so toggling automatically updates the ordered list.
    /// </summary>
    public partial class InkSelection : ObservableObject
    {
        public ObservableCollection<InkColorChip> Chips { get; } = new();
        // Watercolor chips revealed when the user toggles "Custom Color" mode in the
        // popup. Each chip carries the watercolor item's name + Id so the rows show
        // their inventory thumbnail. Picks land in the same Ordered list, prefixed
        // with "Watercolor: " so the summary keeps them distinguishable from inks.
        public ObservableCollection<InkColorChip> WatercolorChips { get; } = new();
        public ObservableCollection<string> Ordered { get; } = new();

        // Search/filter for the popup. Bound to a TextBox at the top so users with
        // 100+ inks can type instead of scrolling. Filters by Color contains.
        [ObservableProperty] private string _searchText = string.Empty;
        public IEnumerable<InkColorChip> FilteredChips =>
            string.IsNullOrWhiteSpace(SearchText)
                ? Chips
                : Chips.Where(c => c.Color != null && c.Color.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        public IEnumerable<InkColorChip> FilteredWatercolorChips =>
            string.IsNullOrWhiteSpace(SearchText)
                ? WatercolorChips
                : WatercolorChips.Where(c => c.Color != null && c.Color.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        partial void OnSearchTextChanged(string value)
        {
            OnPropertyChanged(nameof(FilteredChips));
            OnPropertyChanged(nameof(FilteredWatercolorChips));
        }

        // Comma-separated list shown on the dropdown's closed face. Empty when nothing's picked.
        public string DisplaySummary => Ordered.Count == 0 ? string.Empty : string.Join(", ", Ordered);
        public bool HasSelection => Ordered.Count > 0;
        public bool HasWatercolorOptions => WatercolorChips.Count > 0;

        [ObservableProperty] private bool _isOpen;
        // Used in the stamp context only — toggled via the "Was this embossed?" chip
        // at the top of the popup chip strip. The control auto-hides this chip when
        // the consumer doesn't enable it.
        [ObservableProperty] private bool _isEmbossed;
        // Toggled via "Custom Color" chip in the popup. While true, watercolor rows
        // are visible alongside the standard ink rows so the user can build a custom
        // blend from BOTH sources.
        [ObservableProperty] private bool _isCustomColorMode;

        public void SetColors(IEnumerable<string> colors, Func<string, int>? itemIdLookup = null)
        {
            // Detach old subscriptions to avoid leaks across reloads
            foreach (var oldChip in Chips) oldChip.PropertyChanged -= OnChipChanged;
            Chips.Clear();
            Ordered.Clear();
            foreach (var c in colors)
            {
                var chip = new InkColorChip
                {
                    Color = c,
                    ItemId = itemIdLookup?.Invoke(c) ?? 0
                };
                chip.PropertyChanged += OnChipChanged;
                Chips.Add(chip);
            }
            OnPropertyChanged(nameof(DisplaySummary));
            OnPropertyChanged(nameof(HasSelection));

            // Warm the thumbnail cache for every chip's ink item id so the popup
            // renders with images on first open instead of blank squares.
            var ids = Chips.Where(c => c.ItemId > 0).Select(c => c.ItemId).ToList();
            if (ids.Count > 0) ThumbnailCacheService.PreloadAsync(ids);
        }

        // Populate the watercolor chips revealed in Custom Color mode. Pass the same
        // WizardItemOption list the WatercolorsPicker uses; each item becomes a chip
        // whose Color is the item's Name and ItemId is the inventory Id (for thumbnail).
        public void SetWatercolors(IEnumerable<WizardItemOption> items)
        {
            foreach (var oldChip in WatercolorChips) oldChip.PropertyChanged -= OnChipChanged;
            WatercolorChips.Clear();
            foreach (var it in items)
            {
                if (it == null) continue;
                var chip = new InkColorChip { Color = it.Name, ItemId = it.Id };
                chip.PropertyChanged += OnChipChanged;
                WatercolorChips.Add(chip);
            }
            OnPropertyChanged(nameof(HasWatercolorOptions));
        }

        public void Clear()
        {
            foreach (var chip in Chips) chip.IsSelected = false;
            foreach (var chip in WatercolorChips) chip.IsSelected = false;
            Ordered.Clear();
            IsEmbossed = false;
            IsCustomColorMode = false;
            OnPropertyChanged(nameof(DisplaySummary));
            OnPropertyChanged(nameof(HasSelection));
        }

        [RelayCommand]
        private void ConfirmAndClose() => IsOpen = false;

        private void OnChipChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(InkColorChip.IsSelected) || sender is not InkColorChip chip) return;
            if (chip.IsSelected)
            {
                if (!Ordered.Contains(chip.Color)) Ordered.Add(chip.Color);
            }
            else
            {
                Ordered.Remove(chip.Color);
            }
            OnPropertyChanged(nameof(DisplaySummary));
            OnPropertyChanged(nameof(HasSelection));
        }
    }

    /// <summary>
    /// Per-layer stencil entry for the wizard's stencil layer stepper. One entry
    /// per physical stencil layer (1..StencilLayers); each owns its own InkSelection
    /// + Glitter/Happy Medium/Astro Paste flags + dedicated multi-select pickers
    /// for the specific items used on that layer. Switching layers in the stepper
    /// just rebinds the UI to a different entry, so each layer's checkbox/picker
    /// state is preserved (or empty for an unedited layer).
    /// </summary>
    public partial class WizardStencilLayerEntry : ObservableObject
    {
        public int LayerNumber { get; set; }
        public InkSelection Inks { get; } = new();
        [ObservableProperty] private bool _usedGlitter;
        [ObservableProperty] private bool _usedHappyMedium;
        [ObservableProperty] private bool _usedAstroPaste;

        // Per-layer item pickers — multi-select so the user can record more than
        // one glitter / Happy Medium / Astro Paste item per layer.
        public WizardItemPicker GlitterPicker      { get; } = new() { PlaceholderText = "Which glitter?",       IsMultiSelect = true };
        public WizardItemPicker HappyMediumPicker  { get; } = new() { PlaceholderText = "Which happy medium?", IsMultiSelect = true };
        public WizardItemPicker AstroPastePicker   { get; } = new() { PlaceholderText = "Which astro paste?",  IsMultiSelect = true };

        public WizardStencilLayerEntry()
        {
            Inks.Ordered.CollectionChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(SummaryLine));
                OnPropertyChanged(nameof(HasAnything));
            };
            GlitterPicker.SelectedItems.CollectionChanged     += (_, _) => OnPropertyChanged(nameof(SummaryLine));
            HappyMediumPicker.SelectedItems.CollectionChanged += (_, _) => OnPropertyChanged(nameof(SummaryLine));
            AstroPastePicker.SelectedItems.CollectionChanged  += (_, _) => OnPropertyChanged(nameof(SummaryLine));
        }

        partial void OnUsedGlitterChanged(bool v)
        {
            if (!v) GlitterPicker.SelectedItems.Clear();
            OnPropertyChanged(nameof(SummaryLine));
            OnPropertyChanged(nameof(HasAnything));
        }
        partial void OnUsedHappyMediumChanged(bool v)
        {
            if (!v) HappyMediumPicker.SelectedItems.Clear();
            OnPropertyChanged(nameof(SummaryLine));
            OnPropertyChanged(nameof(HasAnything));
        }
        partial void OnUsedAstroPasteChanged(bool v)
        {
            if (!v) AstroPastePicker.SelectedItems.Clear();
            OnPropertyChanged(nameof(SummaryLine));
            OnPropertyChanged(nameof(HasAnything));
        }

        public bool HasAnything =>
            Inks.HasSelection || UsedGlitter || UsedHappyMedium || UsedAstroPaste;

        public string SummaryLine
        {
            get
            {
                var bits = new List<string>();
                if (Inks.HasSelection) bits.Add(string.Join(", ", Inks.Ordered));
                if (UsedGlitter)
                {
                    var label = "Glitter";
                    if (GlitterPicker.SelectedItems.Count > 0)
                        label += $" ({string.Join(", ", GlitterPicker.SelectedItems.Select(i => i.Name))})";
                    bits.Add(label);
                }
                if (UsedHappyMedium)
                {
                    var label = "Happy Medium";
                    if (HappyMediumPicker.SelectedItems.Count > 0)
                        label += $" ({string.Join(", ", HappyMediumPicker.SelectedItems.Select(i => i.Name))})";
                    bits.Add(label);
                }
                if (UsedAstroPaste)
                {
                    var label = "Astro Paste";
                    if (AstroPastePicker.SelectedItems.Count > 0)
                        label += $" ({string.Join(", ", AstroPastePicker.SelectedItems.Select(i => i.Name))})";
                    bits.Add(label);
                }
                return bits.Count == 0
                    ? $"Layer {LayerNumber}: (nothing yet)"
                    : $"Layer {LayerNumber}: {string.Join(" + ", bits)}";
            }
        }
    }

    /// <summary>
    /// Drop-in reusable VM helper for the wizard's "pick an item" dropdown.
    /// Holds a master list, exposes chip-filtered + search-filtered FilteredItems,
    /// and tracks the selected item. Pair with WizardItemPickerControl.
    /// </summary>
    public partial class WizardItemPicker : ObservableObject
    {
        private readonly List<WizardItemOption> _all = new();

        public ObservableCollection<string> Subtypes { get; } = new();
        public ObservableCollection<WizardItemOption> FilteredItems { get; } = new();

        /// <summary>For multi-select pickers (e.g. OLO Markers in card-build details).
        /// Single-select pickers leave this empty and use SelectedItem instead.</summary>
        public ObservableCollection<WizardItemOption> SelectedItems { get; } = new();

        [ObservableProperty] private string _activeSubtype = "All";
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private WizardItemOption? _selectedItem;
        [ObservableProperty] private bool _isOpen;
        [ObservableProperty] private bool _isMultiSelect;

        // Optional one-line label shown on the closed dropdown when nothing is selected.
        public string PlaceholderText { get; init; } = "Select...";

        public bool HasSubtypes => Subtypes.Count > 1; // "All" + at least one real subtype

        /// <summary>Comma-joined names of all SelectedItems, used as the toggle-button
        /// label for multi-select pickers. Updated whenever SelectedItems changes.</summary>
        public string SelectedItemsLabel
        {
            get
            {
                if (SelectedItems.Count == 0) return string.Empty;
                if (SelectedItems.Count <= 3)
                    return string.Join(", ", SelectedItems.Select(i => i.Name));
                return $"{SelectedItems[0].Name} +{SelectedItems.Count - 1}";
            }
        }

        /// <summary>Unified text shown on the closed picker button. Single-select
        /// pickers show the chosen item's name; multi-select pickers show the
        /// joined SelectedItems label.</summary>
        public string DisplayLabel => IsMultiSelect ? SelectedItemsLabel : (SelectedItem?.Name ?? string.Empty);

        public bool HasSelection => IsMultiSelect ? SelectedItems.Count > 0 : SelectedItem != null;

        public WizardItemPicker()
        {
            SelectedItems.CollectionChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(SelectedItemsLabel));
                OnPropertyChanged(nameof(DisplayLabel));
                OnPropertyChanged(nameof(HasSelection));
            };
        }

        partial void OnSelectedItemChanged(WizardItemOption? value)
        {
            OnPropertyChanged(nameof(DisplayLabel));
            OnPropertyChanged(nameof(HasSelection));
        }

        partial void OnIsMultiSelectChanged(bool value)
        {
            OnPropertyChanged(nameof(DisplayLabel));
            OnPropertyChanged(nameof(HasSelection));
        }

        /// <summary>Toggle an item in the multi-select set. Used by the picker control
        /// when <see cref="IsMultiSelect"/> is true; clicks add or remove the item
        /// instead of replacing SelectedItem.</summary>
        public void ToggleSelected(WizardItemOption item)
        {
            var existing = SelectedItems.FirstOrDefault(i => i.Id == item.Id);
            if (existing != null) SelectedItems.Remove(existing);
            else SelectedItems.Add(item);
        }

        public bool IsItemSelected(WizardItemOption item) =>
            SelectedItems.Any(i => i.Id == item.Id);

        /// <summary>
        /// Loads items into the picker. If <paramref name="canonicalSubtypes"/> is supplied
        /// (e.g. from UserSettingsService.GetSubtypesForType), only those subtypes appear
        /// as chips — and an item matches a chip when its Subtype field CONTAINS the chip
        /// text (case-insensitive). When omitted, falls back to auto-extracted distinct
        /// subtype values with exact-match filtering.
        /// </summary>
        public void Load(IEnumerable<WizardItemOption> items, IEnumerable<string>? canonicalSubtypes = null)
        {
            _all.Clear();
            _all.AddRange(items);

            Subtypes.Clear();
            Subtypes.Add("All");
            if (canonicalSubtypes != null)
            {
                _useContainsMatch = true;
                foreach (var s in canonicalSubtypes
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
                    Subtypes.Add(s);
            }
            else
            {
                _useContainsMatch = false;
                foreach (var s in _all
                    .Select(i => i.Subtype)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
                    Subtypes.Add(s!);
            }
            OnPropertyChanged(nameof(HasSubtypes));

            ActiveSubtype = "All";
            SearchText = string.Empty;
            Refilter();

            // Warm the thumbnail cache for every item so dropdown rows render with images
            // on first open. Pass (Id, ImageUrl) tuples so the cache service can skip its
            // per-item DB query — every WizardItemOption already carries ImageUrl.
            var pairs = _all
                .Where(i => i.Id > 0)
                .Select(i => (i.Id, ImageUrl: i.ImageUrl))
                .ToList();
            if (pairs.Count > 0) ThumbnailCacheService.PreloadAsync(pairs);
        }

        private bool _useContainsMatch;

        partial void OnActiveSubtypeChanged(string value) => Refilter();
        partial void OnSearchTextChanged(string value) => Refilter();

        private void Refilter()
        {
            // Preserve the load-time ordering of _all rather than re-sorting alphabetically here.
            // The caller (e.g. cardstock pinned ordering) controls the sort by passing items
            // already in the desired order — re-sorting in Refilter would clobber that
            // (e.g. dropping "Sugar Cube" from the top because "Banana" sorts earlier).
            IEnumerable<WizardItemOption> q = _all;
            if (!string.Equals(ActiveSubtype, "All", StringComparison.OrdinalIgnoreCase))
            {
                if (_useContainsMatch)
                    q = q.Where(i => i.Subtype != null
                        && i.Subtype.Contains(ActiveSubtype, StringComparison.OrdinalIgnoreCase));
                else
                    q = q.Where(i => string.Equals(i.Subtype, ActiveSubtype, StringComparison.OrdinalIgnoreCase));
            }
            if (!string.IsNullOrWhiteSpace(SearchText))
                q = q.Where(i => i.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            FilteredItems.Clear();
            foreach (var i in q) FilteredItems.Add(i);
        }

        [RelayCommand]
        private void SelectSubtype(string? subtype)
        {
            if (!string.IsNullOrEmpty(subtype)) ActiveSubtype = subtype;
        }
    }

    /// <summary>
    /// One line in the right-side Summary panel. Carries display text and an optional
    /// remove callback — when non-null, the row renders a ✕ button that invokes it
    /// (used to delete an accidentally-added detail entry from its source collection).
    /// </summary>
    public class SummaryRow
    {
        public string Text { get; init; } = string.Empty;
        public Action? RemoveAction { get; init; }
        public bool IsRemovable => RemoveAction != null;
    }

    public class WizardEmbellishment
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string? Subtype { get; set; }
        public int? StampItemId { get; set; }
        public string? StampItemName { get; set; }
        // Ink colors used (only meaningful when Subtype contains "Embossing Powder").
        // Captured from the same multi-select ink picker the Details tab uses.
        public List<string> InkColors { get; set; } = new();
        // Tagged true when the user added this embellishment while IsInsideMode was on.
        public bool IsInside { get; set; }

        public string DisplaySummary
        {
            get
            {
                var s = ItemName;
                if (InkColors.Count > 0) s += $" [{string.Join(", ", InkColors)}]";
                if (!string.IsNullOrEmpty(StampItemName)) s += $" (stamp: {StampItemName})";
                return s;
            }
        }
    }

    // ── Main wizard ViewModel ──────────────────────────────────────────────────

    public partial class CardBuildWizardViewModel : ObservableObject
    {
        private readonly InventoryService _service;
        private readonly SentimentService _sentimentService = new();

        // ── Result (read after dialog closes) ─────────────────────────────────
        public bool WasConfirmed { get; private set; }
        public List<int> SelectedItemIds { get; private set; } = new();
        public string CardBaseType { get; private set; } = string.Empty;
        public List<WizardBuildStep> BuildSteps { get; private set; } = new();

        // (Legacy Sec*Expanded / Sec*Confirmed flags removed — the new hub layout
        //  doesn't use sequential expand/confirm semantics. ShowConfirmation removed
        //  too — the legacy "Review &amp; Confirm" intermediate card is gone; Create
        //  Card on the hub finalizes directly.)

        // ── Hub navigation (new layout) ───────────────────────────────────────
        // Values: "Hub", "CardBase", "BackgroundMat", "AdditionalMat", "FocalMat",
        //         "Sentiment", "Embellishments", "Inside", "Envelope"
        [ObservableProperty] private string _currentSection = "Hub";

        partial void OnCurrentSectionChanged(string value)
        {
            OnPropertyChanged(nameof(IsHubActive));
            OnPropertyChanged(nameof(IsCardBaseSectionActive));
            OnPropertyChanged(nameof(IsBackgroundMatSectionActive));
            OnPropertyChanged(nameof(IsAdditionalMatSectionActive));
            OnPropertyChanged(nameof(IsFocalMatSectionActive));
            OnPropertyChanged(nameof(IsSentimentSectionActive));
            OnPropertyChanged(nameof(IsEmbellishmentsSectionActive));
            OnPropertyChanged(nameof(IsInsideCardstockSectionActive));
            OnPropertyChanged(nameof(IsInsideDetailsSectionActive));
            OnPropertyChanged(nameof(IsDetailsStepActive));
            OnPropertyChanged(nameof(IsBackgroundOrAdditionalMatActive));
        }

        public bool IsHubActive => CurrentSection == "Hub";
        public bool IsCardBaseSectionActive => CurrentSection == "CardBase";
        public bool IsBackgroundMatSectionActive => CurrentSection == "BackgroundMat";
        public bool IsAdditionalMatSectionActive => CurrentSection == "AdditionalMat";
        public bool IsFocalMatSectionActive => CurrentSection == "FocalMat";
        public bool IsSentimentSectionActive => CurrentSection == "Sentiment";
        public bool IsEmbellishmentsSectionActive => CurrentSection == "Embellishments";
        // Inside-hub-only sections. Both are top-level (CurrentSection values),
        // not sub-steps, so they can be reached directly from the inside hub.
        public bool IsInsideCardstockSectionActive => CurrentSection == "InsideCardstock";
        public bool IsInsideDetailsSectionActive   => CurrentSection == "InsideDetails";

        [RelayCommand]
        private void NavToCardBase()
        {
            CurrentCardBaseStep = "Hub";   // always land on the cardbase sub-hub
            CurrentSection = "CardBase";
        }

        // ── Cardbase sub-hub navigation ───────────────────────────────────────
        // Values: "Hub", "Cardfold", "Cardstock", "Details", "Adhesives"
        [ObservableProperty] private string _currentCardBaseStep = "Hub";

        partial void OnCurrentCardBaseStepChanged(string value)
        {
            OnPropertyChanged(nameof(IsCardBaseHubStep));
            OnPropertyChanged(nameof(IsCardBaseCardfoldStep));
            OnPropertyChanged(nameof(IsCardBaseCardstockStep));
            OnPropertyChanged(nameof(IsCardBaseDetailsStep));
            OnPropertyChanged(nameof(IsCardBaseAdhesivesStep));
            OnPropertyChanged(nameof(IsDetailsStepActive));
        }

        public bool IsCardBaseHubStep        => CurrentCardBaseStep == "Hub";
        public bool IsCardBaseCardfoldStep   => CurrentCardBaseStep == "Cardfold";
        public bool IsCardBaseCardstockStep  => CurrentCardBaseStep == "Cardstock";
        public bool IsCardBaseDetailsStep    => CurrentCardBaseStep == "Details";
        public bool IsCardBaseAdhesivesStep  => CurrentCardBaseStep == "Adhesives";

        [RelayCommand] private void NavCardBaseToCardfold()  => CurrentCardBaseStep = "Cardfold";
        [RelayCommand] private void NavCardBaseToCardstock() => CurrentCardBaseStep = "Cardstock";
        [RelayCommand]
        private void NavCardBaseToDetails()
        {
            // The Details page is shared across many parent flows (cardbase, mats, sentiments).
            // Setting DetailsReturnTarget tells the page where Save & Return should navigate
            // and which saved flag to flip.
            DetailsReturnTarget = "CardBase";
            CurrentCardBaseStep = "Details";
        }
        [RelayCommand] private void NavCardBaseToAdhesives() => CurrentCardBaseStep = "Adhesives";

        // Selects a card fold (e.g. "A2 Top Fold"). The Cardfold sub-page binds each
        // button to this command with the fold name as CommandParameter; the matching
        // button highlights automatically via SelectedCardBase comparison.
        [RelayCommand]
        private void SelectCardFold(string? foldName)
        {
            if (!string.IsNullOrEmpty(foldName)) SelectedCardBase = foldName;
        }

        // Save Cardfold → refresh summary, return to the Cardbase sub-hub.
        [RelayCommand]
        private void SaveCardFold()
        {
            CardFoldSaved = !string.IsNullOrEmpty(SelectedCardBase);
            UpdateSummaryLines();
            CurrentCardBaseStep = "Hub";
        }

        // ── Cardbase / Cardstock sub-page state ───────────────────────────────
        // Per-bucket item lists (regular cardstock, foil, glitter) — populated in InitializeAsync.
        public ObservableCollection<WizardItemOption> BaseCardstockRegularItems { get; } = new();
        public ObservableCollection<WizardItemOption> BaseCardstockFoilItems    { get; } = new();
        public ObservableCollection<WizardItemOption> BaseCardstockGlitterItems { get; } = new();

        // Selected item from each bucket. Picking from any bucket clears the other two
        // and updates SelectedBaseCardstockColor (existing string used by the rest of the wizard).
        [ObservableProperty] private WizardItemOption? _selectedBaseRegularCardstockItem;
        [ObservableProperty] private WizardItemOption? _selectedBaseFoilCardstockItem;
        [ObservableProperty] private WizardItemOption? _selectedBaseGlitterCardstockItem;

        private bool _suppressCardstockEcho;
        partial void OnSelectedBaseRegularCardstockItemChanged(WizardItemOption? value)
        {
            if (value == null || _suppressCardstockEcho) return;
            _suppressCardstockEcho = true;
            SelectedBaseFoilCardstockItem = null;
            SelectedBaseGlitterCardstockItem = null;
            SelectedBaseCardstockColor = value.Name;
            _suppressCardstockEcho = false;
        }
        partial void OnSelectedBaseFoilCardstockItemChanged(WizardItemOption? value)
        {
            if (value == null || _suppressCardstockEcho) return;
            _suppressCardstockEcho = true;
            SelectedBaseRegularCardstockItem = null;
            SelectedBaseGlitterCardstockItem = null;
            SelectedBaseCardstockColor = value.Name;
            _suppressCardstockEcho = false;
        }
        partial void OnSelectedBaseGlitterCardstockItemChanged(WizardItemOption? value)
        {
            if (value == null || _suppressCardstockEcho) return;
            _suppressCardstockEcho = true;
            SelectedBaseRegularCardstockItem = null;
            SelectedBaseFoilCardstockItem = null;
            SelectedBaseCardstockColor = value.Name;
            _suppressCardstockEcho = false;
        }

        [RelayCommand]
        private void SaveCardStock()
        {
            // Saved when there's a colour pick OR the user marked it as self-blended
            // (the latter is a valid configuration with no canonical cardstock color).
            CardStockSaved = !string.IsNullOrEmpty(SelectedBaseCardstockColor) || BaseIsSelfBlended;
            UpdateSummaryLines();
            CurrentCardBaseStep = "Hub";
        }

        // ── Cardbase / Details sub-page pickers ───────────────────────────────
        // Each generic-subtype picker is its own WizardItemPicker so the chip strip,
        // search text, and selected item are independent across the 7 dropdowns.
        public WizardItemPicker StampsPicker          { get; } = new() { PlaceholderText = "Stamps" };
        public WizardItemPicker DiesPicker            { get; } = new() { PlaceholderText = "Dies" };
        public WizardItemPicker EmbellishmentsPicker  { get; } = new() { PlaceholderText = "Embellishments" };
        public WizardItemPicker StackletsPicker       { get; } = new() { PlaceholderText = "Stacklets" };
        public WizardItemPicker EmbossingFoldersPicker{ get; } = new() { PlaceholderText = "Embossing Folders" };
        public WizardItemPicker StencilsPicker        { get; } = new() { PlaceholderText = "Stencils" };
        public WizardItemPicker OloMarkersPicker      { get; } = new() { PlaceholderText = "OLO Markers", IsMultiSelect = true };
        public WizardItemPicker FoilsPicker           { get; } = new() { PlaceholderText = "Foils" };

        // ── Stencil follow-up multi-pickers ──────────────────────────────────
        // When the user ticks "Used Glitter" / "Used Happy Medium" / "Used Astro Paste"
        // on a stencil detail, these multi-select pickers let them say WHICH glitter /
        // happy medium / astro paste item they used. Pre-filtered to Embellishments
        // with the matching subtype, populated on demand in InitDetailPickersAsync.
        public WizardItemPicker StencilGlitterPicker      { get; } = new() { PlaceholderText = "Glitter", IsMultiSelect = true };
        public WizardItemPicker StencilHappyMediumPicker  { get; } = new() { PlaceholderText = "Happy Medium", IsMultiSelect = true };
        public WizardItemPicker StencilAstroPastePicker   { get; } = new() { PlaceholderText = "Astro Paste", IsMultiSelect = true };

        // ── Foils follow-up: application method ("GlitterGrab" or "Toner") and the
        // sub-questions each method asks. GlitterGrab reuses the stencil + ink +
        // glitter/HM/AP pattern. Toner takes free text + a font.
        [ObservableProperty] private string _foilApplicationMethod = string.Empty;
        public bool IsFoilGlitterGrabSelected => string.Equals(FoilApplicationMethod, "GlitterGrab", StringComparison.OrdinalIgnoreCase);
        public bool IsFoilTonerSelected       => string.Equals(FoilApplicationMethod, "Toner",       StringComparison.OrdinalIgnoreCase);
        partial void OnFoilApplicationMethodChanged(string value)
        {
            OnPropertyChanged(nameof(IsFoilGlitterGrabSelected));
            OnPropertyChanged(nameof(IsFoilTonerSelected));
        }
        public WizardItemPicker FoilStencilPicker            { get; } = new() { PlaceholderText = "Stencil" };
        public InkSelection      FoilStencilInks              { get; } = new();
        [ObservableProperty] private bool _foilStencilUsedGlitter;
        [ObservableProperty] private bool _foilStencilUsedHappyMedium;
        [ObservableProperty] private bool _foilStencilUsedAstroPaste;
        // Comma-separated layers each medium was applied to (e.g. "1,3"), mirrors
        // the regular stencil layer fields. Saved alongside the picker selections.
        [ObservableProperty] private string _foilStencilGlitterLayers     = string.Empty;
        [ObservableProperty] private string _foilStencilHappyMediumLayers = string.Empty;
        [ObservableProperty] private string _foilStencilAstroPasteLayers  = string.Empty;
        public WizardItemPicker FoilStencilGlitterPicker     { get; } = new() { PlaceholderText = "Glitter", IsMultiSelect = true };
        public WizardItemPicker FoilStencilHappyMediumPicker { get; } = new() { PlaceholderText = "Happy Medium", IsMultiSelect = true };
        public WizardItemPicker FoilStencilAstroPastePicker  { get; } = new() { PlaceholderText = "Astro Paste", IsMultiSelect = true };
        [ObservableProperty] private string _foilTonerText = string.Empty;
        [ObservableProperty] private string _foilTonerFont = string.Empty;
        [ObservableProperty] private string _foilTonerCustomFont = string.Empty;
        /// <summary>All fonts actually installed on this machine. Pulled from
        /// <c>System.Windows.Media.Fonts.SystemFontFamilies</c> at construction
        /// so user-installed fonts show up too. Sorted alphabetically.</summary>
        public List<string> StandardFonts { get; } = System.Windows.Media.Fonts.SystemFontFamilies
            .Select(ff => ff.Source)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();
        // Envelopes lives directly on the main wizard hub — clicking it opens a dropdown
        // immediately rather than navigating to a sub-page. The picker mirrors its
        // SelectedItem onto the existing SelectedEnvelopeItem so Create Card / build
        // steps / summary all continue to work.
        public WizardItemPicker EnvelopesPicker       { get; } = new() { PlaceholderText = "Envelopes" };

        // ── Follow-up pickers used inside the Selection Details panel ────────
        // Embossing powder used when the user says they embossed a stamp.
        // Pre-filtered to Embellishments with subtype containing "Embossing Powder".
        public WizardItemPicker StampEmbossingPowderPicker { get; } = new() { PlaceholderText = "Embossing Powder" };
        // Stamp picker used when the user picks an Embellishments item with "Embossing Powder" subtype
        // and we need to know which stamp got embossed with it.
        public WizardItemPicker EmbellEmbossingStampPicker { get; } = new() { PlaceholderText = "Stamp Embossed" };

        // ── Multi-select ink color pickers (stamp inks, embell embossing inks, stencil inks) ─
        // Each holds the full color list + a click-ordered selection list bound to the
        // reusable InkMultiSelectControl dropdown.
        public InkSelection StampInks            { get; } = new();
        public InkSelection EmbellEmbossingInks  { get; } = new();
        public InkSelection StencilInks          { get; } = new();
        // Inks/Watercolors picker for the Details main hub. Single-pick ink colors
        // and watercolors still flow through SelectedInkColor / WatercolorsPicker
        // (kept for backward compat), but enabling the popup's "Custom Color"
        // chip lets the user blend multiple picks here too — same behaviour as
        // the other multi-select dropdowns. When .Ordered is non-empty the
        // captured detail entry uses that joined list as its ink description.
        public InkSelection DetailsInks          { get; } = new();
        // Inks blended into a self-blended cardstock for the active BG mat piece.
        // Synced into CurrentMat.BlendInkColors on Save Cardstock.
        public InkSelection BgPieceBlendInks     { get; } = new();
        // Inks blended into a self-blended cardstock for the active sentiment piece.
        // Synced into the WizardConfiguredSentimentPart.BlendInkColors at piece-capture time.
        public InkSelection SentimentBlendInks   { get; } = new();

        // ── Follow-up answer state ────────────────────────────────────────────
        // StampWasEmbossed is exposed via StampInks.IsEmbossed (chip in the ink popup).
        [ObservableProperty] private bool _stampUsedAsCombo;       // only when stamp subtype contains "Die Combo"
        [ObservableProperty] private int _stampComboLayers = 1;
        [ObservableProperty] private bool _dieIsMultiLayer;
        [ObservableProperty] private int _dieLayers = 1;
        [ObservableProperty] private int _stackletDieNumber = 1;   // 1 = largest in the set
        [ObservableProperty] private int _stackletLayers = 1;

        // Stencil follow-up: three special media (Glitter / Happy Medium / Astro Paste)
        // each with a comma-separated list of layer numbers it was applied to (e.g. "1,3").
        [ObservableProperty] private bool _stencilUsedGlitter;
        [ObservableProperty] private string _stencilGlitterLayers = string.Empty;
        [ObservableProperty] private bool _stencilUsedHappyMedium;
        [ObservableProperty] private string _stencilHappyMediumLayers = string.Empty;
        [ObservableProperty] private bool _stencilUsedAstroPaste;
        [ObservableProperty] private string _stencilAstroPasteLayers = string.Empty;

        // ── Details-tab stencil layer stepper ─────────────────────────────────
        // Built when the user picks a stencil in the Details tab; one entry per
        // physical layer (StencilsPicker.SelectedItem.StencilLayers, fallback 1).
        // The user steps through one layer at a time picking the inks (and
        // Glitter / Happy Medium / Astro Paste flags) used on that layer. All
        // entries are captured into WizardDetailEntry.StencilLayerEntries on Save.
        // Renamed with a "DetailStencil…" prefix so the names don't collide with
        // the older decoration-level stencil-layer state further down in this VM.
        public ObservableCollection<WizardStencilLayerEntry> DetailStencilLayerEntries { get; } = new();
        [ObservableProperty] private int _detailStencilLayerIndex;
        partial void OnDetailStencilLayerIndexChanged(int value)
        {
            OnPropertyChanged(nameof(DetailStencilLayerEntry));
            OnPropertyChanged(nameof(DetailStencilLayerHeader));
            OnPropertyChanged(nameof(HasPreviousDetailStencilLayer));
            OnPropertyChanged(nameof(HasNextDetailStencilLayer));
            OnPropertyChanged(nameof(IsLastDetailStencilLayer));
        }

        public WizardStencilLayerEntry? DetailStencilLayerEntry =>
            (DetailStencilLayerIndex >= 0 && DetailStencilLayerIndex < DetailStencilLayerEntries.Count)
                ? DetailStencilLayerEntries[DetailStencilLayerIndex]
                : null;

        public string DetailStencilLayerHeader =>
            DetailStencilLayerEntries.Count == 0
                ? "Stencil layers"
                : $"Layer {DetailStencilLayerIndex + 1} of {DetailStencilLayerEntries.Count}";

        public bool HasPreviousDetailStencilLayer => DetailStencilLayerIndex > 0;
        public bool HasNextDetailStencilLayer => DetailStencilLayerIndex < DetailStencilLayerEntries.Count - 1;
        public bool IsLastDetailStencilLayer => DetailStencilLayerEntries.Count > 0
                                             && DetailStencilLayerIndex == DetailStencilLayerEntries.Count - 1;

        [RelayCommand]
        private void NextDetailStencilLayer()
        {
            if (HasNextDetailStencilLayer) DetailStencilLayerIndex++;
        }

        [RelayCommand]
        private void PreviousDetailStencilLayer()
        {
            if (HasPreviousDetailStencilLayer) DetailStencilLayerIndex--;
        }

        // Rebuilds DetailStencilLayerEntries to match the picked stencil's layer
        // count. Each new entry gets its Inks chips populated from the shared
        // ink palette so the same multi-select UI used for stamp inks works
        // per-layer.
        private void RebuildDetailStencilLayerEntries()
        {
            // The layer source is the picked stencil, or a stencil-combo stamp
            // when no stencil is chosen. Layer count comes from its StencilLayers.
            var layerSource = StencilsPicker.SelectedItem
                ?? (StampHasStencilCombo ? StampsPicker.SelectedItem : null);
            int layerCount = Math.Max(1, layerSource?.StencilLayers ?? 1);
            DetailStencilLayerEntries.Clear();
            int InkItemIdFor(string color) =>
                _inkItemIdByColor.TryGetValue(color, out var id) ? id : 0;
            for (int i = 1; i <= layerCount; i++)
            {
                var entry = new WizardStencilLayerEntry { LayerNumber = i };
                entry.Inks.SetColors(_inkColorOptions, InkItemIdFor);
                // Per-layer pickers share the same source data as the legacy global
                // pickers so the user sees identical Glitter / Happy Medium / Astro
                // Paste choices, just scoped to this layer.
                entry.GlitterPicker.Load(_stencilLayerGlitterItems);
                entry.HappyMediumPicker.Load(_stencilLayerHappyMediumItems);
                entry.AstroPastePicker.Load(_stencilLayerAstroPasteItems);
                DetailStencilLayerEntries.Add(entry);
            }
            DetailStencilLayerIndex = 0;
            OnPropertyChanged(nameof(DetailStencilLayerEntry));
            OnPropertyChanged(nameof(DetailStencilLayerHeader));
            OnPropertyChanged(nameof(HasPreviousDetailStencilLayer));
            OnPropertyChanged(nameof(HasNextDetailStencilLayer));
            OnPropertyChanged(nameof(IsLastDetailStencilLayer));
        }

        // ── Visibility flags for the Selection Details panel ──────────────────
        public bool ShowStampFollowups        => StampsPicker.SelectedItem != null;
        public bool StampHasDieCombo          => SubtypeContains(StampsPicker.SelectedItem, "Die Combo");
        // A stamp whose subtype marks it as a stencil combo: the user picks the
        // main stamp color (existing stamp-ink follow-up) and then walks each
        // stencil layer's ink, exactly like picking a stencil from the Stencils
        // dropdown. The layer count comes from the stamp's StencilLayers field.
        // Matches both the correct "Stencil Combo" and the "Sencil Combo" typo
        // that exists across the real inventory data, so neither spelling is missed.
        public bool StampHasStencilCombo      => SubtypeContains(StampsPicker.SelectedItem, "Stencil Combo")
                                              || SubtypeContains(StampsPicker.SelectedItem, "Sencil Combo");
        // Drives the per-layer stencil stepper, which now serves both a picked
        // stencil and a stencil-combo stamp.
        public bool ShowDetailStencilLayers   => ShowStencilFollowups || StampHasStencilCombo;
        // Stencil layers should be saved for a picked stencil OR a combo stamp.
        public bool HasDetailStencilLayers    => StencilsPicker.SelectedItem != null || StampHasStencilCombo;
        public bool ShowDieFollowups          => DiesPicker.SelectedItem != null;
        public bool ShowEmbellEmbossingFollowups => SubtypeContains(EmbellishmentsPicker.SelectedItem, "Embossing Powder");
        public bool ShowStackletFollowups     => StackletsPicker.SelectedItem != null;
        public bool ShowStencilFollowups      => StencilsPicker.SelectedItem != null;
        public bool ShowFoilFollowups         => FoilsPicker.SelectedItem != null;
        public bool ShowAnyFollowups          => ShowStampFollowups || ShowDieFollowups
                                              || ShowEmbellEmbossingFollowups || ShowStackletFollowups
                                              || ShowStencilFollowups || ShowFoilFollowups;

        // ── Live preview of what the user is about to save (above the save buttons) ──
        // Just lists the picked item names with a thin separator. Not detailed by design —
        // the user said "doesn't need to be extremely detailed, but maybe just show the item name".
        public bool HasCurrentDetailPreview =>
            StampsPicker.SelectedItem != null ||
            DiesPicker.SelectedItem != null ||
            EmbellishmentsPicker.SelectedItem != null ||
            StackletsPicker.SelectedItem != null ||
            EmbossingFoldersPicker.SelectedItem != null ||
            StencilsPicker.SelectedItem != null ||
            OloMarkersPicker.SelectedItems.Count > 0 ||
            FoilsPicker.SelectedItem != null ||
            WatercolorsPicker.SelectedItem != null ||
            !string.IsNullOrEmpty(SelectedInkColor);

        public string CurrentDetailPreview
        {
            get
            {
                var parts = new List<string>();
                if (StampsPicker.SelectedItem != null)           parts.Add(StampsPicker.SelectedItem.Name);
                if (DiesPicker.SelectedItem != null)             parts.Add(DiesPicker.SelectedItem.Name);
                if (EmbellishmentsPicker.SelectedItem != null)   parts.Add(EmbellishmentsPicker.SelectedItem.Name);
                if (StackletsPicker.SelectedItem != null)        parts.Add(StackletsPicker.SelectedItem.Name);
                if (EmbossingFoldersPicker.SelectedItem != null) parts.Add(EmbossingFoldersPicker.SelectedItem.Name);
                if (StencilsPicker.SelectedItem != null)         parts.Add(StencilsPicker.SelectedItem.Name);
                if (OloMarkersPicker.SelectedItems.Count > 0)    parts.Add(string.Join(", ", OloMarkersPicker.SelectedItems.Select(m => m.Name)));
                if (FoilsPicker.SelectedItem != null)            parts.Add($"Foil: {FoilsPicker.SelectedItem.Name}");
                if (WatercolorsPicker.SelectedItem != null)      parts.Add(WatercolorsPicker.SelectedItem.Name);
                if (!string.IsNullOrEmpty(SelectedInkColor))     parts.Add($"Ink: {SelectedInkColor}");
                return parts.Count == 0 ? string.Empty : string.Join("   •   ", parts);
            }
        }

        private static bool SubtypeContains(WizardItemOption? item, string fragment) =>
            item?.Subtype != null && item.Subtype.Contains(fragment, StringComparison.OrdinalIgnoreCase);

        private void WireFollowupNotifications()
        {
            // Helper: any picker selection change refreshes the live "current selection" preview.
            void OnAnyPickerSelectionChanged()
            {
                OnPropertyChanged(nameof(CurrentDetailPreview));
                OnPropertyChanged(nameof(HasCurrentDetailPreview));
            }

            StampsPicker.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(WizardItemPicker.SelectedItem))
                {
                    OnPropertyChanged(nameof(ShowStampFollowups));
                    OnPropertyChanged(nameof(StampHasDieCombo));
                    OnPropertyChanged(nameof(StampHasStencilCombo));
                    OnPropertyChanged(nameof(ShowDetailStencilLayers));
                    OnPropertyChanged(nameof(ShowAnyFollowups));
                    // A stencil-combo stamp drives the same layer stepper as the
                    // Stencils dropdown. Build it from the stamp's layer count, or
                    // clear it when deselecting (unless a stencil is still picked).
                    if (StampHasStencilCombo) RebuildDetailStencilLayerEntries();
                    else if (StencilsPicker.SelectedItem == null)
                    {
                        DetailStencilLayerEntries.Clear();
                        DetailStencilLayerIndex = 0;
                    }
                    OnAnyPickerSelectionChanged();
                }
            };
            DiesPicker.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(WizardItemPicker.SelectedItem))
                {
                    OnPropertyChanged(nameof(ShowDieFollowups));
                    OnPropertyChanged(nameof(ShowAnyFollowups));
                    OnAnyPickerSelectionChanged();
                }
            };
            EmbellishmentsPicker.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(WizardItemPicker.SelectedItem))
                {
                    OnPropertyChanged(nameof(ShowEmbellEmbossingFollowups));
                    OnPropertyChanged(nameof(ShowAnyFollowups));
                    OnPropertyChanged(nameof(HasHubEmbellishmentPick));
                    OnPropertyChanged(nameof(CurrentHubEmbellishmentPreview));
                    OnAnyPickerSelectionChanged();
                }
            };
            // Hub embellishment preview also depends on the embossing-powder follow-up
            // picks (ink colors + stamp). Notify whenever those change too.
            EmbellEmbossingStampPicker.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(WizardItemPicker.SelectedItem))
                    OnPropertyChanged(nameof(CurrentHubEmbellishmentPreview));
            };
            EmbellEmbossingInks.Ordered.CollectionChanged += (_, _) =>
                OnPropertyChanged(nameof(CurrentHubEmbellishmentPreview));
            StackletsPicker.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(WizardItemPicker.SelectedItem))
                {
                    OnPropertyChanged(nameof(ShowStackletFollowups));
                    OnPropertyChanged(nameof(ShowAnyFollowups));
                    OnAnyPickerSelectionChanged();
                }
            };
            // EF doesn't drive a follow-up card; the rest do.
            EmbossingFoldersPicker.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(WizardItemPicker.SelectedItem)) OnAnyPickerSelectionChanged();
            };
            StencilsPicker.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(WizardItemPicker.SelectedItem))
                {
                    OnPropertyChanged(nameof(ShowStencilFollowups));
                    OnPropertyChanged(nameof(ShowDetailStencilLayers));
                    OnPropertyChanged(nameof(ShowAnyFollowups));
                    if (StencilsPicker.SelectedItem != null) RebuildDetailStencilLayerEntries();
                    else if (StampHasStencilCombo) RebuildDetailStencilLayerEntries();
                    else { DetailStencilLayerEntries.Clear(); DetailStencilLayerIndex = 0; }
                    OnAnyPickerSelectionChanged();
                }
            };
            OloMarkersPicker.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(WizardItemPicker.SelectedItem)) OnAnyPickerSelectionChanged();
            };
            FoilsPicker.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(WizardItemPicker.SelectedItem))
                {
                    OnPropertyChanged(nameof(ShowFoilFollowups));
                    OnPropertyChanged(nameof(ShowAnyFollowups));
                    OnAnyPickerSelectionChanged();
                }
            };
            WatercolorsPicker.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(WizardItemPicker.SelectedItem)) OnAnyPickerSelectionChanged();
            };

            void OnAdhesivePickerChanged()
            {
                OnPropertyChanged(nameof(HasCurrentCardBaseAdhesivePick));
                OnPropertyChanged(nameof(CurrentCardBaseAdhesivePreview));
            }
            GlueAdhesivePicker.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(WizardItemPicker.SelectedItem)) OnAdhesivePickerChanged();
            };
            FoamAdhesivePicker.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(WizardItemPicker.SelectedItem)) OnAdhesivePickerChanged();
            };
            TapeRunnerAdhesivePicker.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(WizardItemPicker.SelectedItem)) OnAdhesivePickerChanged();
            };

            // BG Mat hub — Cardstock pickers feed mutual-exclusion echo + live save-state.
            void OnBgPieceCardstockChanged(WizardItemPicker source)
            {
                // Echo: picking from one bucket clears the other two.
                if (source == BgPieceCardstockPicker && BgPieceCardstockPicker.SelectedItem != null)
                {
                    BgPieceFoilCardstockPicker.SelectedItem = null;
                    BgPieceGlitterCardstockPicker.SelectedItem = null;
                }
                else if (source == BgPieceFoilCardstockPicker && BgPieceFoilCardstockPicker.SelectedItem != null)
                {
                    BgPieceCardstockPicker.SelectedItem = null;
                    BgPieceGlitterCardstockPicker.SelectedItem = null;
                }
                else if (source == BgPieceGlitterCardstockPicker && BgPieceGlitterCardstockPicker.SelectedItem != null)
                {
                    BgPieceCardstockPicker.SelectedItem = null;
                    BgPieceFoilCardstockPicker.SelectedItem = null;
                }
            }
            BgPieceCardstockPicker.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(WizardItemPicker.SelectedItem)) OnBgPieceCardstockChanged(BgPieceCardstockPicker);
            };
            BgPieceFoilCardstockPicker.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(WizardItemPicker.SelectedItem)) OnBgPieceCardstockChanged(BgPieceFoilCardstockPicker);
            };
            BgPieceGlitterCardstockPicker.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(WizardItemPicker.SelectedItem)) OnBgPieceCardstockChanged(BgPieceGlitterCardstockPicker);
            };

            // BG Mat hub — Adhesives pickers drive the live "current selection" preview.
            void OnBgPieceAdhesivePickerChanged()
            {
                OnPropertyChanged(nameof(HasCurrentBgPieceAdhesivePick));
                OnPropertyChanged(nameof(CurrentBgPieceAdhesivePreview));
            }
            BgPieceGlueAdhesivePicker.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(WizardItemPicker.SelectedItem)) OnBgPieceAdhesivePickerChanged();
            };
            BgPieceFoamAdhesivePicker.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(WizardItemPicker.SelectedItem)) OnBgPieceAdhesivePickerChanged();
            };
            BgPieceTapeRunnerAdhesivePicker.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(WizardItemPicker.SelectedItem)) OnBgPieceAdhesivePickerChanged();
            };

            // Sentiment hub — Cardstock pickers: same mutual-exclusion echo pattern.
            void OnSentimentCardstockChanged(WizardItemPicker source)
            {
                if (source == SentimentCardstockPicker && SentimentCardstockPicker.SelectedItem != null)
                {
                    SentimentFoilCardstockPicker.SelectedItem = null;
                    SentimentGlitterCardstockPicker.SelectedItem = null;
                }
                else if (source == SentimentFoilCardstockPicker && SentimentFoilCardstockPicker.SelectedItem != null)
                {
                    SentimentCardstockPicker.SelectedItem = null;
                    SentimentGlitterCardstockPicker.SelectedItem = null;
                }
                else if (source == SentimentGlitterCardstockPicker && SentimentGlitterCardstockPicker.SelectedItem != null)
                {
                    SentimentCardstockPicker.SelectedItem = null;
                    SentimentFoilCardstockPicker.SelectedItem = null;
                }
            }
            SentimentCardstockPicker.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(WizardItemPicker.SelectedItem)) OnSentimentCardstockChanged(SentimentCardstockPicker);
            };
            SentimentFoilCardstockPicker.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(WizardItemPicker.SelectedItem)) OnSentimentCardstockChanged(SentimentFoilCardstockPicker);
            };
            SentimentGlitterCardstockPicker.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(WizardItemPicker.SelectedItem)) OnSentimentCardstockChanged(SentimentGlitterCardstockPicker);
            };

            // Sentiment hub — Adhesives drive the live "current selection" preview.
            void OnSentimentAdhesivePickerChanged()
            {
                OnPropertyChanged(nameof(HasCurrentSentimentAdhesivePick));
                OnPropertyChanged(nameof(CurrentSentimentAdhesivePreview));
            }
            SentimentGlueAdhesivePicker.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(WizardItemPicker.SelectedItem)) OnSentimentAdhesivePickerChanged();
            };
            SentimentFoamAdhesivePicker.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(WizardItemPicker.SelectedItem)) OnSentimentAdhesivePickerChanged();
            };
            SentimentTapeRunnerAdhesivePicker.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(WizardItemPicker.SelectedItem)) OnSentimentAdhesivePickerChanged();
            };
        }

        partial void OnSelectedInkColorChanged(string? value)
        {
            OnPropertyChanged(nameof(CurrentDetailPreview));
            OnPropertyChanged(nameof(HasCurrentDetailPreview));
        }

        // Inks / Watercolors picker has a mode toggle on top of the dropdown:
        //  - Inks mode = list of color names from the ColorOrder config (no items, no images)
        //  - Watercolors mode = items with type "Watercolor"
        public ObservableCollection<string> InkColorOptionsForPicker { get; } = new();
        public WizardItemPicker WatercolorsPicker { get; } = new() { PlaceholderText = "Watercolors" };

        [ObservableProperty] private bool _inksWatercolorsIsWatercolorMode;   // false = Inks, true = Watercolors
        [ObservableProperty] private string? _selectedInkColor;
        [ObservableProperty] private bool _inksWatercolorsIsOpen;

        // Search / filter for the Details Inks dropdown. Bound to a TextBox at the
        // top of the inks scroll list; filters DetailsInks.Chips by Color contains.
        [ObservableProperty] private string _detailsInkSearchText = string.Empty;
        public IEnumerable<InkColorChip> FilteredDetailsInkChips =>
            string.IsNullOrWhiteSpace(DetailsInkSearchText)
                ? DetailsInks.Chips
                : DetailsInks.Chips.Where(c => c.Color != null &&
                    c.Color.Contains(DetailsInkSearchText, StringComparison.OrdinalIgnoreCase));
        partial void OnDetailsInkSearchTextChanged(string value) =>
            OnPropertyChanged(nameof(FilteredDetailsInkChips));

        [RelayCommand] private void SetInksMode()        => InksWatercolorsIsWatercolorMode = false;
        [RelayCommand] private void SetWatercolorsMode() => InksWatercolorsIsWatercolorMode = true;
        [RelayCommand] private void SelectInkColor(string? color)
        {
            if (!string.IsNullOrEmpty(color))
            {
                SelectedInkColor = color;
                WatercolorsPicker.SelectedItem = null;
                InksWatercolorsIsOpen = false;
            }
        }
        [RelayCommand] private void SelectWatercolor(WizardItemOption? item)
        {
            if (item != null)
            {
                WatercolorsPicker.SelectedItem = item;
                SelectedInkColor = null;
                InksWatercolorsIsOpen = false;
            }
        }

        // ── Generic Details page state (shared across parent contexts) ────────
        // The Details page is reused from cardbase, mats, sentiments, etc.
        // DetailsReturnTarget identifies who opened it; Save & Return dispatches on it,
        // and the bottom button's label adapts to the parent.
        [ObservableProperty] private string _detailsReturnTarget = "CardBase";

        partial void OnDetailsReturnTargetChanged(string value)
        {
            OnPropertyChanged(nameof(DetailsReturnButtonLabel));
            OnPropertyChanged(nameof(DetailsBackButtonLabel));
            OnPropertyChanged(nameof(AddedDetailsForCurrentTarget));
        }

        public string DetailsReturnButtonLabel => DetailsReturnTarget switch
        {
            "CardBase"        => "Save & Return to Cardbase",
            "BackgroundMat"   => "Save & Return to Background Mat",
            "AdditionalMat"   => "Save & Return to Additional Mat",
            "FocalMat"        => "Save & Return to Focal Mat",
            "Sentiment"       => "Save & Return to Sentiment",
            "Inside"          => "Save & Return to Inside",
            "InsideMisc"      => "Save & Return to Inside",
            _                 => "Save & Return"
        };

        public string DetailsBackButtonLabel => DetailsReturnTarget switch
        {
            "CardBase"        => "← Back to Cardbase",
            "BackgroundMat"   => "← Back to Background Mat",
            "AdditionalMat"   => "← Back to Additional Mat",
            "FocalMat"        => "← Back to Focal Mat",
            "Sentiment"       => "← Back to Sentiment",
            "Inside"          => "← Back to Inside",
            "InsideMisc"      => "← Back to Inside",
            _                 => "← Back"
        };

        // Per-parent collections of captured detail entries. CardBase is wired today;
        // future targets get their own ObservableCollection<WizardDetailEntry> here.
        public ObservableCollection<WizardDetailEntry> CardBaseAddedDetails { get; } = new();

        // Returns the appropriate added-details list for whatever opened the Details page.
        // The Details panel binds to this via AddedDetailsForCurrentTarget so the chip-strip
        // shows the right list automatically.
        //
        // Alternative considered: a single shared list with a "target" tag on each entry;
        // rejected — per-target lists keep navigation/edit logic localized to each parent.
        public ObservableCollection<WizardDetailEntry> AddedDetailsForCurrentTarget => DetailsReturnTarget switch
        {
            "CardBase"       => CardBaseAddedDetails,
            "BackgroundMat"  => CurrentMat?.AddedDetails ?? _emptyDetails,
            "AdditionalMat"  => CurrentMat?.AddedDetails ?? _emptyDetails,
            "FocalMat"       => CurrentMat?.AddedDetails ?? _emptyDetails,
            "Sentiment"      => _pendingSentimentDetails,
            "InsideMisc"     => InsideMiscDetails,
            _                => CardBaseAddedDetails
        };

        // Empty fallback so AddedDetailsForCurrentTarget never returns null (keeps the
        // chip-strip binding's Count check stable when CurrentMat is briefly null).
        private static readonly ObservableCollection<WizardDetailEntry> _emptyDetails = new();

        // Public accessor for the BG mat hub's "Added pieces" chip strip. The
        // underlying _currentBgMatGroup is a plain field (not an ObservableProperty)
        // because the legacy add-form code also writes to it directly — wrapping
        // would change those writes' semantics. Re-fire OnPropertyChanged after
        // every reassignment in the new hub commands.
        private static readonly ObservableCollection<WizardBgMat> _emptyBgPieces = new();
        public ObservableCollection<WizardBgMat> CurrentBgMatPieces =>
            _currentBgMatGroup?.Pieces ?? _emptyBgPieces;

        // Tracks how many distinct "detail entries" the user has added (computed from list count).
        public int CardBaseDetailsCount => CardBaseAddedDetails.Count;

        // "Save & Add Another" — capture current picker selections as one detail entry,
        // clear the pickers, stay on the Details page so the user can add another set.
        [RelayCommand]
        private void SaveDetailsAndAddAnother()
        {
            if (!HasAnyDetailSelection()) return;
            CaptureCurrentDetailEntry();
            ClearDetailSelections();
            UpdateSummaryLines();
            // stay on Details page
        }

        // "Save & Return" — capture (if anything's selected) and navigate back to the parent.
        [RelayCommand]
        private void SaveDetailsAndReturn()
        {
            if (HasAnyDetailSelection())
                CaptureCurrentDetailEntry();
            UpdateSummaryLines();

            switch (DetailsReturnTarget)
            {
                case "CardBase":
                    CurrentCardBaseStep = "Hub";
                    break;
                case "BackgroundMat":
                case "AdditionalMat":
                case "FocalMat":
                    BgPieceDetailsSaved = (CurrentMat?.AddedDetails.Count ?? 0) > 0;
                    BgMatHubStep = "Hub";
                    break;
                case "Sentiment":
                    SentimentPieceDetailsSaved = _pendingSentimentDetails.Count > 0;
                    SentimentSubStep = "Hub";
                    break;
                case "InsideMisc":
                    // Top-level Details button on the inside hub. No sub-step
                    // to unwind; just flip the Saved flag and pop back to the
                    // (inside-mode) hub.
                    InsideDetailsSaved = InsideMiscDetails.Count > 0;
                    CurrentSection = "Hub";
                    break;
                default:
                    CurrentCardBaseStep = "Hub";
                    CurrentSection = "Hub";
                    break;
            }
        }

        // Removes a previously-captured detail entry (X button on each chip).
        [RelayCommand]
        private void RemoveAddedDetail(WizardDetailEntry? entry)
        {
            if (entry == null) return;
            var list = AddedDetailsForCurrentTarget;
            list.Remove(entry);
            OnPropertyChanged(nameof(CardBaseDetailsCount));
            OnPropertyChanged(nameof(AddedDetailsForCurrentTarget));
            CardBaseDetailsSaved = CardBaseAddedDetails.Count > 0;
            if (CurrentMat != null)
                BgPieceDetailsSaved = CurrentMat.AddedDetails.Count > 0;
            UpdateSummaryLines();
        }

        private void CaptureCurrentDetailEntry()
        {
            var entry = new WizardDetailEntry
            {
                Stamp                    = StampsPicker.SelectedItem,
                StampInkColors           = StampInks.Ordered.ToList(),
                StampWasEmbossed         = StampInks.IsEmbossed,
                StampEmbossingPowder     = StampInks.IsEmbossed ? StampEmbossingPowderPicker.SelectedItem : null,
                StampUsedAsCombo         = StampHasDieCombo && StampUsedAsCombo,
                StampComboLayers         = (StampHasDieCombo && StampUsedAsCombo) ? StampComboLayers : 1,
                Die                      = DiesPicker.SelectedItem,
                DieIsMultiLayer          = DieIsMultiLayer,
                DieLayers                = DieIsMultiLayer ? DieLayers : 1,
                Embellishment            = EmbellishmentsPicker.SelectedItem,
                EmbellEmbossingInkColors = ShowEmbellEmbossingFollowups
                    ? EmbellEmbossingInks.Ordered.ToList()
                    : new List<string>(),
                EmbellEmbossingStamp     = ShowEmbellEmbossingFollowups ? EmbellEmbossingStampPicker.SelectedItem : null,
                Stacklet                 = StackletsPicker.SelectedItem,
                StackletDieNumber        = StackletDieNumber,
                StackletLayers           = StackletLayers,
                EmbossingFolder          = EmbossingFoldersPicker.SelectedItem,
                Stencil                  = StencilsPicker.SelectedItem,
                StencilLayerEntries      = HasDetailStencilLayers
                    ? DetailStencilLayerEntries.Select(le => new WizardStencilLayer
                        {
                            LayerNumber      = le.LayerNumber,
                            InkColors        = le.Inks.Ordered.ToList(),
                            UsedGlitter      = le.UsedGlitter,
                            UsedHappyMedium  = le.UsedHappyMedium,
                            UsedAstroPaste   = le.UsedAstroPaste,
                            GlitterItems     = le.UsedGlitter      ? le.GlitterPicker.SelectedItems.ToList()     : new List<WizardItemOption>(),
                            HappyMediumItems = le.UsedHappyMedium  ? le.HappyMediumPicker.SelectedItems.ToList() : new List<WizardItemOption>(),
                            AstroPasteItems  = le.UsedAstroPaste   ? le.AstroPastePicker.SelectedItems.ToList()  : new List<WizardItemOption>(),
                        }).ToList()
                    : new List<WizardStencilLayer>(),
                // Flat aggregate of every layer's inks (preserves backward compat
                // with callers that read StencilInkColors directly).
                StencilInkColors         = HasDetailStencilLayers
                    ? DetailStencilLayerEntries.SelectMany(le => le.Inks.Ordered).Distinct().ToList()
                    : new List<string>(),
                // Aggregate the legacy "global" Used* flags + Glitter/HM/AP item
                // lists from the per-layer entries so existing build-step + summary
                // code (which iterates StencilGlitterItems etc.) keeps surfacing
                // every per-layer pick. Layer numbers that flagged the medium are
                // joined into the comma-separated Layers string for display.
                StencilUsedGlitter       = HasDetailStencilLayers
                    && DetailStencilLayerEntries.Any(le => le.UsedGlitter),
                StencilGlitterLayers     = HasDetailStencilLayers
                    ? string.Join(",", DetailStencilLayerEntries.Where(le => le.UsedGlitter).Select(le => le.LayerNumber))
                    : string.Empty,
                StencilUsedHappyMedium   = HasDetailStencilLayers
                    && DetailStencilLayerEntries.Any(le => le.UsedHappyMedium),
                StencilHappyMediumLayers = HasDetailStencilLayers
                    ? string.Join(",", DetailStencilLayerEntries.Where(le => le.UsedHappyMedium).Select(le => le.LayerNumber))
                    : string.Empty,
                StencilUsedAstroPaste    = HasDetailStencilLayers
                    && DetailStencilLayerEntries.Any(le => le.UsedAstroPaste),
                StencilAstroPasteLayers  = HasDetailStencilLayers
                    ? string.Join(",", DetailStencilLayerEntries.Where(le => le.UsedAstroPaste).Select(le => le.LayerNumber))
                    : string.Empty,
                StencilGlitterItems      = HasDetailStencilLayers
                    ? DetailStencilLayerEntries.Where(le => le.UsedGlitter)
                                               .SelectMany(le => le.GlitterPicker.SelectedItems)
                                               .GroupBy(i => i.Id).Select(g => g.First()).ToList()
                    : new List<WizardItemOption>(),
                StencilHappyMediumItems  = HasDetailStencilLayers
                    ? DetailStencilLayerEntries.Where(le => le.UsedHappyMedium)
                                               .SelectMany(le => le.HappyMediumPicker.SelectedItems)
                                               .GroupBy(i => i.Id).Select(g => g.First()).ToList()
                    : new List<WizardItemOption>(),
                StencilAstroPasteItems   = HasDetailStencilLayers
                    ? DetailStencilLayerEntries.Where(le => le.UsedAstroPaste)
                                               .SelectMany(le => le.AstroPastePicker.SelectedItems)
                                               .GroupBy(i => i.Id).Select(g => g.First()).ToList()
                    : new List<WizardItemOption>(),
                OloMarkers               = OloMarkersPicker.SelectedItems.ToList(),
                Foil                     = FoilsPicker.SelectedItem,
                FoilApplicationMethod    = FoilsPicker.SelectedItem != null ? FoilApplicationMethod : string.Empty,
                FoilStencil              = IsFoilGlitterGrabSelected ? FoilStencilPicker.SelectedItem : null,
                FoilStencilInkColors     = IsFoilGlitterGrabSelected ? FoilStencilInks.Ordered.ToList() : new List<string>(),
                FoilStencilUsedGlitter      = IsFoilGlitterGrabSelected && FoilStencilUsedGlitter,
                FoilStencilUsedHappyMedium  = IsFoilGlitterGrabSelected && FoilStencilUsedHappyMedium,
                FoilStencilUsedAstroPaste   = IsFoilGlitterGrabSelected && FoilStencilUsedAstroPaste,
                FoilStencilGlitterLayers     = (IsFoilGlitterGrabSelected && FoilStencilUsedGlitter)     ? (FoilStencilGlitterLayers     ?? string.Empty) : string.Empty,
                FoilStencilHappyMediumLayers = (IsFoilGlitterGrabSelected && FoilStencilUsedHappyMedium) ? (FoilStencilHappyMediumLayers ?? string.Empty) : string.Empty,
                FoilStencilAstroPasteLayers  = (IsFoilGlitterGrabSelected && FoilStencilUsedAstroPaste)  ? (FoilStencilAstroPasteLayers  ?? string.Empty) : string.Empty,
                FoilStencilGlitterItems     = (IsFoilGlitterGrabSelected && FoilStencilUsedGlitter)
                    ? FoilStencilGlitterPicker.SelectedItems.ToList() : new List<WizardItemOption>(),
                FoilStencilHappyMediumItems = (IsFoilGlitterGrabSelected && FoilStencilUsedHappyMedium)
                    ? FoilStencilHappyMediumPicker.SelectedItems.ToList() : new List<WizardItemOption>(),
                FoilStencilAstroPasteItems  = (IsFoilGlitterGrabSelected && FoilStencilUsedAstroPaste)
                    ? FoilStencilAstroPastePicker.SelectedItems.ToList() : new List<WizardItemOption>(),
                FoilTonerText            = IsFoilTonerSelected ? (FoilTonerText ?? string.Empty) : string.Empty,
                FoilTonerFont            = IsFoilTonerSelected
                    ? (string.IsNullOrWhiteSpace(FoilTonerCustomFont) ? (FoilTonerFont ?? string.Empty) : FoilTonerCustomFont)
                    : string.Empty,
                Watercolor               = WatercolorsPicker.SelectedItem,
                // If the user enabled Custom Color and picked from the multi-
                // select dropdown, persist the joined list as the ink colour;
                // otherwise fall back to the legacy single-select SelectedInkColor.
                InkColor                 = DetailsInks.HasSelection
                    ? string.Join(", ", DetailsInks.Ordered)
                    : SelectedInkColor
            };

            switch (DetailsReturnTarget)
            {
                case "CardBase":
                    CardBaseAddedDetails.Add(entry);
                    OnPropertyChanged(nameof(CardBaseDetailsCount));
                    CardBaseDetailsSaved = true;
                    break;
                case "BackgroundMat":
                case "AdditionalMat":
                case "FocalMat":
                    // All three mat hubs use WizardBgMat for their pieces, so the
                    // detail entry lands on whichever mat the user is currently
                    // editing (CurrentMat is reassigned per-target).
                    if (CurrentMat != null)
                    {
                        CurrentMat.AddedDetails.Add(entry);
                        OnPropertyChanged(nameof(AddedDetailsForCurrentTarget));
                        BgPieceDetailsSaved = true;
                    }
                    break;
                case "Sentiment":
                    _pendingSentimentDetails.Add(entry);
                    OnPropertyChanged(nameof(AddedDetailsForCurrentTarget));
                    SentimentPieceDetailsSaved = true;
                    break;
            }
        }

        private bool HasAnyDetailSelection()
        {
            return StampsPicker.SelectedItem != null
                || DiesPicker.SelectedItem != null
                || EmbellishmentsPicker.SelectedItem != null
                || StackletsPicker.SelectedItem != null
                || EmbossingFoldersPicker.SelectedItem != null
                || StencilsPicker.SelectedItem != null
                || OloMarkersPicker.SelectedItems.Count > 0
                || FoilsPicker.SelectedItem != null
                || WatercolorsPicker.SelectedItem != null
                || !string.IsNullOrEmpty(SelectedInkColor)
                || DetailsInks.HasSelection;
        }

        private void ClearDetailSelections()
        {
            StampsPicker.SelectedItem = null;
            DiesPicker.SelectedItem = null;
            EmbellishmentsPicker.SelectedItem = null;
            StackletsPicker.SelectedItem = null;
            EmbossingFoldersPicker.SelectedItem = null;
            StencilsPicker.SelectedItem = null;
            OloMarkersPicker.SelectedItems.Clear();
            WatercolorsPicker.SelectedItem = null;
            SelectedInkColor = null;
            // Reset follow-up answers
            StampEmbossingPowderPicker.SelectedItem = null;
            EmbellEmbossingStampPicker.SelectedItem = null;
            StampInks.Clear();
            EmbellEmbossingInks.Clear();
            StencilInks.Clear();
            DetailsInks.Clear();
            StampUsedAsCombo = false;
            StampComboLayers = 1;
            DieIsMultiLayer = false;
            DieLayers = 1;
            StackletDieNumber = 1;
            StackletLayers = 1;
            StencilUsedGlitter = false;
            StencilGlitterLayers = string.Empty;
            StencilUsedHappyMedium = false;
            StencilHappyMediumLayers = string.Empty;
            StencilUsedAstroPaste = false;
            StencilAstroPasteLayers = string.Empty;
            // Per-layer stepper state — wiped along with the picker.
            DetailStencilLayerEntries.Clear();
            DetailStencilLayerIndex = 0;
            // Stencil follow-up multi-pickers
            StencilGlitterPicker.SelectedItems.Clear();
            StencilHappyMediumPicker.SelectedItems.Clear();
            StencilAstroPastePicker.SelectedItems.Clear();
            // Foils follow-up
            FoilsPicker.SelectedItem = null;
            FoilApplicationMethod = string.Empty;
            FoilStencilPicker.SelectedItem = null;
            FoilStencilInks.Clear();
            FoilStencilUsedGlitter = false;
            FoilStencilUsedHappyMedium = false;
            FoilStencilUsedAstroPaste = false;
            FoilStencilGlitterLayers = string.Empty;
            FoilStencilHappyMediumLayers = string.Empty;
            FoilStencilAstroPasteLayers = string.Empty;
            FoilStencilGlitterPicker.SelectedItems.Clear();
            FoilStencilHappyMediumPicker.SelectedItems.Clear();
            FoilStencilAstroPastePicker.SelectedItems.Clear();
            FoilTonerText = string.Empty;
            FoilTonerFont = string.Empty;
            FoilTonerCustomFont = string.Empty;
        }

        // ── Saved-step indicators (drives " - Done!" suffix on hub buttons) ────
        // Cardbase sub-hub steps:
        [ObservableProperty] private bool _cardFoldSaved;
        [ObservableProperty] private bool _cardStockSaved;
        [ObservableProperty] private bool _cardBaseDetailsSaved;
        [ObservableProperty] private bool _cardBaseAdhesivesSaved;
        // Main hub sections (set when their respective Save command runs):
        [ObservableProperty] private bool _cardBaseSaved;
        [ObservableProperty] private bool _backgroundMatSaved;
        [ObservableProperty] private bool _additionalMatSaved;
        [ObservableProperty] private bool _focalMatSaved;
        [ObservableProperty] private bool _sentimentSaved;
        [ObservableProperty] private bool _embellishmentsSaved;
        [ObservableProperty] private bool _insideSaved;
        [ObservableProperty] private bool _envelopeSaved;

        [RelayCommand]
        private void BackToCardBaseHub()
        {
            CurrentCardBaseStep = "Hub";
        }

        [RelayCommand]
        private void SaveCardBaseAndBackToHub()
        {
            CardBaseSaved = true;
            CurrentCardBaseStep = "Hub";
            CurrentSection = "Hub";
            UpdateSummaryLines();
        }

        // ── Cardbase / Adhesives sub-page ─────────────────────────────────────
        // Three pickers, each pre-filtered at load time to its target subtype.
        public WizardItemPicker GlueAdhesivePicker       { get; } = new() { PlaceholderText = "Glue" };
        public WizardItemPicker FoamAdhesivePicker       { get; } = new() { PlaceholderText = "Foam" };
        public WizardItemPicker TapeRunnerAdhesivePicker { get; } = new() { PlaceholderText = "Tape Runner" };

        public ObservableCollection<WizardItemOption> CardBaseAddedAdhesives { get; } = new();
        public int CardBaseAdhesivesCount => CardBaseAddedAdhesives.Count;

        public bool HasCurrentCardBaseAdhesivePick =>
            GlueAdhesivePicker.SelectedItem != null
            || FoamAdhesivePicker.SelectedItem != null
            || TapeRunnerAdhesivePicker.SelectedItem != null;

        public string CurrentCardBaseAdhesivePreview
        {
            get
            {
                var parts = new List<string>();
                if (GlueAdhesivePicker.SelectedItem != null)       parts.Add(GlueAdhesivePicker.SelectedItem.Name);
                if (FoamAdhesivePicker.SelectedItem != null)       parts.Add(FoamAdhesivePicker.SelectedItem.Name);
                if (TapeRunnerAdhesivePicker.SelectedItem != null) parts.Add(TapeRunnerAdhesivePicker.SelectedItem.Name);
                return parts.Count == 0 ? string.Empty : string.Join("   •   ", parts);
            }
        }

        [RelayCommand]
        private void SaveCardBaseAdhesives()
        {
            foreach (var picker in new[] { GlueAdhesivePicker, FoamAdhesivePicker, TapeRunnerAdhesivePicker })
            {
                if (picker.SelectedItem != null
                    && !CardBaseAddedAdhesives.Any(a => a.Id == picker.SelectedItem.Id))
                {
                    CardBaseAddedAdhesives.Add(picker.SelectedItem);
                }
                picker.SelectedItem = null;
            }
            OnPropertyChanged(nameof(CardBaseAdhesivesCount));
            CardBaseAdhesivesSaved = CardBaseAddedAdhesives.Count > 0;
            UpdateSummaryLines();
            CurrentCardBaseStep = "Hub";
        }

        [RelayCommand]
        private void RemoveCardBaseAdhesive(WizardItemOption? item)
        {
            if (item == null) return;
            CardBaseAddedAdhesives.Remove(item);
            OnPropertyChanged(nameof(CardBaseAdhesivesCount));
            CardBaseAdhesivesSaved = CardBaseAddedAdhesives.Count > 0;
            UpdateSummaryLines();
        }

        // ── Background Mat hub (Phase 1 of mat remaster) ──────────────────────
        // Each piece of a BG mat group flows through a 4-button hub:
        //   Cardstock | How was it cut
        //   Details   | Adhesives
        // plus action buttons: "Add 1 Piece of mat" (commit current piece, stay on hub)
        // and "Add Mat" (finalize the group, return to main hub).
        //
        // Alternative considered for Phase 1: a unified hub shared across BG /
        // Additional / Focal that reads/writes through a CurrentMatTarget switch.
        // Rejected for now — replicate per-mat first to validate UX, then DRY in a
        // later refactor when Additional and Focal land. See also: BgPiece*Picker
        // duplication below — sibling pickers will be added per-mat in Phase 2 / 3.
        [ObservableProperty] private string _bgMatHubStep = "Hub";

        partial void OnBgMatHubStepChanged(string value)
        {
            OnPropertyChanged(nameof(IsBgMatHubStep));
            OnPropertyChanged(nameof(IsBgMatCardstockStep));
            OnPropertyChanged(nameof(IsBgMatHowCutStep));
            OnPropertyChanged(nameof(IsBgMatDetailsStep));
            OnPropertyChanged(nameof(IsBgMatAdhesivesStep));
            OnPropertyChanged(nameof(IsDetailsStepActive));
        }

        // CurrentMat is also driven by the legacy add-form, so this partial fires
        // for both code paths. Re-broadcasting keeps Details / HowCut bindings fresh.
        partial void OnCurrentMatChanged(WizardBgMat? value)
        {
            OnPropertyChanged(nameof(AddedDetailsForCurrentTarget));
            NotifyCuttingMethodChanged();
        }

        public bool IsBgMatHubStep        => BgMatHubStep == "Hub";
        public bool IsBgMatCardstockStep  => BgMatHubStep == "Cardstock";
        public bool IsBgMatHowCutStep     => BgMatHubStep == "HowCut";
        public bool IsBgMatDetailsStep    => BgMatHubStep == "Details";
        public bool IsBgMatAdhesivesStep  => BgMatHubStep == "Adhesives";

        // ── Mat target (Background / Additional share the same hub layout) ─────
        // When the user clicks Background Mat or Additional Mat from the main hub
        // we set this so the hub knows which collection (BgMats vs AdditionalMats)
        // to add the in-progress group into and so labels/back buttons read right.
        // Focal Mat does not share this hub — it uses WizardFocalSection.
        [ObservableProperty] private string _currentMatTarget = "BackgroundMat";

        partial void OnCurrentMatTargetChanged(string value)
        {
            OnPropertyChanged(nameof(MatTitle));
            OnPropertyChanged(nameof(MatBackButtonLabel));
            OnPropertyChanged(nameof(MatPieceActionLabel));
            OnPropertyChanged(nameof(MatPieceWordCapitalized));
            OnPropertyChanged(nameof(IsAnyMatHubActive));
            // Switching between Background / Additional / Focal mat must not carry
            // a leftover search filter from the previous mat into the next one's
            // pickers, which would hide items and confuse the user.
            ResetAllPickerSearches();
        }

        /// <summary>Clears the search box on every item picker the VM owns. Called
        /// when switching mat targets so a stale search from one mat section can't
        /// hide items in the next. Reflection keeps this correct as pickers are
        /// added without having to maintain a hand-written list.</summary>
        private void ResetAllPickerSearches()
        {
            foreach (var prop in GetType().GetProperties())
            {
                if (prop.PropertyType == typeof(WizardItemPicker)
                    && prop.GetValue(this) is WizardItemPicker picker
                    && !string.IsNullOrEmpty(picker.SearchText))
                {
                    picker.SearchText = string.Empty;
                }
            }
            // The Details ink list has its own inline search box, reset it too.
            if (!string.IsNullOrEmpty(DetailsInkSearchText))
                DetailsInkSearchText = string.Empty;
        }

        // True when ANY of the three mat types is the current section. The shared
        // hub XAML uses this so a single set of sub-pages serves Background, Additional,
        // and Focal — only the labels and target collection vary.
        public bool IsAnyMatHubActive =>
            IsBackgroundMatSectionActive || IsAdditionalMatSectionActive || IsFocalMatSectionActive;

        // Kept as an alias so existing XAML bindings keep working — covers the same
        // three sections now that Focal also uses the shared hub.
        public bool IsBackgroundOrAdditionalMatActive => IsAnyMatHubActive;

        // Labels swap based on the active mat target.
        public string MatTitle => CurrentMatTarget switch
        {
            "AdditionalMat" => "Additional Mat",
            "FocalMat"      => "Focal Mat",
            _               => "Background Mat",
        };

        public string MatBackButtonLabel => CurrentMatTarget switch
        {
            "AdditionalMat" => "← Back to Additional Mat",
            "FocalMat"      => "← Back to Focal Mat",
            _               => "← Back to Background Mat",
        };

        // Focal Mat uses "Part" instead of "Piece" terminology per the user's spec.
        public string MatPieceActionLabel => CurrentMatTarget == "FocalMat"
            ? "Add 1 Part"
            : "Add 1 Piece of mat";

        public string MatPieceWordCapitalized => CurrentMatTarget == "FocalMat" ? "Part" : "Piece";

        // Per-piece "X — Done!" indicators on the hub buttons. Reset between pieces.
        [ObservableProperty] private bool _bgPieceCardstockSaved;
        [ObservableProperty] private bool _bgPieceHowCutSaved;
        [ObservableProperty] private bool _bgPieceDetailsSaved;
        [ObservableProperty] private bool _bgPieceAdhesivesSaved;

        // Reuses the existing legacy `_currentBgMatGroup` field (declared further down
        // alongside other mat in-progress state). The legacy add-form drives that field
        // directly; the new hub does too. No [ObservableProperty] needed — XAML doesn't
        // bind to it; commands just read/write through the field reference.

        // ── BG Mat hub navigation ─────────────────────────────────────────────
        [RelayCommand] private void NavBgMatToHub()       => BgMatHubStep = "Hub";
        [RelayCommand] private void NavBgMatToCardstock() => BgMatHubStep = "Cardstock";
        [RelayCommand] private void NavBgMatToHowCut()    => BgMatHubStep = "HowCut";
        [RelayCommand]
        private void NavBgMatToDetails()
        {
            // Tag the Details panel with the current mat target so Save & Return
            // knows whether to flip BgMatHubStep back for Background or Additional.
            DetailsReturnTarget = CurrentMatTarget;
            BgMatHubStep = "Details";
        }
        [RelayCommand] private void NavBgMatToAdhesives() => BgMatHubStep = "Adhesives";
        [RelayCommand] private void BackToBgMatHub()      => BgMatHubStep = "Hub";

        // ── BG Mat / Cardstock sub-page ───────────────────────────────────────
        // Three pickers backed by the same 8.5x11 cardstock item lists used by the
        // Cardbase Cardstock sub-page. Mat data only persists the color *string*
        // (CurrentMat.SelectedCardstockColor), so picking from any bucket sets that
        // string and clears the other two pickers.
        //
        // Alternative considered: share the Cardbase pickers and reset on entry.
        // Rejected — separate pickers keep selection state independent so toggling
        // back to Cardbase doesn't lose its chosen item.
        public WizardItemPicker BgPieceCardstockPicker        { get; } = new() { PlaceholderText = "Cardstock" };
        public WizardItemPicker BgPieceFoilCardstockPicker    { get; } = new() { PlaceholderText = "Foil Cardstock" };
        public WizardItemPicker BgPieceGlitterCardstockPicker { get; } = new() { PlaceholderText = "Glitter Cardstock" };

        // Self-blend follow-up — shown below the 3 pickers, hidden by default.
        // Backed by CurrentMat.IsSelfBlended / SelfBlendDescription / BlendInkColors.

        [RelayCommand]
        private void SaveBgPieceCardstock()
        {
            if (CurrentMat == null) return;
            // Pick whichever bucket has a selection (echo logic clears the other two).
            var picked =
                BgPieceCardstockPicker.SelectedItem
                ?? BgPieceFoilCardstockPicker.SelectedItem
                ?? BgPieceGlitterCardstockPicker.SelectedItem;
            CurrentMat.SelectedCardstockColor = picked?.Name;

            // Sync blend ink picks → CurrentMat.BlendInkColors (only when self-blended).
            // Clears it otherwise so toggling self-blend off doesn't leave stale inks behind.
            CurrentMat.BlendInkColors.Clear();
            if (CurrentMat.IsSelfBlended)
                foreach (var c in BgPieceBlendInks.Ordered) CurrentMat.BlendInkColors.Add(c);

            BgPieceCardstockSaved = picked != null || CurrentMat.IsSelfBlended;
            UpdateSummaryLines();
            BgMatHubStep = "Hub";
        }

        // ── BG Mat / How was it cut sub-page ──────────────────────────────────
        // Per the user's mockup: pure method selection. Seven buttons in a 4×2
        // grid (Stacklets, All Planned Out, Frames, Insider, Foil-It, Custom Size,
        // None — eighth slot empty), then a wide "Confirm Cut" button.
        //
        // The actual item the method was applied to (e.g. which Stacklet die)
        // is captured via the Details sub-page, not here. That decouples
        // cutting *method* from the *items used*.
        //
        // Alternative considered: inline item picker on this page once a method
        // is chosen (matches legacy behavior). Rejected — mockup shows method
        // buttons + Confirm Cut only, with item selection deferred to Details.
        // Build-step emission for BG mats now derives item ids from
        // CurrentMat.AddedDetails rather than CurrentMat.{StackletItem,...}.
        [RelayCommand]
        private void SelectBgCuttingMethod(string? method)
        {
            if (CurrentMat == null || string.IsNullOrEmpty(method)) return;
            CurrentMat.CuttingMethod = method;
            // Custom / None methods replace any prior picks entirely — they're a "fresh
            // start" choice, not an addition to the cut tools.
            _suppressBgCutEcho = true;
            ClearAllBgCutPickers();
            ClearAllBgCutFieldsOnMat();
            _suppressBgCutEcho = false;
            NotifyCuttingMethodChanged();
        }

        // ── BG Mat / How was it cut: per-method dropdowns ─────────────────────
        // Each method now opens an item picker pre-filtered to its own data source.
        // Picking from any one dropdown sets CurrentMat.CuttingMethod, captures the
        // item to the matching CurrentMat.<X>Item field, and clears the other four
        // dropdowns + the matching mat fields (mutual exclusion — only one method
        // per piece).
        public WizardItemPicker BgCutStackletsPicker  { get; } = new() { PlaceholderText = "Stacklets" };
        public WizardItemPicker BgCutPlannedOutPicker { get; } = new() { PlaceholderText = "All Planned Out" };
        public WizardItemPicker BgCutFramesPicker     { get; } = new() { PlaceholderText = "Frames" };
        public WizardItemPicker BgCutInsiderPicker    { get; } = new() { PlaceholderText = "Insider" };
        public WizardItemPicker BgCutFoilItPicker     { get; } = new() { PlaceholderText = "Foil-It" };
        // Follow-up pickers shown inside the Cut details card.
        // Foils picker — pulls from type="Foils". Visible when method is Foil-It or Insider.
        public WizardItemPicker BgCutFoilsPicker      { get; } = new() { PlaceholderText = "Foil sheet" };

        // The "cut tools" group (Stacklets / Frames / All Planned Out) is mutually
        // exclusive within itself. Insider and Foil-It are NOT in this group — they
        // represent cardstock variants and may coexist alongside a cut tool so the
        // user can express "Insider cardstock cut with a Stacklet die".
        private void ClearBgCutToolPickersExcept(WizardItemPicker? keep)
        {
            if (keep != BgCutStackletsPicker)  BgCutStackletsPicker.SelectedItem  = null;
            if (keep != BgCutPlannedOutPicker) BgCutPlannedOutPicker.SelectedItem = null;
            if (keep != BgCutFramesPicker)     BgCutFramesPicker.SelectedItem     = null;
        }

        // Wipes ALL five top-level pickers + mat fields. Used by Custom / None buttons
        // and when switching pieces.
        private void ClearAllBgCutPickers()
        {
            BgCutStackletsPicker.SelectedItem  = null;
            BgCutPlannedOutPicker.SelectedItem = null;
            BgCutFramesPicker.SelectedItem     = null;
            BgCutInsiderPicker.SelectedItem    = null;
            BgCutFoilItPicker.SelectedItem     = null;
        }

        private void ClearBgCutToolFieldsOnMat()
        {
            if (CurrentMat == null) return;
            CurrentMat.StackletItem  = null;
            CurrentMat.PlannedOutItem = null;
            CurrentMat.FramesItem    = null;
        }

        private void ClearAllBgCutFieldsOnMat()
        {
            if (CurrentMat == null) return;
            CurrentMat.StackletItem  = null;
            CurrentMat.PlannedOutItem = null;
            CurrentMat.FramesItem    = null;
            CurrentMat.InsiderItem   = null;
            CurrentMat.FoilItItem    = null;
        }

        private bool _suppressBgCutEcho;
        private void WireBgCutPickerEcho()
        {
            // Cut-tool pickers (Stacklets / Frames / All Planned Out) — picking one
            // clears the other two cut tools (mutual exclusion within the group),
            // sets CuttingMethod, but DOES NOT clear Insider or Foil-It picks. This
            // way the user can pick e.g. "Insider X" + "Stacklet Y" together.
            void WireCutTool(WizardItemPicker picker, string method, Action<WizardItemOption?> assign)
            {
                picker.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName != nameof(WizardItemPicker.SelectedItem)) return;
                    if (_suppressBgCutEcho) return;
                    if (picker.SelectedItem == null) return;
                    if (CurrentMat == null) return;

                    _suppressBgCutEcho = true;
                    ClearBgCutToolPickersExcept(picker);
                    ClearBgCutToolFieldsOnMat();
                    assign(picker.SelectedItem);
                    CurrentMat.CuttingMethod = method;
                    NotifyCuttingMethodChanged();
                    _suppressBgCutEcho = false;
                };
            }
            WireCutTool(BgCutStackletsPicker,  "Stacklets",       v => CurrentMat!.StackletItem   = v);
            WireCutTool(BgCutPlannedOutPicker, "All Planned Out", v => CurrentMat!.PlannedOutItem = v);
            WireCutTool(BgCutFramesPicker,     "Frames",          v => CurrentMat!.FramesItem     = v);

            // Cardstock-variant pickers (Insider / Foil-It) — independent. Picking one
            // sets the matching mat field and updates the summary, but does NOT clear
            // the cut tools or the other variant. CuttingMethod is only updated to
            // Insider / Foil-It if no cut tool is currently picked.
            void WireCardstockVariant(WizardItemPicker picker, string method, Action<WizardItemOption?> assign)
            {
                picker.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName != nameof(WizardItemPicker.SelectedItem)) return;
                    if (_suppressBgCutEcho) return;
                    if (CurrentMat == null) return;

                    _suppressBgCutEcho = true;
                    assign(picker.SelectedItem);
                    // Only adopt the variant as the "method" if no cut tool drives the piece.
                    bool noCutTool = CurrentMat.StackletItem == null
                                  && CurrentMat.PlannedOutItem == null
                                  && CurrentMat.FramesItem == null;
                    if (picker.SelectedItem != null && noCutTool)
                        CurrentMat.CuttingMethod = method;
                    else if (picker.SelectedItem == null && CurrentMat.CuttingMethod == method && noCutTool)
                        CurrentMat.CuttingMethod = string.Empty;
                    NotifyCuttingMethodChanged();
                    _suppressBgCutEcho = false;
                };
            }
            WireCardstockVariant(BgCutInsiderPicker, "Insider", v => CurrentMat!.InsiderItem = v);
            WireCardstockVariant(BgCutFoilItPicker,  "Foil-It", v => CurrentMat!.FoilItItem  = v);

            // Foils side picker (Foil-It / Insider methods only) — independent. Just
            // mirrors the selection onto CurrentMat.FoilsItem and refreshes the header.
            BgCutFoilsPicker.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(WizardItemPicker.SelectedItem)) return;
                if (_suppressBgCutEcho) return;
                if (CurrentMat == null) return;
                CurrentMat.FoilsItem = BgCutFoilsPicker.SelectedItem;
                OnPropertyChanged(nameof(CutFollowupHeader));
            };
        }

        // Mirrors the cutting method on the active piece, shaped for XAML highlight
        // bindings (StringEqualsConverter) to compare against each button's tag.
        public string CurrentBgCuttingMethod => CurrentMat?.CuttingMethod ?? string.Empty;

        // Single helper used everywhere CurrentBgCuttingMethod can change so the
        // follow-up visibility flags + the human-readable header stay in sync without
        // duplicate notifications scattered around. Also wipes the Foils side picker
        // when the method changes to one that doesn't permit it.
        private void NotifyCuttingMethodChanged()
        {
            // Clear Foils picker + field if the new method doesn't support it.
            if (CurrentMat != null && !ShowCutFoils)
            {
                _suppressBgCutEcho = true;
                BgCutFoilsPicker.SelectedItem = null;
                CurrentMat.FoilsItem = null;
                _suppressBgCutEcho = false;
            }

            OnPropertyChanged(nameof(CurrentBgCuttingMethod));
            OnPropertyChanged(nameof(ShowCutDieIndex));
            OnPropertyChanged(nameof(ShowCutLayers));
            OnPropertyChanged(nameof(ShowCutFoils));
            OnPropertyChanged(nameof(ShowAnyCutFollowups));
            OnPropertyChanged(nameof(CutFollowupHeader));
        }

        // Header shown at the top of the Cut details card so the user can confirm what
        // they actually clicked before answering "which die" / "how many layers" — e.g.
        // "All Planned Out — Birthday Bash" or "Stacklets — A2 Postage Stamp Stacklets".
        public string CutFollowupHeader
        {
            get
            {
                if (CurrentMat == null) return string.Empty;
                // Enumerate every pick on the active piece so the user can confirm
                // combinations (e.g. "Insider — X • Stacklets — Y • Foil: Z") before
                // answering layer/die-index questions. Mirrors DisplaySummary's logic
                // but without the cardstock prefix and Custom/None fallbacks.
                var bits = new List<string>();
                if (CurrentMat.StackletItem != null)   bits.Add($"Stacklets — {CurrentMat.StackletItem.Name}");
                if (CurrentMat.FramesItem != null)     bits.Add($"Frames — {CurrentMat.FramesItem.Name}");
                if (CurrentMat.PlannedOutItem != null) bits.Add($"All Planned Out — {CurrentMat.PlannedOutItem.Name}");
                if (CurrentMat.InsiderItem != null)    bits.Add($"Insider — {CurrentMat.InsiderItem.Name}");
                if (CurrentMat.FoilItItem != null)     bits.Add($"Foil-It — {CurrentMat.FoilItItem.Name}");
                if (CurrentMat.FoilsItem != null)      bits.Add($"Foil: {CurrentMat.FoilsItem.Name}");
                if (bits.Count == 0)
                    return string.IsNullOrEmpty(CurrentBgCuttingMethod) ? string.Empty : CurrentBgCuttingMethod;
                return string.Join("   •   ", bits);
            }
        }

        // ── How-Was-Cut follow-up visibility ─────────────────────────────────
        // Stacklets and Frames need both "which die in set" + "how many layers".
        // All Planned Out only needs "how many layers".
        public bool ShowCutDieIndex => CurrentBgCuttingMethod == "Stacklets"
                                       || CurrentBgCuttingMethod == "Frames";
        public bool ShowCutLayers   => CurrentBgCuttingMethod == "Stacklets"
                                       || CurrentBgCuttingMethod == "Frames"
                                       || CurrentBgCuttingMethod == "All Planned Out";

        // Foils dropdown stays visible as long as the user has an Insider OR Foil-It
        // pick on the active piece — regardless of whether the current CuttingMethod
        // (driven by the most recent cut-tool pick) has shifted to Stacklets / Frames /
        // All Planned Out. So picking Insider then a Stacklet keeps the Foils dropdown
        // available alongside the cut-tool follow-ups.
        public bool ShowCutFoils => CurrentMat?.InsiderItem != null
                                    || CurrentMat?.FoilItItem != null;

        public bool ShowAnyCutFollowups => ShowCutLayers || ShowCutFoils;

        [RelayCommand]
        private void SaveBgPieceHowCut()
        {
            if (CurrentMat == null) return;
            BgPieceHowCutSaved = !string.IsNullOrEmpty(CurrentMat.CuttingMethod);
            UpdateSummaryLines();
            BgMatHubStep = "Hub";
        }

        // ── BG Mat / Adhesives sub-page ───────────────────────────────────────
        // Mirrors Cardbase Adhesives but routes saves to CurrentMat.Adhesives.
        // Three independent picker instances per mat-context so navigating between
        // Cardbase Adhesives and BG Mat Adhesives doesn't bleed selections.
        public WizardItemPicker BgPieceGlueAdhesivePicker       { get; } = new() { PlaceholderText = "Glue" };
        public WizardItemPicker BgPieceFoamAdhesivePicker       { get; } = new() { PlaceholderText = "Foam" };
        public WizardItemPicker BgPieceTapeRunnerAdhesivePicker { get; } = new() { PlaceholderText = "Tape Runner" };

        public bool HasCurrentBgPieceAdhesivePick =>
            BgPieceGlueAdhesivePicker.SelectedItem != null
            || BgPieceFoamAdhesivePicker.SelectedItem != null
            || BgPieceTapeRunnerAdhesivePicker.SelectedItem != null;

        public string CurrentBgPieceAdhesivePreview
        {
            get
            {
                var parts = new List<string>();
                if (BgPieceGlueAdhesivePicker.SelectedItem != null)       parts.Add(BgPieceGlueAdhesivePicker.SelectedItem.Name);
                if (BgPieceFoamAdhesivePicker.SelectedItem != null)       parts.Add(BgPieceFoamAdhesivePicker.SelectedItem.Name);
                if (BgPieceTapeRunnerAdhesivePicker.SelectedItem != null) parts.Add(BgPieceTapeRunnerAdhesivePicker.SelectedItem.Name);
                return parts.Count == 0 ? string.Empty : string.Join("   •   ", parts);
            }
        }

        [RelayCommand]
        private void SaveBgPieceAdhesives()
        {
            if (CurrentMat == null) return;
            foreach (var picker in new[] { BgPieceGlueAdhesivePicker, BgPieceFoamAdhesivePicker, BgPieceTapeRunnerAdhesivePicker })
            {
                if (picker.SelectedItem != null && !CurrentMat.Adhesives.Contains(picker.SelectedItem.Name))
                    CurrentMat.Adhesives.Add(picker.SelectedItem.Name);
                picker.SelectedItem = null;
            }
            BgPieceAdhesivesSaved = CurrentMat.Adhesives.Count > 0;
            UpdateSummaryLines();
            BgMatHubStep = "Hub";
        }

        // ── BG Mat / Add Piece + Add Mat actions ──────────────────────────────
        // "Add 1 Piece of mat" commits the current in-progress piece into the
        // group's Pieces list, allocates a fresh CurrentMat, and resets per-piece
        // saved indicators. The user stays on the hub for the next piece.
        [RelayCommand]
        private void AddBgMatPiece()
        {
            if (CurrentMat == null || _currentBgMatGroup == null) return;
            if (HasAnyBgPieceConfiguration(CurrentMat))
            {
                CurrentMat.Layer = _currentBgMatGroup.Pieces.Count + 1;
                _currentBgMatGroup.Pieces.Add(CurrentMat);
            }
            // Allocate a fresh piece for the next iteration.
            CurrentMat = new WizardBgMat { Layer = _currentBgMatGroup.Pieces.Count + 1 };
            ResetBgPieceSavedFlags();
            ClearBgPiecePickers();
            UpdateSummaryLines();
        }

        // "Add Mat" finalizes the group, commits any in-progress piece, and
        // returns to the main wizard hub. Routes to BgMats or AdditionalMats based
        // on CurrentMatTarget.
        [RelayCommand]
        private void AddBgMat()
        {
            if (_currentBgMatGroup == null)
            {
                CurrentSection = "Hub";
                return;
            }
            if (CurrentMat != null && HasAnyBgPieceConfiguration(CurrentMat))
            {
                CurrentMat.Layer = _currentBgMatGroup.Pieces.Count + 1;
                _currentBgMatGroup.Pieces.Add(CurrentMat);
            }

            // Tag with the inside-mode flag so the summary can prefix "Inside " and
            // downstream reports know which side of the card this group belongs to.
            _currentBgMatGroup.IsInside = IsInsideMode;

            // Route the finalized group into the right collection.
            ObservableCollection<WizardBgMatGroup> target = CurrentMatTarget switch
            {
                "AdditionalMat" => AdditionalMats,
                "FocalMat"      => FocalMatGroups,
                _               => BgMats,
            };
            if (_currentBgMatGroup.Pieces.Count > 0 && !target.Contains(_currentBgMatGroup))
                target.Add(_currentBgMatGroup);

            switch (CurrentMatTarget)
            {
                case "AdditionalMat":
                    AdditionalMatSaved = AdditionalMats.Any(g => g.Pieces.Count > 0);
                    break;
                case "FocalMat":
                    FocalMatSaved = FocalMatGroups.Any(g => g.Pieces.Count > 0);
                    break;
                default:
                    BackgroundMatSaved = BgMats.Any(g => g.Pieces.Count > 0);
                    break;
            }

            _currentBgMatGroup = null;
            OnPropertyChanged(nameof(CurrentBgMatPieces));
            CurrentMat = null;
            ResetBgPieceSavedFlags();
            ClearBgPiecePickers();
            UpdateSummaryLines();

            BgMatHubStep = "Hub";
            CurrentSection = "Hub";
        }

        [RelayCommand]
        private void RemoveBgMatPiece(WizardBgMat? piece)
        {
            if (piece == null || _currentBgMatGroup == null) return;
            _currentBgMatGroup.Pieces.Remove(piece);
            // Re-number remaining pieces.
            for (int i = 0; i < _currentBgMatGroup.Pieces.Count; i++)
                _currentBgMatGroup.Pieces[i].Layer = i + 1;
            UpdateSummaryLines();
        }

        private static bool HasAnyBgPieceConfiguration(WizardBgMat m)
        {
            return !string.IsNullOrEmpty(m.SelectedCardstockColor)
                || m.IsSelfBlended
                || !string.IsNullOrEmpty(m.CuttingMethod) && m.CuttingMethod != "None"
                || m.AddedDetails.Count > 0
                || m.Adhesives.Count > 0;
        }

        private void ResetBgPieceSavedFlags()
        {
            BgPieceCardstockSaved = false;
            BgPieceHowCutSaved    = false;
            BgPieceDetailsSaved   = false;
            BgPieceAdhesivesSaved = false;
        }

        private void ClearBgPiecePickers()
        {
            BgPieceCardstockPicker.SelectedItem          = null;
            BgPieceFoilCardstockPicker.SelectedItem      = null;
            BgPieceGlitterCardstockPicker.SelectedItem   = null;
            BgPieceGlueAdhesivePicker.SelectedItem       = null;
            BgPieceFoamAdhesivePicker.SelectedItem       = null;
            BgPieceTapeRunnerAdhesivePicker.SelectedItem = null;
            BgPieceBlendInks.Clear();
            // Cutting-method pickers — also wipe so the new piece starts with no method.
            _suppressBgCutEcho = true;
            ClearAllBgCutPickers();
            BgCutFoilsPicker.SelectedItem = null;
            _suppressBgCutEcho = false;
        }

        // Generalized "Details panel is currently visible" gate. Used by the XAML
        // visibility binding so a single Details panel serves multiple parents
        // (Cardbase today, BackgroundMat now, Additional/Focal in later phases).
        // Alternative considered: replicate the Details panel per parent context;
        // rejected — the panel is ~500 lines and would 3x the XAML.
        public bool IsDetailsStepActive =>
            (IsCardBaseSectionActive && IsCardBaseDetailsStep)
            || (IsBackgroundMatSectionActive && IsBgMatDetailsStep)
            || (IsAdditionalMatSectionActive && IsBgMatDetailsStep)
            || (IsFocalMatSectionActive && IsBgMatDetailsStep)
            || (IsSentimentSectionActive && IsSentimentSubStepDetails)
            || IsInsideDetailsSectionActive;

        // Routes the Details panel's Back button back to the right parent hub.
        // Alternative considered: pass DetailsReturnTarget as a CommandParameter
        // and resolve in XAML; rejected — VM logic stays centralized here.
        [RelayCommand]
        private void BackFromDetails()
        {
            switch (DetailsReturnTarget)
            {
                case "CardBase":       CurrentCardBaseStep = "Hub"; break;
                case "BackgroundMat":  BgMatHubStep        = "Hub"; break;
                case "AdditionalMat":  BgMatHubStep        = "Hub"; break;
                case "FocalMat":       BgMatHubStep        = "Hub"; break;
                case "Sentiment":      SentimentSubStep    = "Hub"; break;
                case "InsideMisc":     CurrentSection      = "Hub"; break;
                default:
                    CurrentCardBaseStep = "Hub";
                    CurrentSection      = "Hub";
                    break;
            }
        }

        [RelayCommand] private void NavToBackgroundMat()
        {
            CurrentMatTarget = "BackgroundMat";
            CurrentSection = "BackgroundMat";
            // Start a fresh group + first piece every time the user enters BG Mat.
            _currentBgMatGroup = new WizardBgMatGroup
            {
                GroupNumber = BgMats.Count + 1,
                TypeLabel = "Background",
                // Capture inside-mode at creation so the prefix stays correct even
                // if the user toggles inside-mode after they enter this hub.
                IsInside = IsInsideMode
            };
            OnPropertyChanged(nameof(CurrentBgMatPieces));
            CurrentMat = new WizardBgMat { Layer = 1 };
            ResetBgPieceSavedFlags();
            ClearBgPiecePickers();
            BgMatHubStep = "Hub";
        }

        // Additional Mat reuses the BG mat hub layout — same pickers, same Cardstock /
        // How-Was-Cut / Details / Adhesives sub-pages. The only difference is the
        // collection the finalized group lands in (AdditionalMats vs BgMats), the
        // section label, and the back-button text — all driven by CurrentMatTarget.
        [RelayCommand]
        private void NavToAdditionalMat()
        {
            CurrentMatTarget = "AdditionalMat";
            CurrentSection = "AdditionalMat";
            _currentBgMatGroup = new WizardBgMatGroup
            {
                GroupNumber = AdditionalMats.Count + 1,
                TypeLabel = "Additional",
                IsInside = IsInsideMode
            };
            OnPropertyChanged(nameof(CurrentBgMatPieces));
            CurrentMat = new WizardBgMat { Layer = 1 };
            ResetBgPieceSavedFlags();
            ClearBgPiecePickers();
            BgMatHubStep = "Hub";
        }

        // Focal Mat reuses the same hub. One synthetic WizardBgMatGroup represents
        // the focal section; each piece in that group is a focal "part" (relabel done
        // via MatPieceActionLabel). Backer is intentionally absent — the user spec
        // dropped the backer section for the new hub. Committed group lands in
        // FocalMatGroups (typically a single group per card).
        public ObservableCollection<WizardBgMatGroup> FocalMatGroups { get; } = new();

        [RelayCommand]
        private void NavToFocalMat()
        {
            CurrentMatTarget = "FocalMat";
            CurrentSection = "FocalMat";
            _currentBgMatGroup = new WizardBgMatGroup
            {
                GroupNumber = FocalMatGroups.Count + 1,
                TypeLabel = "Focal",
                IsInside = IsInsideMode
            };
            OnPropertyChanged(nameof(CurrentBgMatPieces));
            CurrentMat = new WizardBgMat { Layer = 1 };
            ResetBgPieceSavedFlags();
            ClearBgPiecePickers();
            BgMatHubStep = "Hub";
        }
        [RelayCommand] private void NavToSentiment()     => CurrentSection = "Sentiment";
        [RelayCommand] private void NavToEmbellishments() => CurrentSection = "Embellishments";
        // ── Inside-of-Card mode ──────────────────────────────────────────────
        // Toggles a hub-wide flag that:
        //   • shows a banner at the top of the hub ("Inside of Card"),
        //   • hides the Cardbase + Envelopes buttons (those are outside-only),
        //   • suppresses every "- Done!" suffix so the inside hub reads fresh,
        //   • tags any group / sentiment / embellishment saved while active as
        //     IsInside = true so the summary can prefix "Inside " on those rows.
        // The "Build Inside of Card" hub button toggles the flag — same button
        // becomes "Done with Inside" while active so there's a clear way out.
        [ObservableProperty] private bool _isInsideMode;

        [RelayCommand]
        private void ToggleInsideMode()
        {
            IsInsideMode = !IsInsideMode;
            // Make sure we're on the hub — if the user happened to be on a sub-page
            // when they hit the toggle, jump back to the hub so the banner shows.
            CurrentSection = "Hub";
        }
        [RelayCommand]
        private void BackToHub()
        {
            CurrentSection = "Hub";
            UpdateSummaryLines();
        }

        // ── Inside hub: Cardstock (inside liner) ─────────────────────────────
        // The cardstock layered onto the inside of the card (where the message
        // goes). Distinct from Cardbase (outside-only). Single picker, no
        // foil/glitter sub-types for now — kept simple, extensible later.
        public WizardItemPicker InsideLinerCardstockPicker { get; } =
            new() { PlaceholderText = "Cardstock for inside" };

        [ObservableProperty] private WizardItemOption? _selectedInsideLinerCardstockItem;
        [ObservableProperty] private string? _selectedInsideLinerCardstockColor;
        [ObservableProperty] private bool _insideCardstockSaved;

        [RelayCommand]
        private void NavToInsideCardstock()
        {
            CurrentSection = "InsideCardstock";
        }

        [RelayCommand]
        private void SaveInsideCardstockAndBackToHub()
        {
            SelectedInsideLinerCardstockItem = InsideLinerCardstockPicker.SelectedItem;
            SelectedInsideLinerCardstockColor = InsideLinerCardstockPicker.SelectedItem?.Name;
            InsideCardstockSaved = SelectedInsideLinerCardstockItem != null;
            CurrentSection = "Hub";
            UpdateSummaryLines();
        }

        // ── Inside hub: Details (shared sub-page dispatch) ───────────────────
        // Reuses the existing top-level Details picker grid (stamps, dies,
        // embossing folders, etc.) via DetailsReturnTarget = "InsideMisc".
        // Picks committed here land in InsideMiscDetails on the snapshot.
        [ObservableProperty] private bool _insideDetailsSaved;

        public ObservableCollection<WizardDetailEntry> InsideMiscDetails { get; } = new();

        [RelayCommand]
        private void NavToInsideDetails()
        {
            DetailsReturnTarget = "InsideMisc";
            CurrentSection = "InsideDetails";
        }

        // ── Project image (passed in from caller) ─────────────────────────────
        [ObservableProperty] private string? _projectImageBase64;
        public bool HasProjectImage => !string.IsNullOrEmpty(ProjectImageBase64);
        partial void OnProjectImageBase64Changed(string? value) => OnPropertyChanged(nameof(HasProjectImage));

        [RelayCommand]
        private void UploadProjectImage()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select project image",
                Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp;*.heic;*.heif|All files|*.*"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                // Same path the project page uses: resize + JPEG-encode + return as data-URI
                // so the Base64ImageConverter renders it correctly.
                var dataUri = MyCraftyStash.Services.ImageLoadService.LoadAsDataUri(dlg.FileName);
                if (string.IsNullOrEmpty(dataUri)) return;
                ProjectImageBase64 = dataUri;
                // Mirror the upload into the project page's image list so both views stay in sync.
                _onImageAddedToProject?.Invoke(dataUri);
            }
            catch (Exception ex)
            {
                MyCraftyStash.Services.LoggingService.LogError(ex, "Failed to load project image in wizard");
            }
        }

        // ── Aggregated summary for the right-side panel ───────────────────────
        // Each row carries text + an optional RemoveAction. When RemoveAction is non-null,
        // the row renders a ✕ button that calls back into the source collection so the
        // user can delete a duplicate or accidental entry without rebuilding from scratch.
        public ObservableCollection<SummaryRow> SummaryLines { get; } = new();

        [RelayCommand]
        private void RemoveSummaryRow(SummaryRow? row)
        {
            // Each removable row owns the side-effect; we just invoke it and re-render.
            // Source-collection ownership stays with whoever added the row to the summary.
            row?.RemoveAction?.Invoke();
            UpdateSummaryLines();
        }

        public void UpdateSummaryLines()
        {
            SummaryLines.Clear();

            // Helper closures to keep the build below readable.
            void AddInfo(string text) => SummaryLines.Add(new SummaryRow { Text = text });
            void AddRemovable(string text, Action remove) =>
                SummaryLines.Add(new SummaryRow { Text = text, RemoveAction = remove });

            if (!string.IsNullOrEmpty(SelectedCardBase))
            {
                var line = string.IsNullOrEmpty(EffectiveBaseCardstockColor)
                    ? $"Cardbase: {SelectedCardBase}"
                    : $"Cardbase: {SelectedCardBase} in {EffectiveBaseCardstockColor}";
                // Weave self-blend description + inks into the summary so the user
                // can confirm the blend recipe at a glance. Mirrors the same shape
                // used by mat / sentiment self-blend lines elsewhere.
                if (BaseIsSelfBlended)
                {
                    var bits = new List<string>();
                    if (!string.IsNullOrWhiteSpace(BaseSelfBlendDescription))
                        bits.Add(BaseSelfBlendDescription);
                    if (BaseBlendInks.Ordered.Count > 0)
                        bits.Add($"inks: {string.Join(", ", BaseBlendInks.Ordered)}");
                    line += bits.Count == 0
                        ? " (self-blended)"
                        : $" (self-blended — {string.Join("; ", bits)})";
                }
                AddInfo(line);
            }
            foreach (var d in CardBase.Decorations)
            {
                var captured = d;
                AddRemovable($"  • {captured.DisplaySummary}", () => CardBase.Decorations.Remove(captured));
            }

            // Details captured on the cardbase Details sub-page (each one is a snapshot
            // of all 8 picker selections). Numbered for clarity. Removable so an extra
            // accidental entry can be deleted from the summary directly.
            for (int i = 0; i < CardBaseAddedDetails.Count; i++)
            {
                var captured = CardBaseAddedDetails[i];
                AddRemovable($"  Detail {i + 1}: {captured.DisplaySummary}",
                    () => CardBaseAddedDetails.Remove(captured));
            }

            foreach (var a in CardBaseAddedAdhesives)
            {
                var captured = a;
                AddRemovable($"  Adhesive: {captured.Name}", () => CardBaseAddedAdhesives.Remove(captured));
            }

            // Render each mat group + its per-piece detail entries. Same template
            // for outside and inside passes; the section header ("Inside of Card")
            // separates them visually so we don't need an "Inside " prefix per item.
            void RenderMatGroup(WizardBgMatGroup group, string label, string pieceWord,
                                ObservableCollection<WizardBgMatGroup> ownerCollection)
            {
                var capturedGroup = group;
                AddRemovable($"{label} {capturedGroup.GroupNumber}: {capturedGroup.DisplaySummary}",
                    () => ownerCollection.Remove(capturedGroup));
                foreach (var p in group.Pieces)
                {
                    var capturedPiece = p;
                    foreach (var d in p.AddedDetails)
                    {
                        var capturedDetail = d;
                        AddRemovable($"  {pieceWord} {capturedPiece.Layer} detail: {capturedDetail.DisplaySummary}",
                            () => capturedPiece.AddedDetails.Remove(capturedDetail));
                    }
                }
            }

            // ── Outside (main) section ──────────────────────────────────────────
            foreach (var group in BgMats.Where(g => !g.IsInside))
                RenderMatGroup(group, "Background Mat", "Piece", BgMats);
            foreach (var group in AdditionalMats.Where(g => !g.IsInside))
                RenderMatGroup(group, "Additional Mat", "Piece", AdditionalMats);
            foreach (var group in FocalMatGroups.Where(g => !g.IsInside))
                RenderMatGroup(group, "Focal Mat", "Part", FocalMatGroups);

            // Legacy FocalParts (WizardFocalSection) — kept for any old data that may
            // still flow through the legacy in-line form. New hub doesn't write here.
            foreach (var fp in FocalParts)
            {
                var capturedFp = fp;
                AddRemovable($"Focal Mat: {capturedFp.DisplaySummary}", () => FocalParts.Remove(capturedFp));
                foreach (var d in capturedFp.AddedDetails)
                {
                    var capturedDetail = d;
                    AddRemovable($"  Detail: {capturedDetail.DisplaySummary}",
                        () => capturedFp.AddedDetails.Remove(capturedDetail));
                }
            }

            foreach (var s in ConfiguredSentiments.Where(s => !s.IsInside))
            {
                var capturedSent = s;
                AddRemovable($"Sentiment: {capturedSent.DisplaySummary}", () => ConfiguredSentiments.Remove(capturedSent));
                // Surface each per-part captured detail so the user sees their
                // per-sentiment Details sub-page picks before saving.
                foreach (var p in capturedSent.Parts)
                    foreach (var d in p.AddedDetails)
                    {
                        var capturedPart = p;
                        var capturedDetail = d;
                        AddRemovable($"  Sentiment detail: {capturedDetail.DisplaySummary}",
                            () => capturedPart.AddedDetails.Remove(capturedDetail));
                    }
            }

            foreach (var e in AddedEmbellishments.Where(e => !e.IsInside))
            {
                var captured = e;
                AddRemovable($"Embellishment: {captured.DisplaySummary}", () => AddedEmbellishments.Remove(captured));
            }

            // ── Inside of Card section (positioned after the main content but
            // before the envelope so the user can see exactly what's on each side
            // of the card). Header acts as the section divider. ───────────────
            bool hasAnyInside =
                BgMats.Any(g => g.IsInside) ||
                AdditionalMats.Any(g => g.IsInside) ||
                FocalMatGroups.Any(g => g.IsInside) ||
                InsideBgMats.Count > 0 ||
                InsideAdditionalMats.Count > 0 ||
                HasInsideFocalMat ||
                ConfiguredSentiments.Any(s => s.IsInside) ||
                ConfiguredInsideSentiments.Count > 0 ||
                AddedEmbellishments.Any(e => e.IsInside) ||
                InsideAddedEmbellishments.Count > 0;

            if (hasAnyInside)
            {
                AddInfo("── Inside of Card ──");

                foreach (var group in BgMats.Where(g => g.IsInside))
                    RenderMatGroup(group, "Background Mat", "Piece", BgMats);
                // Legacy direct-collection inside mats (older inside flow that wrote
                // straight into InsideBgMats / InsideAdditionalMats instead of tagging
                // a WizardBgMatGroup with IsInside).
                foreach (var mat in InsideBgMats)
                {
                    var capturedMat = mat;
                    AddRemovable($"Background Mat {capturedMat.Layer}: {capturedMat.DisplaySummary}",
                        () => InsideBgMats.Remove(capturedMat));
                    foreach (var d in capturedMat.AddedDetails)
                    {
                        var capturedDetail = d;
                        AddRemovable($"  Piece {capturedMat.Layer} detail: {capturedDetail.DisplaySummary}",
                            () => capturedMat.AddedDetails.Remove(capturedDetail));
                    }
                }

                foreach (var group in AdditionalMats.Where(g => g.IsInside))
                    RenderMatGroup(group, "Additional Mat", "Piece", AdditionalMats);
                foreach (var mat in InsideAdditionalMats)
                {
                    var capturedMat = mat;
                    AddRemovable($"Additional Mat {capturedMat.Layer}: {capturedMat.DisplaySummary}",
                        () => InsideAdditionalMats.Remove(capturedMat));
                    foreach (var d in capturedMat.AddedDetails)
                    {
                        var capturedDetail = d;
                        AddRemovable($"  Piece {capturedMat.Layer} detail: {capturedDetail.DisplaySummary}",
                            () => capturedMat.AddedDetails.Remove(capturedDetail));
                    }
                }

                foreach (var group in FocalMatGroups.Where(g => g.IsInside))
                    RenderMatGroup(group, "Focal Mat", "Part", FocalMatGroups);
                if (HasInsideFocalMat)
                {
                    AddInfo($"Focal Mat: {InsideFocal.DisplaySummary}");
                    foreach (var d in InsideFocal.AddedDetails)
                    {
                        var capturedDetail = d;
                        AddRemovable($"  Inside focal detail: {capturedDetail.DisplaySummary}",
                            () => InsideFocal.AddedDetails.Remove(capturedDetail));
                    }
                }

                foreach (var s in ConfiguredSentiments.Where(s => s.IsInside))
                {
                    var capturedSent = s;
                    AddRemovable($"Sentiment: {capturedSent.DisplaySummary}", () => ConfiguredSentiments.Remove(capturedSent));
                    foreach (var p in capturedSent.Parts)
                        foreach (var d in p.AddedDetails)
                        {
                            var capturedPart = p;
                            var capturedDetail = d;
                            AddRemovable($"  Sentiment detail: {capturedDetail.DisplaySummary}",
                                () => capturedPart.AddedDetails.Remove(capturedDetail));
                        }
                }
                foreach (var s in ConfiguredInsideSentiments)
                {
                    var capturedSent = s;
                    AddRemovable($"Sentiment: {capturedSent.DisplaySummary}",
                        () => ConfiguredInsideSentiments.Remove(capturedSent));
                    foreach (var p in capturedSent.Parts)
                        foreach (var d in p.AddedDetails)
                        {
                            var capturedPart = p;
                            var capturedDetail = d;
                            AddRemovable($"  Sentiment detail: {capturedDetail.DisplaySummary}",
                                () => capturedPart.AddedDetails.Remove(capturedDetail));
                        }
                }

                foreach (var e in AddedEmbellishments.Where(e => e.IsInside))
                {
                    var captured = e;
                    AddRemovable($"Embellishment: {captured.DisplaySummary}", () => AddedEmbellishments.Remove(captured));
                }
                foreach (var e in InsideAddedEmbellishments)
                {
                    var captured = e;
                    AddRemovable($"Embellishment: {captured.DisplaySummary}",
                        () => InsideAddedEmbellishments.Remove(captured));
                }
            }

            // ── Envelope / storage (always last) ────────────────────────────────
            if (SelectedEnvelopeItem != null)
                AddInfo($"Envelope: {SelectedEnvelopeItem.Name}");
            if (SelectedStorageBagItem != null)
                AddInfo($"Storage Bag: {SelectedStorageBagItem.Name}");
        }

        // ── Focal mat inline state (lives inside the Mats section) ───────────
        [ObservableProperty] private bool _isAddingFocalMat;
        [ObservableProperty] private bool _showAddAnotherFocalPartPrompt;

        public ObservableCollection<WizardFocalSection> FocalParts { get; } = new();

        public bool FocalMatConfirmed => FocalParts.Count > 0;
        public bool HasFocalParts => FocalParts.Count > 0;
        public bool ShowFocalAddButton => !IsAddingFocalMat && !ShowAddAnotherFocalPartPrompt;

        private void OnFocalPartsChanged()
        {
            OnPropertyChanged(nameof(FocalMatConfirmed));
            OnPropertyChanged(nameof(HasFocalParts));
            OnPropertyChanged(nameof(FocalMatDisplaySummary));
        }

        partial void OnShowAddAnotherFocalPartPromptChanged(bool _)
        {
            OnPropertyChanged(nameof(ShowFocalAddButton));
            OnPropertyChanged(nameof(CanConfirmSection3));
        }

        // True when no mat form is open - gates the Section 3 Confirm button
        public bool CanConfirmSection3 => !IsAddingBgMat && !IsAddingAdditionalMat && !IsAddingFocalMat
            && !ShowAddAnotherFocalPartPrompt && !ShowAddAnotherBgMatPiecePrompt && !ShowAddAnotherAdditionalMatPiecePrompt;

        partial void OnIsAddingBgMatChanged(bool _) => OnPropertyChanged(nameof(CanConfirmSection3));
        partial void OnIsAddingAdditionalMatChanged(bool _) => OnPropertyChanged(nameof(CanConfirmSection3));
        partial void OnShowAddAnotherBgMatPiecePromptChanged(bool _) => OnPropertyChanged(nameof(CanConfirmSection3));
        partial void OnShowAddAnotherAdditionalMatPiecePromptChanged(bool _) => OnPropertyChanged(nameof(CanConfirmSection3));
        partial void OnIsAddingFocalMatChanged(bool _)
        {
            OnPropertyChanged(nameof(CanConfirmSection3));
            OnPropertyChanged(nameof(ShowFocalAddButton));
        }

        public int FocalMatNumber => 1;
        public string FocalMatDisplaySummary => FocalParts.Count == 0
            ? string.Empty
            : $"Focal Mat {FocalMatNumber}:\n" + string.Join("\n", FocalParts.Select((p, i) => $"Piece {i + 1}: {p.DisplaySummary}"));

        // ── Section 1: Card Base ──────────────────────────────────────────────
        public List<string> CardBaseOptions { get; } = new()
        {
            "A2 Side Fold", "A2 Top Fold", "A7 Top Fold", "A7 Side Fold",
            "Mini Slim Top Fold", "Mini Slim Side Fold", "Fun Fold"
        };
        [ObservableProperty] private string _selectedCardBase = string.Empty;
        public bool HasSelectedCardBase => !string.IsNullOrEmpty(SelectedCardBase);
        partial void OnSelectedCardBaseChanged(string value)
        {
            OnPropertyChanged(nameof(HasSelectedCardBase));
            // Auto-init cardstock color when a base is first chosen
            if (!string.IsNullOrEmpty(value) && SelectedBaseCardstockColor == null)
                SelectedBaseCardstockColor = CardstockColorOptions.FirstOrDefault();
        }

        // ── Section 2: Card Base Cardstock color ──────────────────────────────
        [ObservableProperty] private string? _selectedBaseCardstockColor;
        [ObservableProperty] private string _baseCardstockOtherText = string.Empty;
        public string EffectiveBaseCardstockColor =>
            SelectedBaseCardstockColor == "Other" ? BaseCardstockOtherText : SelectedBaseCardstockColor ?? string.Empty;
        partial void OnSelectedBaseCardstockColorChanged(string? v) => OnPropertyChanged(nameof(EffectiveBaseCardstockColor));
        partial void OnBaseCardstockOtherTextChanged(string v) => OnPropertyChanged(nameof(EffectiveBaseCardstockColor));

        // Cardbase / Cardstock self-blend (mirrors BG Mat / Sentiment self-blend pattern):
        // checkbox toggles a description textbox + an InkMultiSelectControl. Inks are
        // captured via BaseBlendInks below and contribute to CollectAllItemIds at build time.
        [ObservableProperty] private bool _baseIsSelfBlended;
        [ObservableProperty] private string _baseSelfBlendDescription = string.Empty;
        public InkSelection BaseBlendInks { get; } = new();

        // Card Base Extra Details
        [ObservableProperty] private bool _isAddingCardBaseDecoration;
        public WizardFocalSection CardBase { get; } = new();

        public bool CardBaseDecorationActive => CardBase.HasDecoration || (PromptAddMoreDecoration && IsAddingCardBaseDecoration);
        public bool CardBaseExtraDetailsDone => !CardBaseDecorationActive && CardBase.Decorations.Count > 0;
        public bool CardBaseConfirmEnabled   => !CardBaseDecorationActive;

        public string CardBaseDecorationsSummary =>
            CardBase.Decorations.Count == 0
                ? string.Empty
                : string.Join(" + ", CardBase.Decorations.Select(d => d.DisplaySummary));

        partial void OnIsAddingCardBaseDecorationChanged(bool _)
        {
            OnPropertyChanged(nameof(CardBaseDecorationActive));
            OnPropertyChanged(nameof(CardBaseConfirmEnabled));
            NotifyExtraDetailsDone();
        }

        [RelayCommand]
        private void StartCardBaseDecoration()
        {
            ResetDecorationForm();
            IsAddingCardBaseDecoration = true;
            CardBase.HasDecoration = true;
            OnPropertyChanged(nameof(CardBaseDecorationActive));
            OnPropertyChanged(nameof(CardBaseConfirmEnabled));
            NotifyExtraDetailsDone();
        }

        // ── Section 3: Background Mats ────────────────────────────────────────
        [ObservableProperty] private ObservableCollection<WizardBgMatGroup> _bgMats = new();
        [ObservableProperty] private bool _isAddingBgMat;
        [ObservableProperty] private bool _showAddAnotherBgMatPiecePrompt;
        [ObservableProperty] private WizardBgMat? _currentMat;
        private WizardBgMatGroup? _currentBgMatGroup;

        // Current mat dropdowns (loaded fresh for each mat)
        [ObservableProperty] private ObservableCollection<WizardItemOption> _plannedOutItems = new();
        [ObservableProperty] private ObservableCollection<WizardItemOption> _stackletItems = new();
        [ObservableProperty] private ObservableCollection<SubtypeCheckboxItem> _stackletSubtypeFilters = new();
        public bool HasStackletSubtypeFilters => StackletSubtypeFilters.Count > 0;
        [ObservableProperty] private ObservableCollection<WizardItemOption> _insiderItems = new();
        [ObservableProperty] private ObservableCollection<string> _insiderSentiments = new();

        // Decoration picker - type → subtypes → item, shared across mat forms
        [ObservableProperty] private ObservableCollection<string> _decorationTypeOptions = new();
        // Nullable: WPF ComboBox needs `null` (not "") to visually clear when binding TwoWay against ItemsSource that doesn't contain "".
        [ObservableProperty] private string? _selectedDecorationItemType;
        [ObservableProperty] private ObservableCollection<SubtypeCheckboxItem> _decorationSubtypeFilters = new();
        [ObservableProperty] private ObservableCollection<WizardItemOption> _decorationItems = new();
        public bool HasDecorationSubtypeFilters => DecorationSubtypeFilters.Count > 0;
        private List<WizardItemOption> _allDecorationItems = new();
        private CancellationTokenSource? _decorationLoadCts;
        private CancellationTokenSource? _decorationStampLoadCts;
        private bool _suppressDecorationLoad;

        // "Add another decoration?" prompt shown after ConfirmCurrentDecoration
        [ObservableProperty] private bool _promptAddMoreDecoration;

        // Computed visibility helpers: true = decoration form or "add another?" prompt is active → hide "Add Decoration" button and "Add Mat" button
        public bool BgMatDecorationActive         => (CurrentMat?.HasDecoration ?? false) || (PromptAddMoreDecoration && CurrentMat != null);
        public bool AdditionalMatDecorationActive  => (CurrentAdditionalMat?.HasDecoration ?? false) || (PromptAddMoreDecoration && CurrentAdditionalMat != null);
        public bool InsideBgMatDecorationActive          => (CurrentInsideMat?.HasDecoration ?? false) || (PromptAddMoreDecoration && CurrentInsideMat != null);
        public bool InsideAdditionalMatDecorationActive  => (CurrentInsideAdditionalMat?.HasDecoration ?? false) || (PromptAddMoreDecoration && CurrentInsideAdditionalMat != null);
        // Combined: true in either mat context - used by shared XAML blocks
        public bool MatDecorationActive => BgMatDecorationActive || AdditionalMatDecorationActive;
        public bool InsideMatDecorationActive => InsideBgMatDecorationActive || InsideAdditionalMatDecorationActive;
        public bool ExteriorFocalDecorationActive  => ExteriorFocal.HasDecoration || (PromptAddMoreDecoration && IsAddingFocalMat);
        public bool InsideFocalMatDecorationActive => InsideFocal.HasDecoration || (PromptAddMoreDecoration && IsAddingInsideFocalMat);
        public bool SentimentDecorationModeActive  => SentimentHasDecoration || (PromptAddMoreDecoration && IsConfiguringCurrentSentiment);
        public bool InsideFocalDecorationActive    => InsideFocal.HasDecoration || (PromptAddMoreDecoration && !IsAddingFocalMat && !IsConfiguringCurrentSentiment && !IsAddingCardBaseDecoration && !IsAddingInsideFocalMat && CurrentMat == null && CurrentAdditionalMat == null && CurrentInsideMat == null && CurrentInsideAdditionalMat == null);

        // True when the user has confirmed at least one decoration and dismissed the "add another?" prompt
        public bool BgMatExtraDetailsDone         => !BgMatDecorationActive && (CurrentMat?.Decorations.Count ?? 0) > 0;
        public bool AdditionalMatExtraDetailsDone => !AdditionalMatDecorationActive && (CurrentAdditionalMat?.Decorations.Count ?? 0) > 0;
        public bool InsideBgMatExtraDetailsDone         => !InsideBgMatDecorationActive && (CurrentInsideMat?.Decorations.Count ?? 0) > 0;
        public bool InsideAdditionalMatExtraDetailsDone => !InsideAdditionalMatDecorationActive && (CurrentInsideAdditionalMat?.Decorations.Count ?? 0) > 0;
        public bool FocalMatExtraDetailsDone      => !ExteriorFocalDecorationActive && ExteriorFocal.Decorations.Count > 0;
        public bool InsideFocalMatExtraDetailsDone => !InsideFocalMatDecorationActive && InsideFocal.Decorations.Count > 0;
        public bool SentimentExtraDetailsDone     => !SentimentDecorationModeActive && CurrentSentimentDecorations.Count > 0;
        public bool InsideFocalExtraDetailsDone   => !InsideFocalDecorationActive && InsideFocal.Decorations.Count > 0;

        // True when a custom-color picker is open in any mat form - gates other buttons and hides adhesive
        public bool CustomColorsOpen    => ShowBlendInkList || ShowSentimentBlendInkList;
        public bool CustomColorsNotOpen => !CustomColorsOpen;

        // Per-mat adhesive visibility - hides only while a decoration form or custom-color picker is actively open.
        // (We deliberately do NOT hide once at least one extra detail is confirmed - the user still needs to pick how the mat is attached.)
        public bool BgMatAdhesiveVisible         => !BgMatDecorationActive && !ShowBlendInkList;
        public bool AdditionalMatAdhesiveVisible => !AdditionalMatDecorationActive && !ShowBlendInkList;
        public bool InsideBgMatAdhesiveVisible          => !InsideBgMatDecorationActive && !ShowBlendInkList;
        public bool InsideAdditionalMatAdhesiveVisible  => !InsideAdditionalMatDecorationActive && !ShowBlendInkList;
        public bool InsideFocalMatAdhesiveVisible       => !InsideFocalMatDecorationActive && !ShowBlendInkList;
        public bool FocalMatAdhesiveVisible      => !ExteriorFocalDecorationActive && !ShowBlendInkList;
        public bool SentimentAdhesiveVisible     => !SentimentDecorationModeActive && !ShowSentimentBlendInkList;
        public bool InsideFocalAdhesiveVisible   => !ShowBlendInkList;

        // Disables the Confirm Piece / Cancel buttons in the sentiment section while the extra-detail form is open
        public bool SentimentControlsEnabled     => !SentimentDecorationModeActive;

        private void NotifyExtraDetailsDone()
        {
            OnPropertyChanged(nameof(BgMatExtraDetailsDone));
            OnPropertyChanged(nameof(AdditionalMatExtraDetailsDone));
            OnPropertyChanged(nameof(InsideBgMatExtraDetailsDone));
            OnPropertyChanged(nameof(InsideAdditionalMatExtraDetailsDone));
            OnPropertyChanged(nameof(FocalMatExtraDetailsDone));
            OnPropertyChanged(nameof(InsideFocalMatExtraDetailsDone));
            OnPropertyChanged(nameof(SentimentExtraDetailsDone));
            OnPropertyChanged(nameof(InsideFocalExtraDetailsDone));
            OnPropertyChanged(nameof(CardBaseExtraDetailsDone));
            OnPropertyChanged(nameof(CardBaseDecorationActive));
            OnPropertyChanged(nameof(CardBaseConfirmEnabled));
            OnPropertyChanged(nameof(CardBaseDecorationsSummary));
            OnPropertyChanged(nameof(BgMatAdhesiveVisible));
            OnPropertyChanged(nameof(AdditionalMatAdhesiveVisible));
            OnPropertyChanged(nameof(InsideBgMatAdhesiveVisible));
            OnPropertyChanged(nameof(InsideAdditionalMatAdhesiveVisible));
            OnPropertyChanged(nameof(InsideFocalMatAdhesiveVisible));
            OnPropertyChanged(nameof(FocalMatAdhesiveVisible));
            OnPropertyChanged(nameof(SentimentAdhesiveVisible));
            OnPropertyChanged(nameof(InsideFocalAdhesiveVisible));
        }

        private void NotifyCustomColorsChanged()
        {
            OnPropertyChanged(nameof(CustomColorsOpen));
            OnPropertyChanged(nameof(CustomColorsNotOpen));
            OnPropertyChanged(nameof(BgMatAdhesiveVisible));
            OnPropertyChanged(nameof(AdditionalMatAdhesiveVisible));
            OnPropertyChanged(nameof(InsideBgMatAdhesiveVisible));
            OnPropertyChanged(nameof(InsideAdditionalMatAdhesiveVisible));
            OnPropertyChanged(nameof(InsideFocalMatAdhesiveVisible));
            OnPropertyChanged(nameof(FocalMatAdhesiveVisible));
            OnPropertyChanged(nameof(SentimentAdhesiveVisible));
            OnPropertyChanged(nameof(InsideFocalAdhesiveVisible));
        }

        partial void OnPromptAddMoreDecorationChanged(bool _)
        {
            OnPropertyChanged(nameof(BgMatDecorationActive));
            OnPropertyChanged(nameof(AdditionalMatDecorationActive));
            OnPropertyChanged(nameof(InsideBgMatDecorationActive));
            OnPropertyChanged(nameof(InsideAdditionalMatDecorationActive));
            OnPropertyChanged(nameof(MatDecorationActive));
            OnPropertyChanged(nameof(InsideMatDecorationActive));
            OnPropertyChanged(nameof(ExteriorFocalDecorationActive));
            OnPropertyChanged(nameof(InsideFocalMatDecorationActive));
            OnPropertyChanged(nameof(SentimentDecorationModeActive));
            OnPropertyChanged(nameof(SentimentControlsEnabled));
            OnPropertyChanged(nameof(InsideFocalDecorationActive));
            OnPropertyChanged(nameof(CardBaseDecorationActive));
            OnPropertyChanged(nameof(CardBaseConfirmEnabled));
            NotifyExtraDetailsDone();
        }

        // Disables the "Confirm Inside" / "Cancel" buttons at the bottom of Section 7 whenever
        // any inside subform (mat, focal, embellishment, or sentiment configuration) is open.
        public bool InsideSectionConfirmEnabled =>
            !IsAddingInsideBgMat
            && !IsAddingInsideAdditionalMat
            && !IsAddingInsideFocalMat
            && !IsAddingInsideEmbellishment
            && !IsConfiguringCurrentInsideSentiment;

        // Refresh derived flags when the wrapper "is adding" toggles flip
        partial void OnIsAddingInsideFocalMatChanged(bool _)   { OnPropertyChanged(nameof(InsideFocalMatDecorationActive)); OnPropertyChanged(nameof(InsideFocalDecorationActive)); OnPropertyChanged(nameof(InsideSectionConfirmEnabled)); NotifyExtraDetailsDone(); }
        partial void OnIsAddingInsideBgMatChanged(bool _)      { OnPropertyChanged(nameof(InsideBgMatDecorationActive)); OnPropertyChanged(nameof(InsideMatDecorationActive)); OnPropertyChanged(nameof(InsideSectionConfirmEnabled)); NotifyExtraDetailsDone(); }
        partial void OnIsAddingInsideAdditionalMatChanged(bool _) { OnPropertyChanged(nameof(InsideAdditionalMatDecorationActive)); OnPropertyChanged(nameof(InsideMatDecorationActive)); OnPropertyChanged(nameof(InsideSectionConfirmEnabled)); NotifyExtraDetailsDone(); }
        partial void OnIsAddingInsideEmbellishmentChanged(bool _)            { OnPropertyChanged(nameof(InsideSectionConfirmEnabled)); }
        partial void OnIsConfiguringCurrentInsideSentimentChanged(bool _)    { OnPropertyChanged(nameof(InsideSectionConfirmEnabled)); }
        partial void OnCurrentInsideMatChanged(WizardBgMat? _)               { OnPropertyChanged(nameof(InsideBgMatDecorationActive)); OnPropertyChanged(nameof(InsideMatDecorationActive)); }
        partial void OnCurrentInsideAdditionalMatChanged(WizardBgMat? _)     { OnPropertyChanged(nameof(InsideAdditionalMatDecorationActive)); OnPropertyChanged(nameof(InsideMatDecorationActive)); }

        [RelayCommand]
        private void StartDecoration()
        {
            ResetDecorationForm();
            var mat = CurrentMat ?? CurrentAdditionalMat ?? CurrentInsideMat ?? CurrentInsideAdditionalMat;
            if (mat != null) mat.HasDecoration = true;
            else if (IsAddingFocalMat) ExteriorFocal.HasDecoration = true;
            else if (IsAddingInsideFocalMat) InsideFocal.HasDecoration = true;
            else if (IsConfiguringCurrentSentiment) SentimentHasDecoration = true;
            else if (IsAddingCardBaseDecoration) CardBase.HasDecoration = true;
            else InsideFocal.HasDecoration = true;
            OnPropertyChanged(nameof(BgMatDecorationActive));
            OnPropertyChanged(nameof(AdditionalMatDecorationActive));
            OnPropertyChanged(nameof(InsideBgMatDecorationActive));
            OnPropertyChanged(nameof(InsideAdditionalMatDecorationActive));
            OnPropertyChanged(nameof(InsideMatDecorationActive));
            OnPropertyChanged(nameof(ExteriorFocalDecorationActive));
            OnPropertyChanged(nameof(InsideFocalMatDecorationActive));
            OnPropertyChanged(nameof(InsideFocalDecorationActive));
            OnPropertyChanged(nameof(CardBaseDecorationActive));
            NotifyExtraDetailsDone();
        }

        [RelayCommand]
        private void AddAnotherDecoration()
        {
            PromptAddMoreDecoration = false;
            var mat = CurrentMat ?? CurrentAdditionalMat ?? CurrentInsideMat ?? CurrentInsideAdditionalMat;
            if (mat != null) mat.HasDecoration = true;
            else if (IsAddingFocalMat) ExteriorFocal.HasDecoration = true;
            else if (IsConfiguringCurrentSentiment) SentimentHasDecoration = true;
            else if (IsAddingCardBaseDecoration) CardBase.HasDecoration = true;
            else InsideFocal.HasDecoration = true;
            ResetDecorationForm();
            OnPropertyChanged(nameof(BgMatDecorationActive));
            OnPropertyChanged(nameof(AdditionalMatDecorationActive));
            OnPropertyChanged(nameof(ExteriorFocalDecorationActive));
            OnPropertyChanged(nameof(InsideFocalDecorationActive));
            OnPropertyChanged(nameof(CardBaseDecorationActive));
            NotifyExtraDetailsDone();
        }

        [RelayCommand]
        private void DoneWithDecorations()
        {
            PromptAddMoreDecoration = false;
            OnPropertyChanged(nameof(BgMatDecorationActive));
            OnPropertyChanged(nameof(AdditionalMatDecorationActive));
            OnPropertyChanged(nameof(CardBaseDecorationActive));
            NotifyExtraDetailsDone();
        }

        [RelayCommand]
        private void CancelDecoration()
        {
            var mat = CurrentMat ?? CurrentAdditionalMat ?? CurrentInsideMat ?? CurrentInsideAdditionalMat;
            if (mat != null)
            {
                mat.HasDecoration = false;
                mat.DecorationItem = null;
                mat.DecorationStampItem = null;
            }
            else if (IsAddingFocalMat)
            {
                ExteriorFocal.HasDecoration = false;
                ExteriorFocal.DecorationItem = null;
                ExteriorFocal.DecorationStampItem = null;
            }
            else if (IsConfiguringCurrentSentiment)
            {
                SentimentHasDecoration = false;
                SentimentDecorationItem = null;
                SentimentDecorationStampItem = null;
            }
            else if (IsAddingCardBaseDecoration)
            {
                CardBase.HasDecoration = false;
                CardBase.DecorationItem = null;
                CardBase.DecorationStampItem = null;
                IsAddingCardBaseDecoration = false;
            }
            else
            {
                InsideFocal.HasDecoration = false;
                InsideFocal.DecorationItem = null;
                InsideFocal.DecorationStampItem = null;
            }
            ResetDecorationForm();
            OnPropertyChanged(nameof(BgMatDecorationActive));
            OnPropertyChanged(nameof(AdditionalMatDecorationActive));
            OnPropertyChanged(nameof(InsideBgMatDecorationActive));
            OnPropertyChanged(nameof(InsideAdditionalMatDecorationActive));
            OnPropertyChanged(nameof(InsideMatDecorationActive));
            OnPropertyChanged(nameof(MatDecorationActive));
            OnPropertyChanged(nameof(ExteriorFocalDecorationActive));
            OnPropertyChanged(nameof(InsideFocalMatDecorationActive));
            OnPropertyChanged(nameof(SentimentDecorationModeActive));
            OnPropertyChanged(nameof(SentimentControlsEnabled));
            OnPropertyChanged(nameof(InsideFocalDecorationActive));
            OnPropertyChanged(nameof(CardBaseDecorationActive));
            OnPropertyChanged(nameof(CardBaseConfirmEnabled));
            NotifyExtraDetailsDone();
        }

        [RelayCommand]
        private void ConfirmCustomColors()
        {
            // Append the selected ink color names to the active "Describe the color" textbox
            // so the user can see what was captured.
            var colors = _blendInkClickOrder.Count > 0 ? string.Join(", ", _blendInkClickOrder) : null;
            if (!string.IsNullOrEmpty(colors))
            {
                var matTarget = CurrentMat ?? CurrentAdditionalMat ?? CurrentInsideMat ?? CurrentInsideAdditionalMat;
                if (matTarget != null && matTarget.IsSelfBlended)
                {
                    matTarget.SelfBlendDescription = AppendColorsToDescription(matTarget.SelfBlendDescription, colors);
                }
                else if (IsAddingFocalMat && ExteriorFocal.IsSelfBlended)
                {
                    ExteriorFocal.SelfBlendDescription = AppendColorsToDescription(ExteriorFocal.SelfBlendDescription, colors);
                }
                else if (IsAddingInsideFocalMat && InsideFocal.IsSelfBlended)
                {
                    InsideFocal.SelfBlendDescription = AppendColorsToDescription(InsideFocal.SelfBlendDescription, colors);
                }
                else if (InsideFocal.IsSelfBlended)
                {
                    // Section 7 inline inside-focal flow (legacy)
                    InsideFocal.SelfBlendDescription = AppendColorsToDescription(InsideFocal.SelfBlendDescription, colors);
                }
            }
            // Sentiment custom-color picker uses the same _blendInkClickOrder; if a sentiment part is being
            // configured, append to its SelfBlendDescription too.
            if (!string.IsNullOrEmpty(colors) && IsConfiguringCurrentSentiment && SentimentIsSelfBlended)
            {
                SentimentSelfBlendDescription = AppendColorsToDescription(SentimentSelfBlendDescription, colors);
            }

            PickBlendInkColors = false;
            PickWatercolors = false;
            SentimentPickBlendInkColors = false;
            SentimentPickWatercolors = false;
        }

        private static string AppendColorsToDescription(string current, string colors)
        {
            if (string.IsNullOrWhiteSpace(current)) return colors;
            // Avoid appending the same colors twice if the user clicks "Done" repeatedly.
            if (current.TrimEnd().EndsWith(colors, StringComparison.OrdinalIgnoreCase)) return current;
            return current.TrimEnd() + " - " + colors;
        }

        // Sentiment decoration (in-progress, committed to CurrentSentimentDecorations on ConfirmCurrentDecoration)
        [ObservableProperty] private bool _sentimentHasDecoration;
        [ObservableProperty] private WizardItemOption? _sentimentDecorationItem;
        [ObservableProperty] private WizardItemOption? _sentimentDecorationStampItem;
        public ObservableCollection<WizardMatDecoration> CurrentSentimentDecorations { get; } = new();

        public bool ShowSentimentDecorationStampSection =>
            SentimentDecorationItem?.Subtype?.Contains("Embossing Powder", StringComparison.OrdinalIgnoreCase) ?? false;

        partial void OnSentimentDecorationItemChanged(WizardItemOption? value)
        {
            SentimentDecorationStampItem = null;
            OnPropertyChanged(nameof(ShowSentimentDecorationStampSection));
        }

        // Whenever the sentiment decoration form opens or closes, refresh derived flags
        // (so the adhesive picker hides and Confirm Piece / Cancel buttons disable while editing an extra detail).
        partial void OnSentimentHasDecorationChanged(bool _)
        {
            OnPropertyChanged(nameof(SentimentDecorationModeActive));
            OnPropertyChanged(nameof(SentimentControlsEnabled));
            OnPropertyChanged(nameof(SentimentAdhesiveVisible));
            OnPropertyChanged(nameof(SentimentExtraDetailsDone));
        }

        // Stamp picker (shown when decoration item subtype == "Embossing Powder")
        [ObservableProperty] private ObservableCollection<string> _decorationStampTypeOptions = new();
        [ObservableProperty] private string? _selectedDecorationStampType;
        [ObservableProperty] private ObservableCollection<SubtypeCheckboxItem> _decorationStampSubtypeFilters = new();
        [ObservableProperty] private ObservableCollection<WizardItemOption> _decorationStampItems = new();
        public bool HasDecorationStampSubtypeFilters => DecorationStampSubtypeFilters.Count > 0;
        private List<WizardItemOption> _allDecorationStampItems = new();

        // Stencil ink layer picker
        private List<string> _inkColorOptions = new();
        // Cached item lists for the per-layer pickers built inside the new
        // stencil layer stepper (Details tab). Populated alongside the legacy
        // global StencilGlitterPicker / StencilHappyMediumPicker / StencilAstroPastePicker.
        private List<WizardItemOption> _stencilLayerGlitterItems     = new();
        private List<WizardItemOption> _stencilLayerHappyMediumItems = new();
        private List<WizardItemOption> _stencilLayerAstroPasteItems  = new();
        [ObservableProperty] private ObservableCollection<SubtypeCheckboxItem> _stencilInkColorSelections = new();
        [ObservableProperty] private int _currentStencilLayerIndex = 1;
        [ObservableProperty] private int _stencilTotalLayers = 1;
        [ObservableProperty] private ObservableCollection<WizardStencilLayer> _completedStencilLayers = new();

        public bool ShowStencilLayerSection =>
            (SelectedDecorationItemType?.Contains("Stencil", StringComparison.OrdinalIgnoreCase) ?? false)
            && CurrentStencilLayerIndex <= StencilTotalLayers
            && (CurrentMat?.DecorationItem != null || CurrentAdditionalMat?.DecorationItem != null
                || ExteriorFocal.DecorationItem != null || CurrentInsideMat?.DecorationItem != null
                || CurrentInsideAdditionalMat?.DecorationItem != null
                || SentimentDecorationItem != null || CardBase.DecorationItem != null);

        public bool StencilAllLayersDone =>
            (SelectedDecorationItemType?.Contains("Stencil", StringComparison.OrdinalIgnoreCase) ?? false)
            && CompletedStencilLayers.Count >= StencilTotalLayers
            && StencilTotalLayers > 0;

        public string FinishStencilLayerButtonLabel =>
            CurrentStencilLayerIndex >= StencilTotalLayers ? "Finish Stencil" : $"Finish Layer {CurrentStencilLayerIndex}";

        public string StencilInkColorSummary
        {
            get
            {
                var selected = StencilInkColorSelections.Where(s => s.IsChecked).Select(s => s.Label).ToList();
                return selected.Count == 0 ? "Select ink colors..." : string.Join(", ", selected);
            }
        }

        // Stamp ink picker (single set, no layers)
        [ObservableProperty] private ObservableCollection<SubtypeCheckboxItem> _stampInkColorSelections = new();

        public bool ShowStampInkSection =>
            (SelectedDecorationItemType?.Contains("Stamp", StringComparison.OrdinalIgnoreCase) ?? false)
            && (CurrentMat?.DecorationItem != null || CurrentAdditionalMat?.DecorationItem != null
                || ExteriorFocal.DecorationItem != null || CurrentInsideMat?.DecorationItem != null
                || CurrentInsideAdditionalMat?.DecorationItem != null
                || SentimentDecorationItem != null || CardBase.DecorationItem != null);

        public string StampInkColorSummary
        {
            get
            {
                var selected = StampInkColorSelections.Where(s => s.IsChecked).Select(s => s.Label).ToList();
                return selected.Count == 0 ? "Select ink colors..." : string.Join(", ", selected);
            }
        }

        // Embossing powder ink picker (Versafine Nocture + Versafine Clair pinned first)
        private static readonly string[] _pinnedEmbossingInkColors = { "Versafine Nocture", "Versafine Clair" };
        private static readonly string[] _cardstockOnlyColors = { "Black Licorice", "Red Pepper" };
        private List<string> _embossingInkColorOptions = new();
        [ObservableProperty] private ObservableCollection<SubtypeCheckboxItem> _embossingInkColorSelections = new();

        public string EmbossingInkColorSummary
        {
            get
            {
                var selected = EmbossingInkColorSelections.Where(s => s.IsChecked).Select(s => s.Label).ToList();
                return selected.Count == 0 ? "Select ink colors..." : string.Join(", ", selected);
            }
        }

        // ── Custom Color cardstock ink/watercolor picker (shared across all mat/sentiment forms) ──
        // PickBlendInkColors and SentimentPickBlendInkColors are VM-level so we can react
        // to changes and populate BlendInkColorSelections on demand.
        [ObservableProperty] private bool _pickBlendInkColors;
        [ObservableProperty] private bool _sentimentPickBlendInkColors;
        [ObservableProperty] private bool _pickWatercolors;
        [ObservableProperty] private bool _sentimentPickWatercolors;
        private List<WizardItemOption> _watercolorItems = new();
        [ObservableProperty] private bool _sentimentIsSelfBlended;
        [ObservableProperty] private string _sentimentSelfBlendDescription = string.Empty;
        [ObservableProperty] private ObservableCollection<SubtypeCheckboxItem> _blendInkColorSelections = new();
        private readonly List<string> _blendInkClickOrder = new();

        public string BlendInkColorSummary =>
            _blendInkClickOrder.Count == 0 ? "Select ink colors..." : string.Join(", ", _blendInkClickOrder);

        public bool ShowBlendInkList => PickBlendInkColors || PickWatercolors;
        public bool ShowSentimentBlendInkList => SentimentPickBlendInkColors || SentimentPickWatercolors;

        partial void OnPickBlendInkColorsChanged(bool value)
        {
            RebuildBlendInkSelections();
            OnPropertyChanged(nameof(ShowBlendInkList));
            NotifyCustomColorsChanged();
        }
        partial void OnSentimentPickBlendInkColorsChanged(bool value)
        {
            RebuildBlendInkSelections();
            OnPropertyChanged(nameof(ShowSentimentBlendInkList));
            NotifyCustomColorsChanged();
        }
        partial void OnPickWatercolorsChanged(bool value)
        {
            RebuildBlendInkSelections();
            OnPropertyChanged(nameof(ShowBlendInkList));
            NotifyCustomColorsChanged();
        }
        partial void OnSentimentPickWatercolorsChanged(bool value)
        {
            RebuildBlendInkSelections();
            OnPropertyChanged(nameof(ShowSentimentBlendInkList));
            NotifyCustomColorsChanged();
        }

        private void RebuildBlendInkSelections()
        {
            var previousOrder = new List<string>(_blendInkClickOrder);
            var checkedLabels = BlendInkColorSelections
                .Where(c => c.IsChecked)
                .Select(c => c.Label)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            BlendInkColorSelections.Clear();
            _blendInkClickOrder.Clear();

            bool showInks = PickBlendInkColors || SentimentPickBlendInkColors;
            bool showWatercolors = PickWatercolors || SentimentPickWatercolors;

            // If both pickers closed (e.g., user clicked "Done Selecting Colors"), preserve
            // the previous click order so the selected colors are retained for the Confirm step.
            if (!showInks && !showWatercolors)
            {
                _blendInkClickOrder.AddRange(previousOrder);
                OnPropertyChanged(nameof(BlendInkColorSummary));
                return;
            }

            if (showInks)
                foreach (var color in _inkColorOptions)
                    AppendCheckboxToBlendSelections(color);

            if (showWatercolors)
                foreach (var item in _watercolorItems)
                    AppendCheckboxToBlendSelections(item.Name);

            // Restore checked state in previous click order
            foreach (var label in previousOrder)
            {
                var cb = BlendInkColorSelections.FirstOrDefault(c => string.Equals(c.Label, label, StringComparison.OrdinalIgnoreCase));
                if (cb != null) cb.IsChecked = true;
            }
            // Also restore any checked items not captured in the order (safety)
            foreach (var label in checkedLabels)
            {
                if (_blendInkClickOrder.Any(l => string.Equals(l, label, StringComparison.OrdinalIgnoreCase))) continue;
                var cb = BlendInkColorSelections.FirstOrDefault(c => string.Equals(c.Label, label, StringComparison.OrdinalIgnoreCase));
                if (cb != null) cb.IsChecked = true;
            }

            OnPropertyChanged(nameof(BlendInkColorSummary));
        }

        private void AppendCheckboxToBlendSelections(string label)
        {
            var cb = new SubtypeCheckboxItem { Label = label };
            cb.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(SubtypeCheckboxItem.IsChecked))
                {
                    if (cb.IsChecked) _blendInkClickOrder.Add(cb.Label);
                    else _blendInkClickOrder.Remove(cb.Label);
                }
                OnPropertyChanged(nameof(BlendInkColorSummary));
            };
            BlendInkColorSelections.Add(cb);
        }

        private void ResetBlendInkSelections()
        {
            PickBlendInkColors = false;
            PickWatercolors = false;
            // Both false → RebuildBlendInkSelections produces empty list; also clear order
            _blendInkClickOrder.Clear();
            OnPropertyChanged(nameof(BlendInkColorSummary));
        }

        private void RestoreBlendInkSelectionsForEdit(List<string> savedColors)
        {
            var watercolorNames = _watercolorItems.Select(i => i.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            bool hasWatercolors = savedColors.Any(c => watercolorNames.Contains(c));
            bool hasInkColors = savedColors.Any(c => !watercolorNames.Contains(c));

            // Set picker flags (each triggers RebuildBlendInkSelections, but we override below)
            PickBlendInkColors = hasInkColors;
            PickWatercolors = hasWatercolors;

            // Final rebuild with exact saved order
            BlendInkColorSelections.Clear();
            _blendInkClickOrder.Clear();
            if (hasInkColors)
                foreach (var color in _inkColorOptions)
                    AppendCheckboxToBlendSelections(color);
            if (hasWatercolors)
                foreach (var item in _watercolorItems)
                    AppendCheckboxToBlendSelections(item.Name);
            foreach (var color in savedColors)
            {
                var cb = BlendInkColorSelections.FirstOrDefault(c => string.Equals(c.Label, color, StringComparison.OrdinalIgnoreCase));
                if (cb != null) cb.IsChecked = true;
            }

            OnPropertyChanged(nameof(ShowBlendInkList));
            OnPropertyChanged(nameof(BlendInkColorSummary));
        }

        // ── Cardstock color options (shared across all sections) ──────────────
        private static readonly string[] _pinnedCardstockColors =
            { "Sugar Cube", "Black Licorice", "Buttercream Frosting", "Toffee" };
        public List<string> CardstockColorOptions { get; private set; } = new();

        // Foil / Glitter cardstock items (names loaded once at init)
        private List<string> _foilCardstockNames = new();
        private List<string> _glitterCardstockNames = new();
        // All cardstock items keyed by name for "Items Used" lookup
        private Dictionary<string, int> _cardstockItemIdByName = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, int> _foilCardstockIdByName = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, int> _glitterCardstockIdByName = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, int> _adhesiveIdByName = new(StringComparer.OrdinalIgnoreCase);
        // Ink pad items keyed by color name (Mini Cube preferred, Full Size fallback)
        private Dictionary<string, int> _inkItemIdByColor = new(StringComparer.OrdinalIgnoreCase);

        // Per-section cardstock/foil/glitter toggles (Cardstock defaults to true)
        [ObservableProperty] private bool _bgMatCardstock = true;
        [ObservableProperty] private bool _bgMatFoilCardstock;
        [ObservableProperty] private bool _bgMatGlitterCardstock;
        [ObservableProperty] private bool _additionalMatCardstock = true;
        [ObservableProperty] private bool _additionalMatFoilCardstock;
        [ObservableProperty] private bool _additionalMatGlitterCardstock;
        [ObservableProperty] private bool _focalCardstock = true;
        [ObservableProperty] private bool _focalFoilCardstock;
        [ObservableProperty] private bool _focalGlitterCardstock;
        [ObservableProperty] private bool _insideFocalCardstock = true;
        [ObservableProperty] private bool _insideFocalFoilCardstock;
        [ObservableProperty] private bool _insideFocalGlitterCardstock;
        [ObservableProperty] private bool _insideBgMatCardstock = true;
        [ObservableProperty] private bool _insideBgMatFoilCardstock;
        [ObservableProperty] private bool _insideBgMatGlitterCardstock;
        [ObservableProperty] private bool _insideAdditionalMatCardstock = true;
        [ObservableProperty] private bool _insideAdditionalMatFoilCardstock;
        [ObservableProperty] private bool _insideAdditionalMatGlitterCardstock;
        [ObservableProperty] private bool _sentimentCardstock = true;
        [ObservableProperty] private bool _sentimentFoilCardstock;
        [ObservableProperty] private bool _sentimentGlitterCardstock;

        // Computed cardstock lists per section
        public List<string> BgMatCardstockOptions        => BuildCardstockOptions(BgMatCardstock, BgMatFoilCardstock, BgMatGlitterCardstock);
        public List<string> AdditionalMatCardstockOptions => BuildCardstockOptions(AdditionalMatCardstock, AdditionalMatFoilCardstock, AdditionalMatGlitterCardstock);
        public List<string> FocalCardstockOptions         => BuildCardstockOptions(FocalCardstock, FocalFoilCardstock, FocalGlitterCardstock);
        public List<string> InsideFocalCardstockOptions   => BuildCardstockOptions(InsideFocalCardstock, InsideFocalFoilCardstock, InsideFocalGlitterCardstock);
        public List<string> InsideBgMatCardstockOptions   => BuildCardstockOptions(InsideBgMatCardstock, InsideBgMatFoilCardstock, InsideBgMatGlitterCardstock);
        public List<string> InsideAdditionalMatCardstockOptions => BuildCardstockOptions(InsideAdditionalMatCardstock, InsideAdditionalMatFoilCardstock, InsideAdditionalMatGlitterCardstock);
        public List<string> SentimentCardstockOptions     => BuildCardstockOptions(SentimentCardstock, SentimentFoilCardstock, SentimentGlitterCardstock);

        partial void OnInsideBgMatCardstockChanged(bool _)        { OnPropertyChanged(nameof(InsideBgMatCardstockOptions));        if (CurrentInsideMat != null) CurrentInsideMat.SelectedCardstockColor = InsideBgMatCardstockOptions.FirstOrDefault(); }
        partial void OnInsideBgMatFoilCardstockChanged(bool _)    { OnPropertyChanged(nameof(InsideBgMatCardstockOptions));        if (CurrentInsideMat != null) CurrentInsideMat.SelectedCardstockColor = InsideBgMatCardstockOptions.FirstOrDefault(); }
        partial void OnInsideBgMatGlitterCardstockChanged(bool _) { OnPropertyChanged(nameof(InsideBgMatCardstockOptions));        if (CurrentInsideMat != null) CurrentInsideMat.SelectedCardstockColor = InsideBgMatCardstockOptions.FirstOrDefault(); }
        partial void OnInsideAdditionalMatCardstockChanged(bool _)        { OnPropertyChanged(nameof(InsideAdditionalMatCardstockOptions)); if (CurrentInsideAdditionalMat != null) CurrentInsideAdditionalMat.SelectedCardstockColor = InsideAdditionalMatCardstockOptions.FirstOrDefault(); }
        partial void OnInsideAdditionalMatFoilCardstockChanged(bool _)    { OnPropertyChanged(nameof(InsideAdditionalMatCardstockOptions)); if (CurrentInsideAdditionalMat != null) CurrentInsideAdditionalMat.SelectedCardstockColor = InsideAdditionalMatCardstockOptions.FirstOrDefault(); }
        partial void OnInsideAdditionalMatGlitterCardstockChanged(bool _) { OnPropertyChanged(nameof(InsideAdditionalMatCardstockOptions)); if (CurrentInsideAdditionalMat != null) CurrentInsideAdditionalMat.SelectedCardstockColor = InsideAdditionalMatCardstockOptions.FirstOrDefault(); }

        partial void OnBgMatCardstockChanged(bool _)
        {
            OnPropertyChanged(nameof(BgMatCardstockOptions));
            if (CurrentMat != null) CurrentMat.SelectedCardstockColor = BgMatCardstockOptions.FirstOrDefault();
        }
        partial void OnBgMatFoilCardstockChanged(bool _)
        {
            OnPropertyChanged(nameof(BgMatCardstockOptions));
            if (CurrentMat != null) CurrentMat.SelectedCardstockColor = BgMatCardstockOptions.FirstOrDefault();
        }
        partial void OnBgMatGlitterCardstockChanged(bool _)
        {
            OnPropertyChanged(nameof(BgMatCardstockOptions));
            if (CurrentMat != null) CurrentMat.SelectedCardstockColor = BgMatCardstockOptions.FirstOrDefault();
        }
        partial void OnAdditionalMatCardstockChanged(bool _)
        {
            OnPropertyChanged(nameof(AdditionalMatCardstockOptions));
            if (CurrentAdditionalMat != null) CurrentAdditionalMat.SelectedCardstockColor = AdditionalMatCardstockOptions.FirstOrDefault();
        }
        partial void OnAdditionalMatFoilCardstockChanged(bool _)
        {
            OnPropertyChanged(nameof(AdditionalMatCardstockOptions));
            if (CurrentAdditionalMat != null) CurrentAdditionalMat.SelectedCardstockColor = AdditionalMatCardstockOptions.FirstOrDefault();
        }
        partial void OnAdditionalMatGlitterCardstockChanged(bool _)
        {
            OnPropertyChanged(nameof(AdditionalMatCardstockOptions));
            if (CurrentAdditionalMat != null) CurrentAdditionalMat.SelectedCardstockColor = AdditionalMatCardstockOptions.FirstOrDefault();
        }
        partial void OnFocalCardstockChanged(bool _)
        {
            OnPropertyChanged(nameof(FocalCardstockOptions));
            ExteriorFocal.SelectedCardstockColor = FocalCardstockOptions.FirstOrDefault();
        }
        partial void OnFocalFoilCardstockChanged(bool _)
        {
            OnPropertyChanged(nameof(FocalCardstockOptions));
            ExteriorFocal.SelectedCardstockColor = FocalCardstockOptions.FirstOrDefault();
        }
        partial void OnFocalGlitterCardstockChanged(bool _)
        {
            OnPropertyChanged(nameof(FocalCardstockOptions));
            ExteriorFocal.SelectedCardstockColor = FocalCardstockOptions.FirstOrDefault();
        }
        partial void OnInsideFocalCardstockChanged(bool _)
        {
            OnPropertyChanged(nameof(InsideFocalCardstockOptions));
            InsideFocal.SelectedCardstockColor = InsideFocalCardstockOptions.FirstOrDefault();
        }
        partial void OnInsideFocalFoilCardstockChanged(bool _)
        {
            OnPropertyChanged(nameof(InsideFocalCardstockOptions));
            InsideFocal.SelectedCardstockColor = InsideFocalCardstockOptions.FirstOrDefault();
        }
        partial void OnInsideFocalGlitterCardstockChanged(bool _)
        {
            OnPropertyChanged(nameof(InsideFocalCardstockOptions));
            InsideFocal.SelectedCardstockColor = InsideFocalCardstockOptions.FirstOrDefault();
        }
        partial void OnSentimentCardstockChanged(bool _)
        {
            OnPropertyChanged(nameof(SentimentCardstockOptions));
            SentimentConfigCardstockColor = SentimentCardstockOptions.FirstOrDefault();
        }
        partial void OnSentimentFoilCardstockChanged(bool _)
        {
            OnPropertyChanged(nameof(SentimentCardstockOptions));
            SentimentConfigCardstockColor = SentimentCardstockOptions.FirstOrDefault();
        }
        partial void OnSentimentGlitterCardstockChanged(bool _)
        {
            OnPropertyChanged(nameof(SentimentCardstockOptions));
            SentimentConfigCardstockColor = SentimentCardstockOptions.FirstOrDefault();
        }

        private List<string> BuildCardstockOptions(bool includeCardstock, bool includeFoil, bool includeGlitter)
        {
            // Combine all enabled buckets, then re-sort the whole list with the same pinned
            // ordering used by the new item pickers — so Sugar Cube / Black Licorice /
            // Buttercream Frosting / Toffee always float to the top regardless of which
            // bucket they came from (e.g. a "Sugar Cube Foil 8.5x11" lands at the top
            // alongside the plain "Sugar Cube" cardstock).
            var combined = new List<string>();
            if (includeCardstock) combined.AddRange(CardstockColorOptions.Where(c => c != "Other"));
            if (includeFoil)      combined.AddRange(_foilCardstockNames);
            if (includeGlitter)   combined.AddRange(_glitterCardstockNames);

            int Rank(string name)
            {
                for (int i = 0; i < _pinnedCardstockColors.Length; i++)
                    if (name.Contains(_pinnedCardstockColors[i], StringComparison.OrdinalIgnoreCase))
                        return i;
                return int.MaxValue;
            }

            var sorted = combined
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(Rank)
                .ThenBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
            sorted.Add("Other"); // always last
            return sorted;
        }

        // ── Section 3: Foil-It items ──────────────────────────────────────────
        [ObservableProperty] private ObservableCollection<WizardItemOption> _foilItItems = new();

        // ── Section 3: Additional Mats ────────────────────────────────────────
        [ObservableProperty] private ObservableCollection<WizardBgMatGroup> _additionalMats = new();
        [ObservableProperty] private bool _isAddingAdditionalMat;
        [ObservableProperty] private bool _showAddAnotherAdditionalMatPiecePrompt;
        [ObservableProperty] private WizardBgMat? _currentAdditionalMat;
        private WizardBgMatGroup? _currentAdditionalMatGroup;

        // ── Section 4: Focal Mat items (loaded once at init) ─────────────────
        [ObservableProperty] private WizardFocalSection _exteriorFocal = new();
        [ObservableProperty] private ObservableCollection<WizardItemOption> _allDieItems = new();
        // Frames items - Dies filtered to subtype "Frames", used by every "How was this mat cut?" Frames branch
        [ObservableProperty] private ObservableCollection<WizardItemOption> _framesItems = new();
        private List<WizardItemOption> _allDieItemsCache = new();
        [ObservableProperty] private ObservableCollection<SubtypeCheckboxItem> _dieSubtypeFilters = new();
        public bool HasDieSubtypeFilters => DieSubtypeFilters.Count > 0;
        [ObservableProperty] private ObservableCollection<WizardItemOption> _allStampItems = new();
        [ObservableProperty] private ObservableCollection<WizardItemOption> _allEmbellishmentItems = new();

        // ── Section 5: Sentiments ────────────────────────────────────────────
        [ObservableProperty] private string _sentimentSearchQuery = string.Empty;
        [ObservableProperty] private ObservableCollection<WizardSentimentSelection> _sentimentResults = new();
        [ObservableProperty] private bool _isSentimentSearching;
        [ObservableProperty] private bool _sentimentFilterDies;
        [ObservableProperty] private bool _sentimentFilterStamps;

        // Theme search infrastructure (subtype-based; existing legacy theme dropdown
        // pre-loads SentimentThemes from a config file).
        public List<string> SentimentThemes { get; private set; } = new();
        // "Full sets" filter — when on, expand matched results to include every
        // sentiment in the matching set. Was previously misnamed SentimentSearchByTheme.
        [ObservableProperty] private bool _sentimentFilterFullSets;
        // "Theme search" filter — when on, the typed query is treated as a theme name
        // (subtype) rather than literal sentiment text. Replaces the old separate
        // theme dropdown UX.
        [ObservableProperty] private bool _sentimentFilterThemeSearch;
        [ObservableProperty] private string _sentimentSelectedTheme = string.Empty;
        [ObservableProperty] private bool _isSentimentThemeSearching;

        partial void OnSentimentFilterFullSetsChanged(bool value)
        {
            SentimentResults.Clear();
            if (!string.IsNullOrWhiteSpace(SentimentSearchQuery))
                _ = SearchSentiments();
        }

        partial void OnSentimentFilterThemeSearchChanged(bool value)
        {
            SentimentResults.Clear();
            if (!string.IsNullOrWhiteSpace(SentimentSearchQuery))
                _ = SearchSentiments();
        }

        // Configured sentiments list (replaces old multi-select flow)
        [ObservableProperty] private ObservableCollection<WizardConfiguredSentiment> _configuredSentiments = new();

        // Current sentiment being configured
        [ObservableProperty] private WizardSentimentSelection? _currentSentimentResult;
        [ObservableProperty] private bool _isConfiguringCurrentSentiment;
        [ObservableProperty] private bool _showAddAnotherSentimentPrompt;

        // Sentiment config state
        [ObservableProperty] private string? _sentimentConfigCardstockColor;
        [ObservableProperty] private string _sentimentOtherCardstockText = string.Empty;
        public string SentimentEffectiveCardstockColor =>
            SentimentConfigCardstockColor == "Other" ? SentimentOtherCardstockText : SentimentConfigCardstockColor ?? string.Empty;
        partial void OnSentimentOtherCardstockTextChanged(string v) => OnPropertyChanged(nameof(SentimentEffectiveCardstockColor));
        partial void OnSentimentConfigCardstockColorChanged(string? v) => OnPropertyChanged(nameof(SentimentEffectiveCardstockColor));
        [ObservableProperty] private bool _sentimentIsEmbossed;
        [ObservableProperty] private WizardItemOption? _sentimentEmbossingPowder;
        [ObservableProperty] private ObservableCollection<WizardItemOption> _embossingPowderItems = new();
        // ── Sentiment multi-part state ────────────────────────────────────────────
        private readonly List<WizardConfiguredSentimentPart> _currentSentimentParts = new();
        private readonly List<string> _sentimentOtherNotes = new();
        [ObservableProperty] private bool _showSentimentMorePartsQuestion;
        public int CurrentSentimentPartNumber => _currentSentimentParts.Count + 1;
        public bool IsAddingMoreSentimentParts => _currentSentimentParts.Count > 0;
        public bool CanAddMoreSentimentParts => _currentSentimentParts.Count < 4;

        [ObservableProperty] private ObservableCollection<SubtypeCheckboxItem> _sentimentStampInkSelections = new();

        public bool SentimentResultIsStamp =>
            CurrentSentimentResult?.ItemType?.Contains("Stamp", StringComparison.OrdinalIgnoreCase) ?? false;

        public string SentimentStampInkSummary
        {
            get
            {
                var selected = SentimentStampInkSelections.Where(s => s.IsChecked).Select(s => s.Label).ToList();
                return selected.Count == 0 ? "Select ink colors..." : string.Join(", ", selected);
            }
        }

        // ── Adhesive (shared across mat forms and sentiment config) ──────────────
        [ObservableProperty] private string? _selectedAdhesive;
        [ObservableProperty] private ObservableCollection<string> _adhesiveItems = new();

        // ── Sentiment hub (Phase 2 of remaster) ───────────────────────────────
        // Sentiment hub has two render states gated off IsConfiguringCurrentSentiment:
        //   • Search state (no selection): textbox + 4 filter checkboxes + Search.
        //   • Configuring state (sentiment picked): collapsed result card + 3 sub-page
        //     buttons (Cardstock / Details / Adhesives) + inline ink section + 2 actions.
        // Sub-pages drill in via SentimentSubStep ("Hub" / "Cardstock" / "Adhesives";
        // Details routes through the shared Details panel via DetailsReturnTarget).
        //
        // Alternative considered: split each sentiment piece into its own dedicated
        // section (like BG mat group/pieces). Rejected — sentiment parts are simpler
        // (no per-piece cutting method etc.), so the lighter sub-step model fits.
        [ObservableProperty] private string _sentimentSubStep = "Hub";

        partial void OnSentimentSubStepChanged(string value)
        {
            OnPropertyChanged(nameof(IsSentimentSubStepHub));
            OnPropertyChanged(nameof(IsSentimentSubStepCardstock));
            OnPropertyChanged(nameof(IsSentimentSubStepAdhesives));
            OnPropertyChanged(nameof(IsDetailsStepActive));
        }

        public bool IsSentimentSubStepHub        => SentimentSubStep == "Hub";
        public bool IsSentimentSubStepCardstock  => SentimentSubStep == "Cardstock";
        public bool IsSentimentSubStepAdhesives  => SentimentSubStep == "Adhesives";
        public bool IsSentimentSubStepDetails    => SentimentSubStep == "Details";

        // Per-piece "Done!" indicators on the hub sub-page buttons. Reset between pieces.
        [ObservableProperty] private bool _sentimentPieceCardstockSaved;
        [ObservableProperty] private bool _sentimentPieceDetailsSaved;
        [ObservableProperty] private bool _sentimentPieceAdhesivesSaved;

        // ── Sentiment hub navigation ──────────────────────────────────────────
        [RelayCommand] private void NavSentimentToHub()       => SentimentSubStep = "Hub";
        [RelayCommand] private void NavSentimentToCardstock() => SentimentSubStep = "Cardstock";
        [RelayCommand]
        private void NavSentimentToDetails()
        {
            DetailsReturnTarget = "Sentiment";
            SentimentSubStep = "Details";
        }
        [RelayCommand] private void NavSentimentToAdhesives() => SentimentSubStep = "Adhesives";
        [RelayCommand] private void BackToSentimentHub()      => SentimentSubStep = "Hub";

        // ── Sentiment / Cardstock sub-page (mirrors BG mat Cardstock) ─────────
        public WizardItemPicker SentimentCardstockPicker        { get; } = new() { PlaceholderText = "Cardstock" };
        public WizardItemPicker SentimentFoilCardstockPicker    { get; } = new() { PlaceholderText = "Foil Cardstock" };
        public WizardItemPicker SentimentGlitterCardstockPicker { get; } = new() { PlaceholderText = "Glitter Cardstock" };

        [RelayCommand]
        private void SaveSentimentCardstock()
        {
            var picked =
                SentimentCardstockPicker.SelectedItem
                ?? SentimentFoilCardstockPicker.SelectedItem
                ?? SentimentGlitterCardstockPicker.SelectedItem;
            SentimentConfigCardstockColor = picked?.Name;
            SentimentPieceCardstockSaved = picked != null || SentimentIsSelfBlended;
            SentimentSubStep = "Hub";
        }

        // ── Sentiment / Adhesives sub-page (mirrors BG mat Adhesives) ─────────
        public WizardItemPicker SentimentGlueAdhesivePicker       { get; } = new() { PlaceholderText = "Glue" };
        public WizardItemPicker SentimentFoamAdhesivePicker       { get; } = new() { PlaceholderText = "Foam" };
        public WizardItemPicker SentimentTapeRunnerAdhesivePicker { get; } = new() { PlaceholderText = "Tape Runner" };

        // Per-piece accumulated adhesives (committed on Add 1 Piece / Finish Sentiment).
        public ObservableCollection<string> CurrentSentimentPieceAdhesives { get; } = new();

        public bool HasCurrentSentimentAdhesivePick =>
            SentimentGlueAdhesivePicker.SelectedItem != null
            || SentimentFoamAdhesivePicker.SelectedItem != null
            || SentimentTapeRunnerAdhesivePicker.SelectedItem != null;

        public string CurrentSentimentAdhesivePreview
        {
            get
            {
                var parts = new List<string>();
                if (SentimentGlueAdhesivePicker.SelectedItem != null)       parts.Add(SentimentGlueAdhesivePicker.SelectedItem.Name);
                if (SentimentFoamAdhesivePicker.SelectedItem != null)       parts.Add(SentimentFoamAdhesivePicker.SelectedItem.Name);
                if (SentimentTapeRunnerAdhesivePicker.SelectedItem != null) parts.Add(SentimentTapeRunnerAdhesivePicker.SelectedItem.Name);
                return parts.Count == 0 ? string.Empty : string.Join("   •   ", parts);
            }
        }

        [RelayCommand]
        private void SaveSentimentAdhesives()
        {
            foreach (var picker in new[] { SentimentGlueAdhesivePicker, SentimentFoamAdhesivePicker, SentimentTapeRunnerAdhesivePicker })
            {
                if (picker.SelectedItem != null && !CurrentSentimentPieceAdhesives.Contains(picker.SelectedItem.Name))
                    CurrentSentimentPieceAdhesives.Add(picker.SelectedItem.Name);
                picker.SelectedItem = null;
            }
            SentimentPieceAdhesivesSaved = CurrentSentimentPieceAdhesives.Count > 0;
            SentimentSubStep = "Hub";
        }

        // ── Sentiment / Ink section (inline on the hub, matches Details tab pattern) ──
        // Reuses the InkSelection chip control from the Details tab. The "Was this
        // embossed?" chip + StampEmbossingPowderPicker pattern is intentionally identical
        // so muscle memory carries over.
        public InkSelection SentimentInks { get; } = new();
        public WizardItemPicker SentimentStampEmbossingPowderPicker { get; } = new() { PlaceholderText = "Embossing Powder" };

        // ── Add 1 Piece to sentiment / Finish Sentiment ───────────────────────
        // Add 1 Piece: capture current selections as a part, append to _currentSentimentParts,
        // reset selection state, return to search so the user can pick the next piece.
        // Finish Sentiment: same capture, then push the accumulated parts into a new
        // WizardConfiguredSentiment on ConfiguredSentiments and return to the main hub.
        [RelayCommand]
        private void AddSentimentPiece()
        {
            if (CurrentSentimentResult == null) return;
            CaptureCurrentSentimentPart();
            ResetSentimentPieceState();
            // Clear the selection so the search results re-expand for the next pick.
            CurrentSentimentResult = null;
            IsConfiguringCurrentSentiment = false;
            OnPropertyChanged(nameof(SentimentResultIsStamp));
        }

        [RelayCommand]
        private void FinishSentiment()
        {
            if (CurrentSentimentResult != null)
                CaptureCurrentSentimentPart();
            if (_currentSentimentParts.Count > 0)
            {
                var sentiment = new WizardConfiguredSentiment { IsInside = IsInsideMode };
                foreach (var p in _currentSentimentParts) sentiment.Parts.Add(p);
                ConfiguredSentiments.Add(sentiment);
                _currentSentimentParts.Clear();
            }
            SentimentSaved = ConfiguredSentiments.Count > 0;
            ResetSentimentPieceState();
            CurrentSentimentResult = null;
            IsConfiguringCurrentSentiment = false;
            SentimentResults.Clear();
            SentimentSearchQuery = string.Empty;
            UpdateSummaryLines();
            CurrentSection = "Hub";
        }

        private void CaptureCurrentSentimentPart()
        {
            if (CurrentSentimentResult == null) return;
            var part = new WizardConfiguredSentimentPart
            {
                ItemId          = CurrentSentimentResult.ItemId,
                ItemName        = CurrentSentimentResult.ItemName,
                ThumbnailBase64 = CurrentSentimentResult.ThumbnailBase64,
                IsStampType     = SentimentResultIsStamp,
                CardstockColor  = SentimentEffectiveCardstockColor,
                IsSelfBlended   = SentimentIsSelfBlended,
                SelfBlendDescription = SentimentSelfBlendDescription,
                IsEmbossed      = SentimentInks.IsEmbossed,
                EmbossingPowderName = SentimentInks.IsEmbossed ? SentimentStampEmbossingPowderPicker.SelectedItem?.Name : null,
                EmbossingPowderItemId = SentimentInks.IsEmbossed ? SentimentStampEmbossingPowderPicker.SelectedItem?.Id : null,
            };
            foreach (var c in SentimentInks.Ordered) part.StampInkColors.Add(c);
            foreach (var a in CurrentSentimentPieceAdhesives) part.Adhesives.Add(a);
            // Self-blend inks (only meaningful when IsSelfBlended). Cleared otherwise so
            // toggling self-blend off doesn't leak inks into the captured part.
            if (SentimentIsSelfBlended)
                foreach (var c in SentimentBlendInks.Ordered) part.BlendInkColors.Add(c);
            // AddedDetails entries from the shared Details panel (routed via DetailsReturnTarget).
            // _pendingSentimentDetails accumulates them per part — see DetailsReturnTarget routing.
            foreach (var d in _pendingSentimentDetails) part.AddedDetails.Add(d);
            _pendingSentimentDetails.Clear();
            _currentSentimentParts.Add(part);
        }

        // Holding pen for Details entries while configuring a sentiment piece. Captured
        // into the WizardConfiguredSentimentPart on Add 1 Piece / Finish Sentiment.
        private readonly ObservableCollection<WizardDetailEntry> _pendingSentimentDetails = new();

        private void ResetSentimentPieceState()
        {
            SentimentPieceCardstockSaved = false;
            SentimentPieceDetailsSaved   = false;
            SentimentPieceAdhesivesSaved = false;
            SentimentConfigCardstockColor = null;
            SentimentOtherCardstockText = string.Empty;
            SentimentIsSelfBlended = false;
            SentimentSelfBlendDescription = string.Empty;
            SentimentInks.Clear();
            SentimentBlendInks.Clear();
            SentimentStampEmbossingPowderPicker.SelectedItem = null;
            CurrentSentimentPieceAdhesives.Clear();
            _pendingSentimentDetails.Clear();
            SentimentCardstockPicker.SelectedItem        = null;
            SentimentFoilCardstockPicker.SelectedItem    = null;
            SentimentGlitterCardstockPicker.SelectedItem = null;
            SentimentGlueAdhesivePicker.SelectedItem       = null;
            SentimentFoamAdhesivePicker.SelectedItem       = null;
            SentimentTapeRunnerAdhesivePicker.SelectedItem = null;
            SentimentSubStep = "Hub";
        }

        // ── Section 6: Embellishments ────────────────────────────────────────
        [ObservableProperty] private ObservableCollection<WizardEmbellishment> _addedEmbellishments = new();
        [ObservableProperty] private bool _isAddingEmbellishment;
        [ObservableProperty] private WizardItemOption? _newEmbellishmentItem;
        [ObservableProperty] private WizardItemOption? _newEmbellishmentStampItem;
        [ObservableProperty] private ObservableCollection<WizardItemOption> _embellishmentItemsForSubtype = new();
        [ObservableProperty] private ObservableCollection<SubtypeCheckboxItem> _embellishmentSubtypeFilters = new();
        public bool HasEmbellishmentSubtypeFilters => EmbellishmentSubtypeFilters.Count > 0;
        public bool ShowEmbellishmentStampSection =>
            NewEmbellishmentItem?.Subtype?.Contains("Embossing Powder", StringComparison.OrdinalIgnoreCase) ?? false;
        [ObservableProperty] private bool _embellishmentsLoaded;
        private List<WizardItemOption> _allEmbellishmentItemsFlat = new();
        partial void OnNewEmbellishmentItemChanged(WizardItemOption? value)
        {
            NewEmbellishmentStampItem = null;
            OnPropertyChanged(nameof(ShowEmbellishmentStampSection));
        }

        // ── Section 7: Inside ────────────────────────────────────────────────
        [ObservableProperty] private bool? _hasInside; // null = not answered
        [ObservableProperty] private ObservableCollection<WizardBgMat> _insideBgMats = new();
        [ObservableProperty] private bool _isAddingInsideBgMat;
        [ObservableProperty] private WizardBgMat? _currentInsideMat;

        // Inside Additional Mats (mirrors outside Additional Mats - button-driven)
        [ObservableProperty] private ObservableCollection<WizardBgMat> _insideAdditionalMats = new();
        [ObservableProperty] private bool _isAddingInsideAdditionalMat;
        [ObservableProperty] private WizardBgMat? _currentInsideAdditionalMat;

        // Inside Focal Mat - button-driven (form hidden until user clicks "+ Add Inside Focal Mat")
        [ObservableProperty] private WizardFocalSection _insideFocal = new();
        [ObservableProperty] private bool _isAddingInsideFocalMat;
        [ObservableProperty] private bool _hasInsideFocalMat;     // true once user has confirmed an inside focal mat

        // Inside Sentiments - search → click result → configure → confirm
        [ObservableProperty] private ObservableCollection<WizardSentimentSelection> _insideSentimentResults = new();
        [ObservableProperty] private string _insideSentimentSearchQuery = string.Empty;
        [ObservableProperty] private bool _isInsideSentimentSearching;
        [ObservableProperty] private bool _insideSentimentFilterDies;
        [ObservableProperty] private bool _insideSentimentFilterStamps;
        [ObservableProperty] private bool _insideSentimentSearchByTheme;
        partial void OnInsideSentimentSearchByThemeChanged(bool value)
        {
            if (!string.IsNullOrWhiteSpace(InsideSentimentSearchQuery))
                _ = SearchInsideSentiments();
        }
        [ObservableProperty] private ObservableCollection<WizardConfiguredSentiment> _configuredInsideSentiments = new();
        [ObservableProperty] private WizardSentimentSelection? _currentInsideSentimentResult;
        [ObservableProperty] private bool _isConfiguringCurrentInsideSentiment;
        [ObservableProperty] private string? _insideSentimentConfigCardstockColor;
        [ObservableProperty] private string _insideSentimentOtherCardstockText = string.Empty;
        [ObservableProperty] private bool _insideSentimentCardstockOn = true;
        [ObservableProperty] private bool _insideSentimentFoilCardstockOn;
        [ObservableProperty] private bool _insideSentimentGlitterCardstockOn;
        public List<string> InsideSentimentCardstockOptions => BuildCardstockOptions(InsideSentimentCardstockOn, InsideSentimentFoilCardstockOn, InsideSentimentGlitterCardstockOn);
        public string InsideSentimentEffectiveCardstockColor =>
            InsideSentimentConfigCardstockColor == "Other" ? InsideSentimentOtherCardstockText : (InsideSentimentConfigCardstockColor ?? string.Empty);
        partial void OnInsideSentimentCardstockOnChanged(bool _)        => OnPropertyChanged(nameof(InsideSentimentCardstockOptions));
        partial void OnInsideSentimentFoilCardstockOnChanged(bool _)    => OnPropertyChanged(nameof(InsideSentimentCardstockOptions));
        partial void OnInsideSentimentGlitterCardstockOnChanged(bool _) => OnPropertyChanged(nameof(InsideSentimentCardstockOptions));

        // Inside Embellishments - mirrors outside Embellishments flow (Add → form → Confirm)
        [ObservableProperty] private ObservableCollection<WizardEmbellishment> _insideAddedEmbellishments = new();
        [ObservableProperty] private bool _isAddingInsideEmbellishment;
        [ObservableProperty] private WizardItemOption? _newInsideEmbellishmentItem;
        [ObservableProperty] private WizardItemOption? _newInsideEmbellishmentStampItem;
        public bool ShowInsideEmbellishmentStampSection =>
            NewInsideEmbellishmentItem?.Subtype?.Contains("Embossing Powder", StringComparison.OrdinalIgnoreCase) ?? false;
        partial void OnNewInsideEmbellishmentItemChanged(WizardItemOption? value)
        {
            NewInsideEmbellishmentStampItem = null;
            OnPropertyChanged(nameof(ShowInsideEmbellishmentStampSection));
        }

        // ── Section 8: Envelope ──────────────────────────────────────────────
        [ObservableProperty] private WizardItemOption? _selectedEnvelopeItem;
        [ObservableProperty] private ObservableCollection<WizardItemOption> _envelopeItems = new();
        [ObservableProperty] private ObservableCollection<SubtypeCheckboxItem> _envelopeSubtypeFilters = new();
        public bool HasEnvelopeSubtypeFilters => EnvelopeSubtypeFilters.Count > 0;
        private List<WizardItemOption> _allEnvelopeItems = new();

        // Optional storage bag dropdown - items loaded from inventory using "Storage Bags"
        [ObservableProperty] private WizardItemOption? _selectedStorageBagItem;
        [ObservableProperty] private ObservableCollection<WizardItemOption> _storageBagItems = new();

        // ── Confirmation text ────────────────────────────────────────────────
        // (Legacy _confirmationText field removed.)

        // ── Shared data (loaded once) ─────────────────────────────────────────
        private List<WizardItemOption> _allInsiderItems = new();
        private List<WizardItemOption> _allPlannedOutItems = new();

        // Optional callback raised when the user uploads an image inside the wizard,
        // so the parent (ProjectsViewModel) can mirror it into NewProjectImages.
        private Action<string>? _onImageAddedToProject;

        public CardBuildWizardViewModel(InventoryService service) : this(service, null, null) { }
        public CardBuildWizardViewModel(InventoryService service, string? projectImageBase64) : this(service, projectImageBase64, null) { }

        public CardBuildWizardViewModel(InventoryService service, string? projectImageBase64, Action<string>? onImageAddedToProject)
        {
            _service = service;
            _projectImageBase64 = projectImageBase64;
            _onImageAddedToProject = onImageAddedToProject;
            FocalParts.CollectionChanged += (_, __) => OnFocalPartsChanged();
            WireFollowupNotifications();
            WireBgCutPickerEcho();
            // Envelopes picker on the main hub — mirror selection onto SelectedEnvelopeItem
            // (which Create Card / build steps / summary all consume) and flip the
            // EnvelopeSaved flag for hub-button "- Done!" state.
            EnvelopesPicker.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(WizardItemPicker.SelectedItem)) return;
                SelectedEnvelopeItem = EnvelopesPicker.SelectedItem;
                EnvelopeSaved = SelectedEnvelopeItem != null;
                UpdateSummaryLines();
            };
            CardBase.Decorations.CollectionChanged += (_, __) =>
            {
                OnPropertyChanged(nameof(CardBaseDecorationsSummary));
                OnPropertyChanged(nameof(CardBaseExtraDetailsDone));
                OnPropertyChanged(nameof(CardBaseConfirmEnabled));
                OnPropertyChanged(nameof(CardBaseDecorationActive));
            };
        }

        public async Task InitializeAsync()
        {
            // Build cardstock color list: pinned 4 first, then rest from ColorOrder
            var colorOrder = InventoryService.ColorOrder;
            var rest = colorOrder.Where(c => !_pinnedCardstockColors
                .Any(p => string.Equals(p, c, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();
            CardstockColorOptions = _pinnedCardstockColors.ToList().Concat(rest).Concat(new[] { "Other" }).ToList();
            OnPropertyChanged(nameof(CardstockColorOptions));

            // Build alphabetical ink color list for stencil layers and stamps (cardstock-only colors excluded)
            _inkColorOptions = colorOrder
                .Where(c => !_cardstockOnlyColors.Any(x => string.Equals(x, c, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();

            // Build embossing powder ink color list: Versafine Nocture + Versafine Clair pinned first, then rest A-Z
            var pinnedEmbossing = _pinnedEmbossingInkColors
                .Select(p => colorOrder.FirstOrDefault(c => string.Equals(c, p, StringComparison.OrdinalIgnoreCase)) ?? p)
                .ToList();
            var restEmbossing = colorOrder
                .Where(c => !_pinnedEmbossingInkColors.Any(p => string.Equals(p, c, StringComparison.OrdinalIgnoreCase))
                         && !_cardstockOnlyColors.Any(x => string.Equals(x, c, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();
            _embossingInkColorOptions = pinnedEmbossing.Concat(restEmbossing).ToList();

            // Build ink pad lookup: color name → item ID (Mini Cube preferred, Full Size fallback)
            var labels = CardLabelMappingService.Default;
            var miniCubeInks = await labels.GetItemsForLabelAsync("Mini Cube Inks", _service);
            var fullSizeInks = await labels.GetItemsForLabelAsync("Full Pad Inks", _service);
            _inkItemIdByColor = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var color in colorOrder)
            {
                var match = miniCubeInks.FirstOrDefault(i => i.Name.Contains(color, StringComparison.OrdinalIgnoreCase));
                match ??= fullSizeInks.FirstOrDefault(i => i.Name.Contains(color, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                    _inkItemIdByColor.TryAdd(color, match.Id);
            }

            // Restrict the ink-color pickers to colors the user actually owns an
            // ink in (Mini Cube or full pad), kept in Color Order sequence. Colors
            // they don't own can still be added via the "Custom Color" button.
            _inkColorOptions = _inkColorOptions
                .Where(c => _inkItemIdByColor.ContainsKey(c))
                .ToList();

            // Load foil and glitter cardstock item names for the per-section checkboxes
            var foilCardstockItems = await labels.GetItemsForLabelAsync("Foil Cardstock", _service);
            _foilCardstockNames = foilCardstockItems.Select(i => i.Name).ToList();
            var glitterCardstockItems = await labels.GetItemsForLabelAsync("Glitter Cardstock", _service);
            _glitterCardstockNames = glitterCardstockItems.Select(i => i.Name).ToList();

            // ── Populate the new Cardstock sub-page item lists ────────────────
            // Regular cardstock = type "Cardstock", subtype "8.5x11". Sorted with pinned colors
            // first (matching the existing CardstockColorOptions pinning), then alphabetical.
            // (BULK) sheets are excluded — bulk packs aren't selected per-card.
            static bool IsBulk(string name) => name.Contains("(BULK)", StringComparison.OrdinalIgnoreCase);

            // Same pinned ordering applied to every cardstock dropdown (regular / foil / glitter):
            // Sugar Cube, Black Licorice, Buttercream Frosting, Toffee at the top — anything
            // whose name contains one of those colors lands in the matching pinned group;
            // everything else falls through to alphabetical.
            int PinnedRank(string name)
            {
                for (int i = 0; i < _pinnedCardstockColors.Length; i++)
                    if (name.Contains(_pinnedCardstockColors[i], StringComparison.OrdinalIgnoreCase))
                        return i;
                return int.MaxValue;
            }
            IEnumerable<WizardItemOption> SortedCardstock(IEnumerable<WizardItemOption> items) =>
                items.Where(i => !IsBulk(i.Name))
                     .OrderBy(i => PinnedRank(i.Name))
                     .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase);

            // Restrict to 8.5x11 cardstock specifically. subtypeContains handles two cases:
            //   1. Items with a combined subtype like "8.5X11, Background" still match.
            //   2. Case differences ("8.5X11" canonical in subtypes.json vs the lowercase
            //      query string here) — the service ToLowers both sides under contains.
            var regularCardstockItems = await labels.GetItemsForLabelAsync("Cardstock", _service);
            BaseCardstockRegularItems.Clear();
            foreach (var it in SortedCardstock(regularCardstockItems)) BaseCardstockRegularItems.Add(it);
            BaseCardstockFoilItems.Clear();
            foreach (var it in SortedCardstock(foilCardstockItems)) BaseCardstockFoilItems.Add(it);
            BaseCardstockGlitterItems.Clear();
            foreach (var it in SortedCardstock(glitterCardstockItems)) BaseCardstockGlitterItems.Add(it);

            // Eagerly preload cardstock thumbnails BEFORE the other pickers below queue
            // their items. Cardstock has 100+ items with large photo thumbnails — without
            // this, the user opening the Cardstock dropdown at the BG mat / sentiment
            // sub-pages waits for the decode queue to drain (5-8s on first open). Doing
            // it here ensures cardstock IDs are at the front of the semaphore-bounded
            // worker pool. Subsequent picker.Load() calls are dedup'd via _cache.ContainsKey.
            var cardstockPairs = BaseCardstockRegularItems
                .Concat(BaseCardstockFoilItems)
                .Concat(BaseCardstockGlitterItems)
                .Where(i => i.Id > 0)
                .Select(i => (i.Id, ImageUrl: i.ImageUrl))
                .ToList();
            if (cardstockPairs.Count > 0) ThumbnailCacheService.PreloadAsync(cardstockPairs);

            // ── Populate Details sub-page pickers ─────────────────────────────
            // Each picker pulls items by type, with the chip strip sourced from the
            // user-defined subtypes (settings → subtypes.json) and matched via CONTAINS
            // so an item with combined subtypes (e.g. "A2, Background") shows under
            // each chip it mentions.
            List<string>? Subs(string type) => UserSettingsService.GetSubtypesForType(type);
            StampsPicker.Load(await labels.GetItemsForLabelAsync("Stamps", _service), Subs("Stamps"));
            DiesPicker.Load(await labels.GetItemsForLabelAsync("Dies", _service), Subs("Dies"));
            EmbellishmentsPicker.Load(await labels.GetItemsForLabelAsync("Embellishments", _service), Subs("Embellishments"));
            StackletsPicker.Load(await labels.GetItemsForLabelAsync("Stacklets", _service), Subs("Stacklets"));
            EmbossingFoldersPicker.Load(await labels.GetItemsForLabelAsync("Embossing Folders", _service), Subs("Embossing Folders"));
            StencilsPicker.Load(await labels.GetItemsForLabelAsync("Stencils", _service), Subs("Stencils"));
            OloMarkersPicker.Load(await labels.GetItemsForLabelAsync("OLO Markers", _service), Subs("OLO Markers"));
            FoilsPicker.Load(await labels.GetItemsForLabelAsync("Foils", _service), Subs("Foils"));

            // Stencil follow-up multi-pickers (Embellishments filtered by subtype) plus
            // the parallel set used inside the Foils → Glitter Grab follow-up. Subtype
            // match is case-insensitive Contains so "Glitter, Embossing Powder" appears
            // under the Glitter chip, etc.
            var stencilFollowupEmbItems = await labels.GetItemsForLabelAsync("Embellishments", _service);
            List<MyCraftyStash.Services.WizardItemOption> SubFilter(string sub) =>
                stencilFollowupEmbItems.Where(i => i.Subtype != null &&
                                                   i.Subtype.Contains(sub, StringComparison.OrdinalIgnoreCase)).ToList();
            var glitterItems     = SubFilter("Glitter");
            var happyMediumItems = SubFilter("Happy Medium");
            var astroPasteItems  = SubFilter("Astro Paste");
            StencilGlitterPicker.Load(glitterItems);
            StencilHappyMediumPicker.Load(happyMediumItems);
            StencilAstroPastePicker.Load(astroPasteItems);
            // Cache so the per-layer pickers built inside the stencil layer
            // stepper can be loaded with the same item lists without re-querying.
            _stencilLayerGlitterItems     = glitterItems;
            _stencilLayerHappyMediumItems = happyMediumItems;
            _stencilLayerAstroPasteItems  = astroPasteItems;
            FoilStencilPicker.Load(await labels.GetItemsForLabelAsync("Stencils", _service), Subs("Stencils"));
            FoilStencilGlitterPicker.Load(glitterItems);
            FoilStencilHappyMediumPicker.Load(happyMediumItems);
            FoilStencilAstroPastePicker.Load(astroPasteItems);
            var watercolorItemsForPicker = await labels.GetItemsForLabelAsync("Watercolor", _service);
            WatercolorsPicker.Load(watercolorItemsForPicker, Subs("Watercolor"));
            EnvelopesPicker.Load(
                await labels.GetItemsForLabelAsync("Envelopes", _service),
                Subs("Envelopes"));
            // Inside hub: liner cardstock picker. Same source as base cardstock
            // (label "Cardstock" with subtype 8.5x11), kept as one bag of items
            // so the inside layer can be anything the user owns in stock paper.
            InsideLinerCardstockPicker.Load(
                await labels.GetItemsForLabelAsync("Cardstock", _service),
                Subs("Cardstock"));

            // Embossing-powder picker (used inside the stamp follow-up when "Did you emboss?" is yes,
            // and for the embellishment embossing-powder follow-up). Pre-filtered to Embellishments
            // with subtype containing "Embossing Powder".
            var stampEmbPowderItems = await labels.GetItemsForLabelAsync("Embossing Powder", _service);
            StampEmbossingPowderPicker.Load(stampEmbPowderItems);
            EmbellEmbossingStampPicker.Load(await labels.GetItemsForLabelAsync("Stamps", _service), Subs("Stamps"));

            // BG Mat / How was it cut — five method-specific dropdowns:
            //   Stacklets       → type Stacklets, chips from Stacklets subtypes (settings)
            //   All Planned Out → name search "all planned out" (no subtypes — flat list)
            //   Frames          → Dies items with subtype Frames
            //   Insider         → Cardstock items with subtype Insider
            //   Foil-It         → Cardstock items with subtype Foil-it (subtypeContains for variants)
            BgCutStackletsPicker.Load(
                await labels.GetItemsForLabelAsync("Stacklets", _service),
                Subs("Stacklets"));
            BgCutPlannedOutPicker.Load(
                await labels.GetItemsForLabelAsync("All Planned Out", _service),
                Array.Empty<string>());
            BgCutFramesPicker.Load(
                await labels.GetItemsForLabelAsync("Frames Die", _service),
                Array.Empty<string>());
            BgCutInsiderPicker.Load(
                await labels.GetItemsForLabelAsync("Insider Cardstock", _service),
                Array.Empty<string>());
            BgCutFoilItPicker.Load(
                await labels.GetItemsForLabelAsync("Foil-It Cardstock", _service),
                Array.Empty<string>());
            BgCutFoilsPicker.Load(
                await labels.GetItemsForLabelAsync("Foils", _service),
                Array.Empty<string>());

            // Inks pick from ColorOrder directly (alphabetical, cardstock-only colors excluded).
            InkColorOptionsForPicker.Clear();
            foreach (var c in _inkColorOptions) InkColorOptionsForPicker.Add(c);

            // Multi-select ink dropdowns for the stamp + embellishment-embossing follow-ups.
            // Each chip carries the corresponding ink item id (Mini Cube preferred, then Full Pad),
            // so the dropdown rows render real inventory thumbnails.
            int InkItemIdFor(string color) => _inkItemIdByColor.TryGetValue(color, out var id) ? id : 0;
            StampInks.SetColors(_inkColorOptions, InkItemIdFor);
            EmbellEmbossingInks.SetColors(_inkColorOptions, InkItemIdFor);
            StencilInks.SetColors(_inkColorOptions, InkItemIdFor);
            FoilStencilInks.SetColors(_inkColorOptions, InkItemIdFor);
            BgPieceBlendInks.SetColors(_inkColorOptions, InkItemIdFor);
            SentimentBlendInks.SetColors(_inkColorOptions, InkItemIdFor);
            BaseBlendInks.SetColors(_inkColorOptions, InkItemIdFor);

            // Watercolors live alongside inks under the dropdown's Custom Color toggle.
            // The same chip → Ordered handler runs for both lists, so toggling a
            // watercolor chip blends seamlessly into the multi-select preview.
            StampInks.SetWatercolors(watercolorItemsForPicker);
            EmbellEmbossingInks.SetWatercolors(watercolorItemsForPicker);
            StencilInks.SetWatercolors(watercolorItemsForPicker);
            BgPieceBlendInks.SetWatercolors(watercolorItemsForPicker);
            SentimentBlendInks.SetWatercolors(watercolorItemsForPicker);
            BaseBlendInks.SetWatercolors(watercolorItemsForPicker);

            // Build lookup: color name (and item name) → item ID (used to add cardstock to "Items Used")
            // CardstockColorOptions uses short color names (e.g. "Sugar Cube") from ColorOrder,
            // but DB item names may be longer (e.g. "Sugar Cube 8.5x11 Cardstock"), so we also
            // add entries keyed by the matching color prefix so both forms resolve correctly.
            var allCardstockItems = await labels.GetItemsForLabelAsync("Cardstock", _service);
            _cardstockItemIdByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            // Sort longest color names first so "Black Licorice" wins over "Black" when both match
            var colorOrderList = InventoryService.ColorOrder
                .OrderByDescending(c => c.Length).ToList();
            foreach (var item in allCardstockItems)
            {
                _cardstockItemIdByName.TryAdd(item.Name, item.Id);
                // Match every color that appears as a whole phrase anywhere in the item name
                // (word-boundary check: preceded and followed by start/end or a space)
                foreach (var c in colorOrderList)
                {
                    var idx = item.Name.IndexOf(c, StringComparison.OrdinalIgnoreCase);
                    if (idx < 0) continue;
                    bool startOk = idx == 0 || item.Name[idx - 1] == ' ';
                    bool endOk   = idx + c.Length == item.Name.Length || item.Name[idx + c.Length] == ' ';
                    if (startOk && endOk)
                        _cardstockItemIdByName.TryAdd(c, item.Id);
                }
            }
            // Separate lookups for foil and glitter - keyed by their item names (same strings shown in dropdown)
            _foilCardstockIdByName    = foilCardstockItems.ToDictionary(i => i.Name, i => i.Id, StringComparer.OrdinalIgnoreCase);
            _glitterCardstockIdByName = glitterCardstockItems.ToDictionary(i => i.Name, i => i.Id, StringComparer.OrdinalIgnoreCase);

            _allInsiderItems = await labels.GetItemsForLabelAsync("Insider Cardstock", _service);
            _allPlannedOutItems = await labels.GetItemsForLabelAsync("All Planned Out", _service);
            InsiderItems = new ObservableCollection<WizardItemOption>(_allInsiderItems);
            PlannedOutItems = new ObservableCollection<WizardItemOption>(_allPlannedOutItems);

            // Load focal mat items - derive subtypes directly from die items so filters always populate
            var dieItems = await labels.GetItemsForLabelAsync("Dies", _service);
            _allDieItemsCache = dieItems;
            AllDieItems = new ObservableCollection<WizardItemOption>(dieItems);
            // Frames items - every Die whose subtype contains "Frames"
            FramesItems = new ObservableCollection<WizardItemOption>(
                dieItems.Where(i => i.Subtype?.Contains("Frames", StringComparison.OrdinalIgnoreCase) ?? false));
            var dieSubtypes = dieItems
                .Where(i => !string.IsNullOrEmpty(i.Subtype))
                .SelectMany(i => i.Subtype!.Split(',').Select(s => s.Trim()))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var s in dieSubtypes)
            {
                var cb = new SubtypeCheckboxItem { Label = s };
                cb.PropertyChanged += (_, _) => ApplyDieSubtypeFilter();
                DieSubtypeFilters.Add(cb);
            }
            OnPropertyChanged(nameof(HasDieSubtypeFilters));

            var stampItems = await labels.GetItemsForLabelAsync("Stamps", _service);
            AllStampItems = new ObservableCollection<WizardItemOption>(stampItems);

            var embItems = await labels.GetItemsForLabelAsync("Embellishments", _service);
            AllEmbellishmentItems = new ObservableCollection<WizardItemOption>(embItems);

            var foilItems = await labels.GetItemsForLabelAsync("Foil-It Cardstock", _service);
            FoilItItems = new ObservableCollection<WizardItemOption>(foilItems);

            var allTypes = await _service.GetAllItemTypesAsync();
            var excludedTypes = InventoryService.GetProjectExcludedItemTypes();
            var filteredTypes = allTypes.Where(t => !excludedTypes.Contains(t)).ToList();
            DecorationTypeOptions = new ObservableCollection<string>(filteredTypes);
            DecorationStampTypeOptions = new ObservableCollection<string>(filteredTypes);

            // Load adhesive items - keep a name→id lookup so the chosen adhesive
            // can also be recorded in the project's items-used list.
            var adhesiveItems = await labels.GetItemsForLabelAsync("Adhesives", _service);
            var glueItems       = await labels.GetItemsForLabelAsync("Glue Adhesive", _service);
            var foamItems       = await labels.GetItemsForLabelAsync("Foam Adhesive", _service);
            var tapeRunnerItems = await labels.GetItemsForLabelAsync("Tape Runner Adhesive", _service);
            AdhesiveItems = new ObservableCollection<string>(adhesiveItems.Select(i => i.Name));
            _adhesiveIdByName = adhesiveItems
                .GroupBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

            // Cardbase / Adhesives sub-page: each picker pre-filtered via the
            // configurable label mapping. Empty chip list suppresses the chip strip.
            var noSubChips = System.Array.Empty<string>();
            GlueAdhesivePicker.Load(glueItems, noSubChips);
            FoamAdhesivePicker.Load(foamItems, noSubChips);
            TapeRunnerAdhesivePicker.Load(tapeRunnerItems, noSubChips);

            // BG Mat hub — Cardstock pickers reuse the Cardbase 8.5x11 / Foil / Glitter
            // item lists (already loaded above into BaseCardstock*Items).
            BgPieceCardstockPicker.Load(BaseCardstockRegularItems, noSubChips);
            BgPieceFoilCardstockPicker.Load(BaseCardstockFoilItems, noSubChips);
            BgPieceGlitterCardstockPicker.Load(BaseCardstockGlitterItems, noSubChips);

            // BG Mat hub — Adhesives mirror Cardbase Adhesives, separate instances so
            // selections don't bleed when navigating between contexts.
            BgPieceGlueAdhesivePicker.Load(glueItems, noSubChips);
            BgPieceFoamAdhesivePicker.Load(foamItems, noSubChips);
            BgPieceTapeRunnerAdhesivePicker.Load(tapeRunnerItems, noSubChips);

            // Sentiment hub — Cardstock + Adhesive pickers (per-context instances).
            SentimentCardstockPicker.Load(BaseCardstockRegularItems, noSubChips);
            SentimentFoilCardstockPicker.Load(BaseCardstockFoilItems, noSubChips);
            SentimentGlitterCardstockPicker.Load(BaseCardstockGlitterItems, noSubChips);
            SentimentGlueAdhesivePicker.Load(glueItems, noSubChips);
            SentimentFoamAdhesivePicker.Load(foamItems, noSubChips);
            SentimentTapeRunnerAdhesivePicker.Load(tapeRunnerItems, noSubChips);
            // Embossing-powder picker for the inline sentiment ink section. Same source as
            // the Details tab's StampEmbossingPowderPicker but a separate instance keeps
            // selections independent across contexts.
            SentimentStampEmbossingPowderPicker.Load(stampEmbPowderItems);
            // Set up the sentiment ink chips with the same color list + ink-item-id lookup.
            SentimentInks.SetColors(_inkColorOptions, InkItemIdFor);
            SentimentInks.SetWatercolors(watercolorItemsForPicker);

            // Details main-hub Inks/Watercolors dropdown — same chip set, plus
            // the Custom Color toggle for blending across both lists.
            DetailsInks.SetColors(_inkColorOptions, InkItemIdFor);
            DetailsInks.SetWatercolors(watercolorItemsForPicker);

            // Load envelopes and build subtype filters
            _allEnvelopeItems = await labels.GetItemsForLabelAsync("Envelopes", _service);
            var envelopeSubtypes = _allEnvelopeItems
                .Where(i => !string.IsNullOrEmpty(i.Subtype))
                .SelectMany(i => i.Subtype!.Split(',').Select(s => s.Trim()))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var s in envelopeSubtypes)
            {
                var cb = new SubtypeCheckboxItem { Label = s };
                cb.PropertyChanged += (_, _) => ApplyEnvelopeSubtypeFilter();
                EnvelopeSubtypeFilters.Add(cb);
            }
            OnPropertyChanged(nameof(HasEnvelopeSubtypeFilters));
            EnvelopeItems = new ObservableCollection<WizardItemOption>(_allEnvelopeItems);

            // Load storage bags (used in the Envelope section as an optional dropdown)
            var storageBagItems = await labels.GetItemsForLabelAsync("Storage Bags", _service);
            StorageBagItems = new ObservableCollection<WizardItemOption>(storageBagItems);

            // Load embossing powder items for sentiment config
            var embPowderItems = await labels.GetItemsForLabelAsync("Embossing Powder", _service);
            EmbossingPowderItems = new ObservableCollection<WizardItemOption>(embPowderItems);

            // Load watercolor items for the custom color picker
            _watercolorItems = await labels.GetItemsForLabelAsync("Watercolor", _service);

            // Load themes for sentiment theme search.
            // MCS stores config in settings.db via ConfigStore; JandH reads
            // text files from the network share. Same end shape (List<string>),
            // just sourced differently.
            SentimentThemes = MyCraftyStash.Services.ConfigStore
                .GetLines(MyCraftyStash.Services.ConfigStore.Themes)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Trim())
                .OrderBy(l => l, StringComparer.OrdinalIgnoreCase)
                .ToList();
            OnPropertyChanged(nameof(SentimentThemes));
        }

        private static bool MatchesSubtypeFilter(string? subtype, List<string> selected) =>
            subtype != null && selected.All(s =>
                subtype.Split(',').Select(p => p.Trim()).Any(p =>
                    string.Equals(p, s, StringComparison.OrdinalIgnoreCase)));

        // Returns how many of the selected subtypes an item's subtype field contains
        // (comma-separated subtype values are each tested individually).
        private static int CountSubtypeMatches(string? subtype, List<string> selected) =>
            subtype == null ? 0 : selected.Count(s =>
                subtype.Split(',').Select(p => p.Trim()).Any(p =>
                    string.Equals(p, s, StringComparison.OrdinalIgnoreCase)));

        /// <summary>
        /// Filters <paramref name="items"/> to only those matching at least one selected subtype,
        /// then sorts by match count (descending), subtype A-Z, name A-Z.
        /// When nothing is selected the full list is returned sorted by subtype then name.
        /// </summary>
        private static IEnumerable<WizardItemOption> SortWithSelectedSubtypesFirst(
            IEnumerable<WizardItemOption> items,
            List<string> selectedSubtypes,
            Func<WizardItemOption, List<string>, int> scoreFn)
        {
            var list = items.ToList();
            if (selectedSubtypes.Count == 0)
                return list.OrderBy(i => i.Subtype ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                           .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase);

            return list.Where(i => MatchesSubtypeFilter(i.Subtype, selectedSubtypes))
                       .OrderByDescending(i => scoreFn(i, selectedSubtypes))
                       .ThenBy(i => i.Subtype ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                       .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase);
        }

        private void ApplyDecorationSubtypeFilter()
        {
            var selected = DecorationSubtypeFilters.Where(s => s.IsChecked).Select(s => s.Label).ToList();
            DecorationItems = new ObservableCollection<WizardItemOption>(
                SortWithSelectedSubtypesFirst(_allDecorationItems, selected, (i, sel) => CountSubtypeMatches(i.Subtype, sel)));
        }

        // Restores decoration type, subtypes, and selected item when entering edit mode.
        // Uses the saved item's type if available, falls back to SelectedDecorationItemType so
        // the list reloads even when no item was previously chosen.
        // The reassign callback receives the freshly-loaded instance matching the saved item's ID
        // so the ComboBox SelectedItem binding resolves by reference against the new collection.
        private async Task RestoreDecorationStateForEditAsync(WizardItemOption? existingItem, Action<WizardItemOption?> reassign)
        {
            var type = existingItem?.ItemType
                ?? (string.IsNullOrEmpty(SelectedDecorationItemType) ? null : SelectedDecorationItemType);
            if (string.IsNullOrEmpty(type)) return;

            _suppressDecorationLoad = true;
            try
            {
                _decorationLoadCts?.Cancel();
                _decorationLoadCts?.Dispose();
                _decorationLoadCts = new CancellationTokenSource();
                var cts = _decorationLoadCts;

                DecorationSubtypeFilters.Clear();
                _allDecorationItems = new List<WizardItemOption>();
                OnPropertyChanged(nameof(HasDecorationSubtypeFilters));

                var items = await _service.GetWizardItemsAsync(type: type);
                if (cts.IsCancellationRequested) return;

                _allDecorationItems = items;
                SelectedDecorationItemType = type;

                var subtypes = UserSettingsService.GetSubtypesForType(type);
                foreach (var s in subtypes)
                {
                    var cb = new SubtypeCheckboxItem { Label = s };
                    cb.PropertyChanged += (_, _) => ApplyDecorationSubtypeFilter();
                    DecorationSubtypeFilters.Add(cb);
                }
                OnPropertyChanged(nameof(HasDecorationSubtypeFilters));
                DecorationItems = new ObservableCollection<WizardItemOption>(items);

                // Re-assign from the freshly loaded collection so WPF reference equality matches
                var matched = existingItem != null ? items.FirstOrDefault(i => i.Id == existingItem.Id) : null;
                reassign(matched);
            }
            finally
            {
                _suppressDecorationLoad = false;
            }
        }

        private void ApplyDecorationStampSubtypeFilter()
        {
            var selected = DecorationStampSubtypeFilters.Where(s => s.IsChecked).Select(s => s.Label).ToList();
            DecorationStampItems = new ObservableCollection<WizardItemOption>(
                SortWithSelectedSubtypesFirst(_allDecorationStampItems, selected, (i, sel) => CountSubtypeMatches(i.Subtype, sel)));
        }

        // (Legacy ConfirmSection1 / EditSection1 / ConfirmSection2 commands removed —
        //  the new hub doesn't use sequential expand/confirm semantics.)

        // EditSection2 removed (legacy form has no Edit button anymore).

        // ── Section 3: Background Mats ────────────────────────────────────────

        private List<WizardItemOption> _allStackletItems = new();

        private async Task LoadSection3DataAsync()
        {
            _allStackletItems = await CardLabelMappingService.Default.GetItemsForLabelAsync("Stacklets", _service);
            StackletSubtypeFilters.Clear();

            var subtypes = UserSettingsService.GetSubtypesForType("Stacklets");
            foreach (var s in subtypes)
            {
                var cb = new SubtypeCheckboxItem { Label = s };
                cb.PropertyChanged += (_, _) => ApplyStackletSubtypeFilter();
                StackletSubtypeFilters.Add(cb);
            }

            OnPropertyChanged(nameof(HasStackletSubtypeFilters));
            StackletItems = new ObservableCollection<WizardItemOption>(_allStackletItems);
        }

        private void ApplyStackletSubtypeFilter()
        {
            var selected = StackletSubtypeFilters.Where(s => s.IsChecked).Select(s => s.Label).ToList();
            StackletItems = new ObservableCollection<WizardItemOption>(
                SortWithSelectedSubtypesFirst(_allStackletItems, selected,
                    (i, sel) => sel.Count(s => i.Subtype != null &&
                                               i.Subtype.Contains(s, StringComparison.OrdinalIgnoreCase))));
        }

        private void ApplyEnvelopeSubtypeFilter()
        {
            var selected = EnvelopeSubtypeFilters.Where(s => s.IsChecked).Select(s => s.Label).ToList();
            EnvelopeItems = new ObservableCollection<WizardItemOption>(
                SortWithSelectedSubtypesFirst(_allEnvelopeItems, selected, (i, sel) => CountSubtypeMatches(i.Subtype, sel)));
            SelectedEnvelopeItem = null;
        }

        private void ApplyDieSubtypeFilter()
        {
            var selected = DieSubtypeFilters.Where(s => s.IsChecked).Select(s => s.Label).ToList();
            AllDieItems = new ObservableCollection<WizardItemOption>(
                SortWithSelectedSubtypesFirst(_allDieItemsCache, selected,
                    (i, sel) => CountSubtypeMatches(i.Subtype, sel)));
        }

        [RelayCommand]
        private void StartAddBgMat()
        {
            _currentBgMatGroup = new WizardBgMatGroup { GroupNumber = BgMats.Count + 1, TypeLabel = "Background", IsInside = IsInsideMode };
            BgMatCardstock = true;
            BgMatFoilCardstock = false;
            BgMatGlitterCardstock = false;
            CurrentMat = new WizardBgMat { Layer = 1 };
            CurrentMat.SelectedCardstockColor = CardstockColorOptions.FirstOrDefault();
            OnPropertyChanged(nameof(BgMatCardstockOptions)); // ensure ItemsSource is pushed after SelectedCardstockColor is set
            InsiderSentiments = new ObservableCollection<string>();
            ResetBlendInkSelections();
            // Reset decoration state for the new mat
            SelectedDecorationItemType = null;
            DecorationSubtypeFilters.Clear();
            DecorationItems = new ObservableCollection<WizardItemOption>();
            _allDecorationItems = new List<WizardItemOption>();
            SelectedDecorationStampType = null;
            DecorationStampSubtypeFilters.Clear();
            DecorationStampItems = new ObservableCollection<WizardItemOption>();
            _allDecorationStampItems = new List<WizardItemOption>();
            ResetStencilState();
            InitAdhesiveSelections();
            IsAddingBgMat = true;
        }

        [RelayCommand]
        private async Task LoadDecorationItems(string? type)
        {
            if (_suppressDecorationLoad) return;
            _decorationLoadCts?.Cancel();
            _decorationLoadCts?.Dispose();
            _decorationLoadCts = new CancellationTokenSource();
            var cts = _decorationLoadCts;

            DecorationSubtypeFilters.Clear();
            _allDecorationItems = new List<WizardItemOption>();
            OnPropertyChanged(nameof(HasDecorationSubtypeFilters));
            ResetStencilState();
            if (string.IsNullOrEmpty(type)) { DecorationItems = new(); return; }

            var items = await _service.GetWizardItemsAsync(type: type);

            if (cts.IsCancellationRequested) return;

            _allDecorationItems = items;

            var subtypes = UserSettingsService.GetSubtypesForType(type);
            foreach (var s in subtypes)
            {
                var cb = new SubtypeCheckboxItem { Label = s };
                cb.PropertyChanged += (_, _) => ApplyDecorationSubtypeFilter();
                DecorationSubtypeFilters.Add(cb);
            }
            OnPropertyChanged(nameof(HasDecorationSubtypeFilters));
            DecorationItems = new ObservableCollection<WizardItemOption>(items);

            if (CurrentMat != null) CurrentMat.DecorationItem = null;
            else if (CurrentAdditionalMat != null) CurrentAdditionalMat.DecorationItem = null;
            else if (CurrentInsideMat != null) CurrentInsideMat.DecorationItem = null;
            else if (CurrentInsideAdditionalMat != null) CurrentInsideAdditionalMat.DecorationItem = null;
            else if (IsAddingFocalMat) ExteriorFocal.DecorationItem = null;
            else if (IsAddingInsideFocalMat) InsideFocal.DecorationItem = null;
            else if (IsConfiguringCurrentSentiment) SentimentDecorationItem = null;
            else if (IsAddingCardBaseDecoration) CardBase.DecorationItem = null;
            else InsideFocal.DecorationItem = null;
        }

        [RelayCommand]
        private async Task LoadDecorationStampItems(string? type)
        {
            _decorationStampLoadCts?.Cancel();
            _decorationStampLoadCts?.Dispose();
            _decorationStampLoadCts = new CancellationTokenSource();
            var cts = _decorationStampLoadCts;

            DecorationStampSubtypeFilters.Clear();
            _allDecorationStampItems = new List<WizardItemOption>();
            OnPropertyChanged(nameof(HasDecorationStampSubtypeFilters));
            if (string.IsNullOrEmpty(type)) { DecorationStampItems = new(); return; }

            var items = await _service.GetWizardItemsAsync(type: type);

            if (cts.IsCancellationRequested) return;

            _allDecorationStampItems = items;

            var subtypes = UserSettingsService.GetSubtypesForType(type);
            foreach (var s in subtypes)
            {
                var cb = new SubtypeCheckboxItem { Label = s };
                cb.PropertyChanged += (_, _) => ApplyDecorationStampSubtypeFilter();
                DecorationStampSubtypeFilters.Add(cb);
            }
            OnPropertyChanged(nameof(HasDecorationStampSubtypeFilters));
            DecorationStampItems = new ObservableCollection<WizardItemOption>(items);

            if (CurrentMat != null) CurrentMat.DecorationStampItem = null;
            else if (CurrentAdditionalMat != null) CurrentAdditionalMat.DecorationStampItem = null;
            else if (IsConfiguringCurrentSentiment) SentimentDecorationStampItem = null;
            else if (IsAddingCardBaseDecoration) CardBase.DecorationStampItem = null;
            else ExteriorFocal.DecorationStampItem = null;
        }

        // ── Stencil ink layer helpers ─────────────────────────────────────────

        public void InitStencilForDecorationItem(WizardItemOption item)
        {
            StencilTotalLayers = item.StencilLayers ?? 1;
            CurrentStencilLayerIndex = 1;
            CompletedStencilLayers.Clear();
            var mat = CurrentMat ?? CurrentAdditionalMat ?? CurrentInsideMat ?? CurrentInsideAdditionalMat;
            mat?.StencilInkLayers.Clear();
            if (!IsConfiguringCurrentSentiment && ExteriorFocal.DecorationItem == item) ExteriorFocal.StencilInkLayers.Clear();
            ResetCurrentLayerSelections();
            OnPropertyChanged(nameof(ShowStencilLayerSection));
            OnPropertyChanged(nameof(StencilAllLayersDone));
            OnPropertyChanged(nameof(FinishStencilLayerButtonLabel));
        }

        private void ResetStencilState()
        {
            CurrentStencilLayerIndex = 1;
            StencilTotalLayers = 1;
            CompletedStencilLayers.Clear();
            StencilInkColorSelections.Clear();
            OnPropertyChanged(nameof(ShowStencilLayerSection));
            OnPropertyChanged(nameof(StencilAllLayersDone));
            OnPropertyChanged(nameof(FinishStencilLayerButtonLabel));
            StampInkColorSelections.Clear();
            OnPropertyChanged(nameof(ShowStampInkSection));
            OnPropertyChanged(nameof(StampInkColorSummary));
            EmbossingInkColorSelections.Clear();
            OnPropertyChanged(nameof(EmbossingInkColorSummary));
        }

        private void InitAdhesiveSelections()
        {
            SelectedAdhesive = null;
        }

        private void ResetSentimentConfigState()
        {
            _currentSentimentParts.Clear();
            CurrentSentimentDecorations.Clear();
            CurrentSentimentResult = null;
            IsConfiguringCurrentSentiment = false;
            ShowAddAnotherSentimentPrompt = false;
            ShowSentimentMorePartsQuestion = false;
            SentimentCardstock = true;
            SentimentFoilCardstock = false;
            SentimentGlitterCardstock = false;
            SentimentConfigCardstockColor = null;
            SentimentOtherCardstockText = string.Empty;
            SentimentIsSelfBlended = false;
            SentimentSelfBlendDescription = string.Empty;
            SentimentPickBlendInkColors = false;
            SentimentPickWatercolors = false;
            SentimentIsEmbossed = false;
            SentimentEmbossingPowder = null;
            SentimentStampInkSelections.Clear();
            ResetBlendInkSelections();
            SentimentHasDecoration = false;
            SentimentDecorationItem = null;
            SentimentDecorationStampItem = null;
            OnPropertyChanged(nameof(SentimentResultIsStamp));
            OnPropertyChanged(nameof(SentimentStampInkSummary));
            SelectedAdhesive = null;
            OnPropertyChanged(nameof(CurrentSentimentPartNumber));
            OnPropertyChanged(nameof(IsAddingMoreSentimentParts));
            OnPropertyChanged(nameof(CanAddMoreSentimentParts));

            // Phase-2 hub state — reset alongside legacy resets so the new UI is
            // back to a clean slate whenever sentiment config is cancelled/finished.
            SentimentInks.Clear();
            SentimentStampEmbossingPowderPicker.SelectedItem = null;
            SentimentCardstockPicker.SelectedItem        = null;
            SentimentFoilCardstockPicker.SelectedItem    = null;
            SentimentGlitterCardstockPicker.SelectedItem = null;
            SentimentGlueAdhesivePicker.SelectedItem       = null;
            SentimentFoamAdhesivePicker.SelectedItem       = null;
            SentimentTapeRunnerAdhesivePicker.SelectedItem = null;
            CurrentSentimentPieceAdhesives.Clear();
            _pendingSentimentDetails.Clear();
            SentimentPieceCardstockSaved = false;
            SentimentPieceDetailsSaved   = false;
            SentimentPieceAdhesivesSaved = false;
            SentimentSubStep = "Hub";
        }

        public void InitStampInkForDecorationItem()
        {
            StampInkColorSelections.Clear();
            foreach (var color in _inkColorOptions)
            {
                var cb = new SubtypeCheckboxItem { Label = color };
                cb.PropertyChanged += (_, _) => OnPropertyChanged(nameof(StampInkColorSummary));
                StampInkColorSelections.Add(cb);
            }
            OnPropertyChanged(nameof(ShowStampInkSection));
            OnPropertyChanged(nameof(StampInkColorSummary));
        }

        public void InitEmbossingInkForDecorationItem()
        {
            EmbossingInkColorSelections.Clear();
            foreach (var color in _embossingInkColorOptions)
            {
                var cb = new SubtypeCheckboxItem { Label = color };
                cb.PropertyChanged += (_, _) => OnPropertyChanged(nameof(EmbossingInkColorSummary));
                EmbossingInkColorSelections.Add(cb);
            }
            OnPropertyChanged(nameof(EmbossingInkColorSummary));
        }

        private void ResetCurrentLayerSelections()
        {
            StencilInkColorSelections.Clear();
            foreach (var color in _inkColorOptions)
            {
                var cb = new SubtypeCheckboxItem { Label = color };
                cb.PropertyChanged += (_, _) => OnPropertyChanged(nameof(StencilInkColorSummary));
                StencilInkColorSelections.Add(cb);
            }
            OnPropertyChanged(nameof(StencilInkColorSummary));
        }

        [RelayCommand]
        private void FinishStencilLayer()
        {
            var checkedColors = StencilInkColorSelections.Where(s => s.IsChecked).Select(s => s.Label).ToList();
            var layer = new WizardStencilLayer { LayerNumber = CurrentStencilLayerIndex, InkColors = checkedColors };
            CompletedStencilLayers.Add(layer);

            var mat = CurrentMat ?? CurrentAdditionalMat ?? CurrentInsideMat ?? CurrentInsideAdditionalMat;
            if (mat != null)
                mat.StencilInkLayers.Add(layer);
            else
                ExteriorFocal.StencilInkLayers.Add(layer);

            CurrentStencilLayerIndex++;
            if (CurrentStencilLayerIndex <= StencilTotalLayers)
                ResetCurrentLayerSelections();
            else
            {
                StencilInkColorSelections.Clear();
                OnPropertyChanged(nameof(StencilInkColorSummary));
            }

            OnPropertyChanged(nameof(ShowStencilLayerSection));
            OnPropertyChanged(nameof(StencilAllLayersDone));
            OnPropertyChanged(nameof(FinishStencilLayerButtonLabel));
        }

        private void CommitInProgressDecorationToMat(WizardBgMat mat)
        {
            if (!mat.HasDecoration || mat.DecorationItem == null) return;
            var d = new WizardMatDecoration { Item = mat.DecorationItem, StampItem = mat.DecorationStampItem };
            d.StampInkColors.AddRange(StampInkColorSelections.Where(s => s.IsChecked).Select(s => s.Label));
            d.EmbossingInkColors.AddRange(EmbossingInkColorSelections.Where(s => s.IsChecked).Select(s => s.Label));
            d.StencilInkLayers.AddRange(mat.StencilInkLayers);
            mat.Decorations.Add(d);
            mat.HasDecoration = false;
            mat.DecorationItem = null;
            mat.DecorationStampItem = null;
            mat.StencilInkLayers.Clear();
            mat.StampInkColors.Clear();
            mat.EmbossingInkColors.Clear();
        }

        private void CommitInProgressDecorationToFocal(WizardFocalSection focal)
        {
            if (!focal.HasDecoration || focal.DecorationItem == null) return;
            var d = new WizardMatDecoration { Item = focal.DecorationItem, StampItem = focal.DecorationStampItem };
            d.StampInkColors.AddRange(StampInkColorSelections.Where(s => s.IsChecked).Select(s => s.Label));
            d.EmbossingInkColors.AddRange(EmbossingInkColorSelections.Where(s => s.IsChecked).Select(s => s.Label));
            d.StencilInkLayers.AddRange(focal.StencilInkLayers);
            focal.Decorations.Add(d);
            focal.HasDecoration = false;
            focal.DecorationItem = null;
            focal.DecorationStampItem = null;
            focal.StencilInkLayers.Clear();
            focal.StampInkColors.Clear();
            focal.EmbossingInkColors.Clear();
        }

        private void ResetDecorationForm()
        {
            SelectedDecorationItemType = null;
            DecorationSubtypeFilters.Clear();
            DecorationItems = new ObservableCollection<WizardItemOption>();
            _allDecorationItems = new List<WizardItemOption>();
            SelectedDecorationStampType = null;
            DecorationStampSubtypeFilters.Clear();
            DecorationStampItems = new ObservableCollection<WizardItemOption>();
            _allDecorationStampItems = new List<WizardItemOption>();
            ResetStencilState();
        }

        [RelayCommand]
        private void ConfirmCurrentDecoration()
        {
            var mat = CurrentMat ?? CurrentAdditionalMat ?? CurrentInsideMat ?? CurrentInsideAdditionalMat;
            if (mat != null)
                CommitInProgressDecorationToMat(mat);
            else if (IsAddingFocalMat)
                CommitInProgressDecorationToFocal(ExteriorFocal);
            else if (IsConfiguringCurrentSentiment)
            {
                if (!SentimentHasDecoration || SentimentDecorationItem == null) return;
                var d = new WizardMatDecoration { Item = SentimentDecorationItem, StampItem = SentimentDecorationStampItem };
                d.StampInkColors.AddRange(StampInkColorSelections.Where(s => s.IsChecked).Select(s => s.Label));
                d.EmbossingInkColors.AddRange(EmbossingInkColorSelections.Where(s => s.IsChecked).Select(s => s.Label));
                d.StencilInkLayers.AddRange(CompletedStencilLayers);
                CurrentSentimentDecorations.Add(d);
                SentimentHasDecoration = false;
                SentimentDecorationItem = null;
                SentimentDecorationStampItem = null;
            }
            else if (IsAddingCardBaseDecoration)
            {
                CommitInProgressDecorationToFocal(CardBase);
                IsAddingCardBaseDecoration = false;
            }
            else
                CommitInProgressDecorationToFocal(InsideFocal);
            ResetDecorationForm();
            PromptAddMoreDecoration = false;
            OnPropertyChanged(nameof(BgMatDecorationActive));
            OnPropertyChanged(nameof(AdditionalMatDecorationActive));
            OnPropertyChanged(nameof(InsideBgMatDecorationActive));
            OnPropertyChanged(nameof(InsideAdditionalMatDecorationActive));
            OnPropertyChanged(nameof(InsideMatDecorationActive));
            OnPropertyChanged(nameof(MatDecorationActive));
            OnPropertyChanged(nameof(ExteriorFocalDecorationActive));
            OnPropertyChanged(nameof(InsideFocalMatDecorationActive));
            OnPropertyChanged(nameof(InsideFocalDecorationActive));
            OnPropertyChanged(nameof(SentimentDecorationModeActive));
            OnPropertyChanged(nameof(SentimentControlsEnabled));
            OnPropertyChanged(nameof(CardBaseDecorationActive));
            OnPropertyChanged(nameof(CardBaseConfirmEnabled));
            NotifyExtraDetailsDone();
        }

        [RelayCommand]
        private void RemoveMatDecoration(WizardMatDecoration decoration)
        {
            var mat = CurrentMat ?? CurrentAdditionalMat ?? CurrentInsideMat ?? CurrentInsideAdditionalMat;
            if (mat != null) mat.Decorations.Remove(decoration);
            else if (IsAddingFocalMat) ExteriorFocal.Decorations.Remove(decoration);
            else if (IsConfiguringCurrentSentiment) CurrentSentimentDecorations.Remove(decoration);
            else if (IsAddingCardBaseDecoration) CardBase.Decorations.Remove(decoration);
            else InsideFocal.Decorations.Remove(decoration);
            OnPropertyChanged(nameof(BgMatDecorationActive));
            OnPropertyChanged(nameof(AdditionalMatDecorationActive));
            OnPropertyChanged(nameof(InsideBgMatDecorationActive));
            OnPropertyChanged(nameof(InsideAdditionalMatDecorationActive));
            OnPropertyChanged(nameof(InsideMatDecorationActive));
            OnPropertyChanged(nameof(MatDecorationActive));
            OnPropertyChanged(nameof(ExteriorFocalDecorationActive));
            OnPropertyChanged(nameof(InsideFocalMatDecorationActive));
            OnPropertyChanged(nameof(InsideFocalDecorationActive));
            NotifyExtraDetailsDone();
        }

        [RelayCommand]
        private void RemoveCardBaseDecoration(WizardMatDecoration decoration)
        {
            CardBase.Decorations.Remove(decoration);
            NotifyExtraDetailsDone();
        }

        [RelayCommand]
        private void CancelAddBgMat()
        {
            CurrentMat = null;
            IsAddingBgMat = false;
            ResetStencilState();
            SelectedAdhesive = null;
            // If pieces were already added to the group, go back to the "add another?" prompt
            if (_currentBgMatGroup != null && _currentBgMatGroup.Pieces.Count > 0)
                ShowAddAnotherBgMatPiecePrompt = true;
            else
                _currentBgMatGroup = null;
        }

        [RelayCommand]
        private void ConfirmAddBgMat()
        {
            if (CurrentMat == null || _currentBgMatGroup == null) return;
            CommitInProgressDecorationToMat(CurrentMat);
            CurrentMat.StampInkColors.Clear();
            CurrentMat.EmbossingInkColors.Clear();
            CurrentMat.Adhesives.Clear();
            if (SelectedAdhesive != null) CurrentMat.Adhesives.Add(SelectedAdhesive);
            CurrentMat.BlendInkColors.Clear();
            if (CurrentMat.IsSelfBlended && _blendInkClickOrder.Count > 0)
                CurrentMat.BlendInkColors.AddRange(_blendInkClickOrder);
            CurrentMat.Layer = _currentBgMatGroup.Pieces.Count + 1;
            _currentBgMatGroup.Pieces.Add(CurrentMat);
            CurrentMat = null;
            IsAddingBgMat = false;
            ResetStencilState();
            SelectedAdhesive = null;
            ShowAddAnotherBgMatPiecePrompt = true;
        }

        [RelayCommand]
        private void AddAnotherBgMatPiece()
        {
            ShowAddAnotherBgMatPiecePrompt = false;
            BgMatCardstock = true;
            BgMatFoilCardstock = false;
            BgMatGlitterCardstock = false;
            CurrentMat = new WizardBgMat { Layer = (_currentBgMatGroup?.Pieces.Count ?? 0) + 1 };
            CurrentMat.SelectedCardstockColor = CardstockColorOptions.FirstOrDefault();
            OnPropertyChanged(nameof(BgMatCardstockOptions));
            InsiderSentiments = new ObservableCollection<string>();
            ResetBlendInkSelections();
            SelectedDecorationItemType = null;
            DecorationSubtypeFilters.Clear();
            DecorationItems = new ObservableCollection<WizardItemOption>();
            _allDecorationItems = new List<WizardItemOption>();
            SelectedDecorationStampType = null;
            DecorationStampSubtypeFilters.Clear();
            DecorationStampItems = new ObservableCollection<WizardItemOption>();
            _allDecorationStampItems = new List<WizardItemOption>();
            ResetStencilState();
            InitAdhesiveSelections();
            IsAddingBgMat = true;
        }

        [RelayCommand]
        private void DoneWithBgMatPieces()
        {
            if (_currentBgMatGroup == null) return;
            BgMats.Add(_currentBgMatGroup);
            for (int i = 0; i < AdditionalMats.Count; i++) AdditionalMats[i].GroupNumber = i + 1;
            _currentBgMatGroup = null;
            ShowAddAnotherBgMatPiecePrompt = false;
            OnPropertyChanged(nameof(FocalMatNumber));
            OnPropertyChanged(nameof(FocalMatDisplaySummary));
        }

        [RelayCommand]
        private async Task EditBgMat(WizardBgMatGroup group)
        {
            BgMats.Remove(group);
            for (int i = 0; i < BgMats.Count; i++) BgMats[i].GroupNumber = i + 1;
            for (int i = 0; i < AdditionalMats.Count; i++) AdditionalMats[i].GroupNumber = i + 1;
            OnPropertyChanged(nameof(FocalMatNumber));
            OnPropertyChanged(nameof(FocalMatDisplaySummary));

            // Pop the last piece back as the current in-progress piece
            _currentBgMatGroup = group;
            var mat = group.Pieces.Count > 0 ? group.Pieces[group.Pieces.Count - 1] : new WizardBgMat { Layer = 1 };
            if (group.Pieces.Count > 0) group.Pieces.RemoveAt(group.Pieces.Count - 1);

            // Detect cardstock type from the saved color
            BgMatFoilCardstock = _foilCardstockNames.Contains(mat.SelectedCardstockColor ?? string.Empty, StringComparer.OrdinalIgnoreCase);
            BgMatGlitterCardstock = _glitterCardstockNames.Contains(mat.SelectedCardstockColor ?? string.Empty, StringComparer.OrdinalIgnoreCase);
            BgMatCardstock = !BgMatFoilCardstock && !BgMatGlitterCardstock;
            OnPropertyChanged(nameof(BgMatCardstockOptions));

            CurrentMat = mat;

            // Restore adhesive selection
            SelectedAdhesive = mat.Adhesives.FirstOrDefault();

            // Restore blend ink selections
            if (mat.IsSelfBlended && mat.BlendInkColors.Count > 0)
                RestoreBlendInkSelectionsForEdit(mat.BlendInkColors);
            else
                ResetBlendInkSelections();

            // Restore decoration state without triggering LoadDecorationItems
            SelectedDecorationItemType = null;
            DecorationSubtypeFilters.Clear();
            DecorationItems = new ObservableCollection<WizardItemOption>();
            _allDecorationItems = new List<WizardItemOption>();
            SelectedDecorationStampType = null;
            DecorationStampSubtypeFilters.Clear();
            DecorationStampItems = new ObservableCollection<WizardItemOption>();
            _allDecorationStampItems = new List<WizardItemOption>();
            ResetStencilState();

            InsiderSentiments = new ObservableCollection<string>();
            IsAddingBgMat = true;

            if (mat.HasDecoration)
                await RestoreDecorationStateForEditAsync(mat.DecorationItem,
                    item => mat.DecorationItem = item);
        }

        [RelayCommand]
        private void RemoveBgMat(WizardBgMatGroup group)
        {
            BgMats.Remove(group);
            for (int i = 0; i < BgMats.Count; i++) BgMats[i].GroupNumber = i + 1;
            for (int i = 0; i < AdditionalMats.Count; i++) AdditionalMats[i].GroupNumber = i + 1;
            OnPropertyChanged(nameof(FocalMatNumber));
            OnPropertyChanged(nameof(FocalMatDisplaySummary));
        }

        [RelayCommand]
        private void StartAddAdditionalMat()
        {
            _currentAdditionalMatGroup = new WizardBgMatGroup { GroupNumber = AdditionalMats.Count + 1, TypeLabel = "Additional", IsInside = IsInsideMode };
            AdditionalMatCardstock = true;
            AdditionalMatFoilCardstock = false;
            AdditionalMatGlitterCardstock = false;
            CurrentAdditionalMat = new WizardBgMat { Layer = 1 };
            CurrentAdditionalMat.SelectedCardstockColor = CardstockColorOptions.FirstOrDefault();
            OnPropertyChanged(nameof(AdditionalMatCardstockOptions));
            InsiderSentiments = new ObservableCollection<string>();
            ResetBlendInkSelections();
            SelectedDecorationItemType = null;
            DecorationSubtypeFilters.Clear();
            DecorationItems = new ObservableCollection<WizardItemOption>();
            _allDecorationItems = new List<WizardItemOption>();
            SelectedDecorationStampType = null;
            DecorationStampSubtypeFilters.Clear();
            DecorationStampItems = new ObservableCollection<WizardItemOption>();
            _allDecorationStampItems = new List<WizardItemOption>();
            ResetStencilState();
            InitAdhesiveSelections();
            IsAddingAdditionalMat = true;
        }

        [RelayCommand]
        private void CancelAddAdditionalMat()
        {
            CurrentAdditionalMat = null;
            IsAddingAdditionalMat = false;
            ResetStencilState();
            SelectedAdhesive = null;
            if (_currentAdditionalMatGroup != null && _currentAdditionalMatGroup.Pieces.Count > 0)
                ShowAddAnotherAdditionalMatPiecePrompt = true;
            else
                _currentAdditionalMatGroup = null;
        }

        [RelayCommand]
        private void ConfirmAddAdditionalMat()
        {
            if (CurrentAdditionalMat == null || _currentAdditionalMatGroup == null) return;
            CommitInProgressDecorationToMat(CurrentAdditionalMat);
            CurrentAdditionalMat.StampInkColors.Clear();
            CurrentAdditionalMat.EmbossingInkColors.Clear();
            CurrentAdditionalMat.Adhesives.Clear();
            if (SelectedAdhesive != null) CurrentAdditionalMat.Adhesives.Add(SelectedAdhesive);
            CurrentAdditionalMat.BlendInkColors.Clear();
            if (CurrentAdditionalMat.IsSelfBlended && _blendInkClickOrder.Count > 0)
                CurrentAdditionalMat.BlendInkColors.AddRange(_blendInkClickOrder);
            CurrentAdditionalMat.Layer = _currentAdditionalMatGroup.Pieces.Count + 1;
            _currentAdditionalMatGroup.Pieces.Add(CurrentAdditionalMat);
            CurrentAdditionalMat = null;
            IsAddingAdditionalMat = false;
            ResetStencilState();
            SelectedAdhesive = null;
            ShowAddAnotherAdditionalMatPiecePrompt = true;
        }

        [RelayCommand]
        private void AddAnotherAdditionalMatPiece()
        {
            ShowAddAnotherAdditionalMatPiecePrompt = false;
            AdditionalMatCardstock = true;
            AdditionalMatFoilCardstock = false;
            AdditionalMatGlitterCardstock = false;
            CurrentAdditionalMat = new WizardBgMat { Layer = (_currentAdditionalMatGroup?.Pieces.Count ?? 0) + 1 };
            CurrentAdditionalMat.SelectedCardstockColor = CardstockColorOptions.FirstOrDefault();
            OnPropertyChanged(nameof(AdditionalMatCardstockOptions));
            InsiderSentiments = new ObservableCollection<string>();
            ResetBlendInkSelections();
            SelectedDecorationItemType = null;
            DecorationSubtypeFilters.Clear();
            DecorationItems = new ObservableCollection<WizardItemOption>();
            _allDecorationItems = new List<WizardItemOption>();
            SelectedDecorationStampType = null;
            DecorationStampSubtypeFilters.Clear();
            DecorationStampItems = new ObservableCollection<WizardItemOption>();
            _allDecorationStampItems = new List<WizardItemOption>();
            ResetStencilState();
            InitAdhesiveSelections();
            IsAddingAdditionalMat = true;
        }

        [RelayCommand]
        private void DoneWithAdditionalMatPieces()
        {
            if (_currentAdditionalMatGroup == null) return;
            AdditionalMats.Add(_currentAdditionalMatGroup);
            for (int i = 0; i < AdditionalMats.Count; i++) AdditionalMats[i].GroupNumber = i + 1;
            _currentAdditionalMatGroup = null;
            ShowAddAnotherAdditionalMatPiecePrompt = false;
            OnPropertyChanged(nameof(FocalMatNumber));
            OnPropertyChanged(nameof(FocalMatDisplaySummary));
        }

        [RelayCommand]
        private void RemoveAdditionalMat(WizardBgMatGroup group)
        {
            AdditionalMats.Remove(group);
            for (int i = 0; i < AdditionalMats.Count; i++) AdditionalMats[i].GroupNumber = i + 1;
            OnPropertyChanged(nameof(FocalMatNumber));
            OnPropertyChanged(nameof(FocalMatDisplaySummary));
        }

        [RelayCommand]
        private async Task LoadInsiderSentiments(WizardItemOption? item)
        {
            if (item == null) return;
            var lines = await _service.GetItemSentimentLinesAsync(item.Id);
            InsiderSentiments = new ObservableCollection<string>(lines);
        }

        [RelayCommand]
        private async Task ConfirmSection3()
        {
            // Clear shared decoration state (used by mat forms)
            SelectedDecorationItemType = null;
            DecorationSubtypeFilters.Clear();
            DecorationItems = new ObservableCollection<WizardItemOption>();
            _allDecorationItems = new List<WizardItemOption>();
            SelectedDecorationStampType = null;
            DecorationStampSubtypeFilters.Clear();
            DecorationStampItems = new ObservableCollection<WizardItemOption>();
            _allDecorationStampItems = new List<WizardItemOption>();
            // Focal mat is now inline in this section - skip Sec4 in the chain
            // Reset sentiment state for Section 3 (Sentiments)
            SentimentResults.Clear();
            SentimentSearchQuery = string.Empty;
            ResetSentimentConfigState();
        }

        // EditSection3 removed (legacy form has no Edit button anymore).

        // ── Focal Mat inline (lives inside the Mats section) ─────────────────

        [RelayCommand]
        private void StartAddFocalMat()
        {
            FocalCardstock = true;
            FocalFoilCardstock = false;
            FocalGlitterCardstock = false;
            ExteriorFocal.SelectedCardstockColor = CardstockColorOptions.FirstOrDefault();
            ExteriorFocal.CuttingMethod = "Stacklet";   // default to Stacklet so subtypes auto-appear
            ExteriorFocal.IsSelfBlended = false;
            ExteriorFocal.SelfBlendDescription = string.Empty;
            ExteriorFocal.BlendInkColors.Clear();
            OnPropertyChanged(nameof(FocalCardstockOptions));
            ResetBlendInkSelections();
            InitAdhesiveSelections();
            IsAddingFocalMat = true;
        }

        [RelayCommand]
        private void ConfirmFocalMat()
        {
            CommitInProgressDecorationToFocal(ExteriorFocal);
            ExteriorFocal.StampInkColors.Clear();
            ExteriorFocal.EmbossingInkColors.Clear();
            ExteriorFocal.Adhesives.Clear();
            if (SelectedAdhesive != null) ExteriorFocal.Adhesives.Add(SelectedAdhesive);
            ExteriorFocal.BlendInkColors.Clear();
            if (ExteriorFocal.IsSelfBlended && _blendInkClickOrder.Count > 0)
                ExteriorFocal.BlendInkColors.AddRange(_blendInkClickOrder);
            ExteriorFocal.PartNumber = FocalParts.Count + 1;
            FocalParts.Add(ExteriorFocal);
            ExteriorFocal = new WizardFocalSection();
            ResetStencilState();
            SelectedAdhesive = null;
            IsAddingFocalMat = false;
            ShowAddAnotherFocalPartPrompt = true;
        }

        [RelayCommand]
        private void CancelAddFocalMat()
        {
            SelectedAdhesive = null;
            IsAddingFocalMat = false;
        }

        [RelayCommand]
        private void AddAnotherFocalPart()
        {
            ShowAddAnotherFocalPartPrompt = false;
            StartAddFocalMat();
        }

        [RelayCommand]
        private void DoneWithFocalParts()
        {
            ShowAddAnotherFocalPartPrompt = false;
        }

        [RelayCommand]
        private void RemoveFocalPart(WizardFocalSection part)
        {
            FocalParts.Remove(part);
            // Re-number remaining parts
            for (int i = 0; i < FocalParts.Count; i++) FocalParts[i].PartNumber = i + 1;
        }

        [RelayCommand]
        private async Task EditFocalMat()
        {
            // Pop last piece back as the current in-progress piece
            if (FocalParts.Count > 0)
            {
                ExteriorFocal = FocalParts[FocalParts.Count - 1];
                FocalParts.RemoveAt(FocalParts.Count - 1);
            }
            InitAdhesiveSelections();
            IsAddingFocalMat = true;
            ShowAddAnotherFocalPartPrompt = false;

            if (ExteriorFocal.IsSelfBlended && ExteriorFocal.BlendInkColors.Count > 0)
                RestoreBlendInkSelectionsForEdit(ExteriorFocal.BlendInkColors);
            else
                ResetBlendInkSelections();

            if (ExteriorFocal.HasDecoration)
                await RestoreDecorationStateForEditAsync(ExteriorFocal.DecorationItem,
                    item => ExteriorFocal.DecorationItem = item);
        }

        [RelayCommand]
        private void RemoveFocalMat()
        {
            FocalParts.Clear();
            FocalCardstock = true;
            FocalFoilCardstock = false;
            FocalGlitterCardstock = false;
            ExteriorFocal.SelectedCardstockColor = CardstockColorOptions.FirstOrDefault();
            ExteriorFocal.CuttingMethod = "Stacklet";
            OnPropertyChanged(nameof(FocalCardstockOptions));
            ExteriorFocal.StampInkColors.Clear();
            ExteriorFocal.EmbossingInkColors.Clear();
            ExteriorFocal.Adhesives.Clear();
            ExteriorFocal.HasDecoration = false;
            ExteriorFocal.DecorationItem = null;
            ExteriorFocal.DecorationStampItem = null;
            ExteriorFocal.Decorations.Clear();
            IsAddingFocalMat = false;
        }

        // ── Section 5: Sentiments ─────────────────────────────────────────────

        [RelayCommand]
        private async Task SearchSentiments()
        {
            if (string.IsNullOrWhiteSpace(SentimentSearchQuery)) return;
            IsSentimentSearching = true;
            try
            {
                // Theme search: typed query is treated as a theme name (subtype). Bypasses
                // sentiment-OCR matching and queries items by subtype directly.
                if (SentimentFilterThemeSearch)
                {
                    await SearchSentimentsByThemeAsync(SentimentSearchQuery);
                    // Re-apply type filter on results (Dies/Stamps are post-filters here too).
                    if (SentimentFilterDies || SentimentFilterStamps)
                    {
                        IEnumerable<WizardSentimentSelection> filteredTheme = SentimentResults;
                        if (SentimentFilterDies && !SentimentFilterStamps)
                            filteredTheme = filteredTheme.Where(s => s.ItemType?.Contains("Die", StringComparison.OrdinalIgnoreCase) ?? false);
                        else if (SentimentFilterStamps && !SentimentFilterDies)
                            filteredTheme = filteredTheme.Where(s => s.ItemType?.Contains("Stamp", StringComparison.OrdinalIgnoreCase) ?? false);
                        SentimentResults = new ObservableCollection<WizardSentimentSelection>(filteredTheme);
                    }
                    return;
                }

                // Literal-text search. "Full sets" expands matched results to include all
                // sentiments from every matching set (matched ones appear first within each set).
                var sentimentImages = SentimentFilterFullSets
                    ? await _sentimentService.SearchSentimentsExpandedAsync(SentimentSearchQuery)
                    : await _sentimentService.SearchSentimentsAsync(SentimentSearchQuery);

                // Apply type filter (Dies/Stamps are combinable per the user's spec — checking
                // both shows both kinds, checking neither shows everything).
                IEnumerable<MyCraftyStash.Models.SentimentImage> filtered = sentimentImages;
                if (SentimentFilterDies && !SentimentFilterStamps)
                    filtered = sentimentImages.Where(s => s.Item?.Type?.Contains("Die", StringComparison.OrdinalIgnoreCase) ?? false);
                else if (SentimentFilterStamps && !SentimentFilterDies)
                    filtered = sentimentImages.Where(s => s.Item?.Type?.Contains("Stamp", StringComparison.OrdinalIgnoreCase) ?? false);

                // Always show one row per individual clipped sentiment. The checkboxes
                // (Die only / Stamp only / Full sets / Theme) restrict WHICH sentiments
                // are returned — the result granularity stays per-sentiment regardless.
                // Full Sets ON  → service expanded to include every sentiment in matching sets.
                // Full Sets OFF → service returns only sentiments whose extracted text matched.
                var results = filtered.Select(s => new WizardSentimentSelection
                {
                    ItemId          = s.ItemId,
                    ItemName        = s.Item?.Name ?? "Unknown",
                    ItemType        = s.Item?.Type,
                    ThumbnailBase64 = s.ImageData,           // clipped sentiment image, not parent item thumbnail
                    SentimentPreview = s.ExtractedText,
                });
                SentimentResults = new ObservableCollection<WizardSentimentSelection>(results);
            }
            finally { IsSentimentSearching = false; }
        }

        private async Task SearchSentimentsByThemeAsync(string theme)
        {
            IsSentimentThemeSearching = true;
            try
            {
                var items = await _service.GetWizardSentimentItemsByThemeAsync(theme);
                var results = items.Select(i => new WizardSentimentSelection
                {
                    ItemId = i.Id,
                    ItemName = i.Name,
                    ItemType = i.ItemType,
                    Subtype = i.Subtype,
                    SentimentPreview = i.Subtype ?? string.Empty,
                });
                SentimentResults = new ObservableCollection<WizardSentimentSelection>(results);
            }
            finally { IsSentimentThemeSearching = false; }
        }

        [RelayCommand]
        private void SelectSentimentResult(WizardSentimentSelection result)
        {
            CurrentSentimentResult = result;
            SentimentCardstock = true;
            SentimentFoilCardstock = false;
            SentimentGlitterCardstock = false;
            OnPropertyChanged(nameof(SentimentCardstockOptions));
            SentimentConfigCardstockColor = SentimentCardstockOptions.FirstOrDefault();
            SentimentIsSelfBlended = false;
            SentimentSelfBlendDescription = string.Empty;
            SentimentPickBlendInkColors = false;
            SentimentPickWatercolors = false;
            SentimentIsEmbossed = false;
            SentimentEmbossingPowder = null;
            SentimentStampInkSelections.Clear();
            ResetBlendInkSelections();
            OnPropertyChanged(nameof(SentimentResultIsStamp));
            if (SentimentResultIsStamp)
            {
                foreach (var color in _inkColorOptions)
                {
                    var cb = new SubtypeCheckboxItem { Label = color };
                    cb.PropertyChanged += (_, _) => OnPropertyChanged(nameof(SentimentStampInkSummary));
                    SentimentStampInkSelections.Add(cb);
                }
                OnPropertyChanged(nameof(SentimentStampInkSummary));
            }
            SentimentHasDecoration = false;
            SentimentDecorationItem = null;
            SentimentDecorationStampItem = null;
            CurrentSentimentDecorations.Clear();
            InitAdhesiveSelections();
            ShowSentimentMorePartsQuestion = false;

            // Phase-2 hub state: clear per-piece state when a fresh sentiment is picked.
            SentimentInks.Clear();
            SentimentStampEmbossingPowderPicker.SelectedItem = null;
            SentimentCardstockPicker.SelectedItem        = null;
            SentimentFoilCardstockPicker.SelectedItem    = null;
            SentimentGlitterCardstockPicker.SelectedItem = null;
            SentimentGlueAdhesivePicker.SelectedItem       = null;
            SentimentFoamAdhesivePicker.SelectedItem       = null;
            SentimentTapeRunnerAdhesivePicker.SelectedItem = null;
            CurrentSentimentPieceAdhesives.Clear();
            _pendingSentimentDetails.Clear();
            SentimentPieceCardstockSaved = false;
            SentimentPieceDetailsSaved   = false;
            SentimentPieceAdhesivesSaved = false;
            SentimentSubStep = "Hub";

            IsConfiguringCurrentSentiment = true;
        }

        [RelayCommand]
        private void CancelSentimentConfig()
        {
            ResetSentimentConfigState();
        }

        [RelayCommand]
        private void ConfirmSentimentPart()
        {
            if (CurrentSentimentResult == null) return;
            var part = new WizardConfiguredSentimentPart
            {
                ItemId = CurrentSentimentResult.ItemId,
                ItemName = CurrentSentimentResult.ItemName,
                ThumbnailBase64 = CurrentSentimentResult.ThumbnailBase64,
                IsStampType = SentimentResultIsStamp,
                CardstockColor = SentimentEffectiveCardstockColor,
                IsSelfBlended = SentimentIsSelfBlended,
                SelfBlendDescription = SentimentSelfBlendDescription,
                IsEmbossed = SentimentIsEmbossed,
                EmbossingPowderName = SentimentEmbossingPowder?.Name,
                EmbossingPowderItemId = SentimentEmbossingPowder?.Id,
            };
            if (SentimentIsSelfBlended && (SentimentPickBlendInkColors || SentimentPickWatercolors))
                part.BlendInkColors.AddRange(_blendInkClickOrder);
            part.StampInkColors.AddRange(SentimentStampInkSelections.Where(s => s.IsChecked).Select(s => s.Label));
            if (SelectedAdhesive != null) part.Adhesives.Add(SelectedAdhesive);
            // Commit any in-progress sentiment decoration before finalizing the part
            if (SentimentHasDecoration && SentimentDecorationItem != null)
            {
                var d = new WizardMatDecoration { Item = SentimentDecorationItem, StampItem = SentimentDecorationStampItem };
                d.StampInkColors.AddRange(StampInkColorSelections.Where(s => s.IsChecked).Select(s => s.Label));
                d.EmbossingInkColors.AddRange(EmbossingInkColorSelections.Where(s => s.IsChecked).Select(s => s.Label));
                d.StencilInkLayers.AddRange(CompletedStencilLayers);
                CurrentSentimentDecorations.Add(d);
            }
            foreach (var sd in CurrentSentimentDecorations) part.Decorations.Add(sd);
            CurrentSentimentDecorations.Clear();
            SentimentHasDecoration = false;
            SentimentDecorationItem = null;
            SentimentDecorationStampItem = null;
            ResetDecorationForm();
            _currentSentimentParts.Add(part);

            // Capture "Other" cardstock note before fields are cleared
            if (SentimentConfigCardstockColor == "Other" && !string.IsNullOrWhiteSpace(SentimentOtherCardstockText))
                _sentimentOtherNotes.Add($"Sentiment \"{part.ItemName}\" cardstock: {SentimentOtherCardstockText}");

            // Reset per-part config state (keep _currentSentimentParts intact)
            CurrentSentimentResult = null;
            SentimentCardstock = true;
            SentimentFoilCardstock = false;
            SentimentGlitterCardstock = false;
            SentimentConfigCardstockColor = null;
            SentimentIsEmbossed = false;
            SentimentEmbossingPowder = null;
            SentimentStampInkSelections.Clear();
            OnPropertyChanged(nameof(SentimentResultIsStamp));
            OnPropertyChanged(nameof(SentimentStampInkSummary));
            SelectedAdhesive = null;
            OnPropertyChanged(nameof(CurrentSentimentPartNumber));
            OnPropertyChanged(nameof(IsAddingMoreSentimentParts));
            OnPropertyChanged(nameof(CanAddMoreSentimentParts));

            IsConfiguringCurrentSentiment = false;
            // If we've hit the 4-part limit auto-finalize, otherwise ask
            if (_currentSentimentParts.Count >= 4)
                FinalizeSentiment();
            else
                ShowSentimentMorePartsQuestion = true;
        }

        [RelayCommand]
        private void AddAnotherSentimentPiece()
        {
            ShowSentimentMorePartsQuestion = false;
            // Keep SentimentSearchQuery so the user doesn't have to retype for the same set
            SentimentResults.Clear();
            OnPropertyChanged(nameof(CurrentSentimentPartNumber));
            OnPropertyChanged(nameof(IsAddingMoreSentimentParts));
        }

        [RelayCommand]
        private void FinalizeSentiment()
        {
            var sentiment = new WizardConfiguredSentiment { IsInside = IsInsideMode };
            sentiment.Parts.AddRange(_currentSentimentParts);
            ConfiguredSentiments.Add(sentiment);
            _currentSentimentParts.Clear();
            ShowSentimentMorePartsQuestion = false;
            OnPropertyChanged(nameof(CurrentSentimentPartNumber));
            OnPropertyChanged(nameof(IsAddingMoreSentimentParts));
            OnPropertyChanged(nameof(CanAddMoreSentimentParts));
            SentimentSearchQuery = string.Empty;
            SentimentResults.Clear();
            ShowAddAnotherSentimentPrompt = true;
        }

        [RelayCommand]
        private void AddAnotherSentiment()
        {
            ShowAddAnotherSentimentPrompt = false;
            SentimentSearchQuery = string.Empty;
            SentimentResults.Clear();
        }

        [RelayCommand]
        private async Task DoneWithSentiments()
        {
            ShowAddAnotherSentimentPrompt = false;
            await LoadEmbellishmentsAsync();
        }

        [RelayCommand]
        private void RemoveConfiguredSentiment(WizardConfiguredSentiment s) => ConfiguredSentiments.Remove(s);

        [RelayCommand]
        private void EditSection5()
        {
            ResetSentimentConfigState();
        }

        // ── Section 6: Embellishments ─────────────────────────────────────────

        private async Task LoadEmbellishmentsAsync()
        {
            if (EmbellishmentsLoaded) return;
            var items = await CardLabelMappingService.Default.GetItemsForLabelAsync("Embellishments", _service);
            _allEmbellishmentItemsFlat = items;
            EmbellishmentSubtypeFilters.Clear();
            var subtypes = UserSettingsService.GetSubtypesForType("Embellishment");
            foreach (var s in subtypes)
            {
                var cb = new SubtypeCheckboxItem { Label = s };
                cb.PropertyChanged += (_, _) => ApplyEmbellishmentSubtypeFilter();
                EmbellishmentSubtypeFilters.Add(cb);
            }
            OnPropertyChanged(nameof(HasEmbellishmentSubtypeFilters));
            EmbellishmentsLoaded = true;
        }

        private void ApplyEmbellishmentSubtypeFilter()
        {
            var selected = EmbellishmentSubtypeFilters.Where(s => s.IsChecked).Select(s => s.Label).ToList();
            EmbellishmentItemsForSubtype = new ObservableCollection<WizardItemOption>(
                SortWithSelectedSubtypesFirst(_allEmbellishmentItemsFlat, selected, (i, sel) => CountSubtypeMatches(i.Subtype, sel)));
            NewEmbellishmentItem = null;
        }

        [RelayCommand]
        private void StartAddEmbellishment()
        {
            NewEmbellishmentItem = null;
            NewEmbellishmentStampItem = null;
            ApplyEmbellishmentSubtypeFilter();
            IsAddingEmbellishment = true;
        }

        [RelayCommand]
        private void CancelAddEmbellishment()
        {
            IsAddingEmbellishment = false;
        }

        [RelayCommand]
        private void ConfirmAddEmbellishment()
        {
            if (NewEmbellishmentItem == null) return;
            AddedEmbellishments.Add(new WizardEmbellishment
            {
                ItemId = NewEmbellishmentItem.Id,
                ItemName = NewEmbellishmentItem.Name,
                Subtype = NewEmbellishmentItem.Subtype,
                StampItemId = NewEmbellishmentStampItem?.Id,
                StampItemName = NewEmbellishmentStampItem?.Name
            });
            IsAddingEmbellishment = false;
        }

        [RelayCommand]
        private void RemoveEmbellishment(WizardEmbellishment item) => AddedEmbellishments.Remove(item);

        // ── Main hub Embellishments page (rebuilt to match Details tab UX) ────
        // Reuses the EmbellishmentsPicker + EmbellEmbossingInks + EmbellEmbossingStampPicker
        // from the Details tab so the data sources and embossing-powder follow-up are
        // identical. Save commands capture into AddedEmbellishments (the general-card
        // collection) and clear the pickers for the next entry.

        public bool HasHubEmbellishmentPick => EmbellishmentsPicker.SelectedItem != null;

        public string CurrentHubEmbellishmentPreview
        {
            get
            {
                if (EmbellishmentsPicker.SelectedItem == null) return string.Empty;
                var s = EmbellishmentsPicker.SelectedItem.Name;
                if (ShowEmbellEmbossingFollowups)
                {
                    var inks = EmbellEmbossingInks.Ordered;
                    if (inks.Count > 0) s += $" • Inks: {string.Join(", ", inks)}";
                    if (EmbellEmbossingStampPicker.SelectedItem != null)
                        s += $" • Stamp: {EmbellEmbossingStampPicker.SelectedItem.Name}";
                }
                return s;
            }
        }

        private void CaptureHubEmbellishment()
        {
            var picked = EmbellishmentsPicker.SelectedItem;
            if (picked == null) return;
            var entry = new WizardEmbellishment
            {
                ItemId        = picked.Id,
                ItemName      = picked.Name,
                Subtype       = picked.Subtype,
                StampItemId   = ShowEmbellEmbossingFollowups ? EmbellEmbossingStampPicker.SelectedItem?.Id   : null,
                StampItemName = ShowEmbellEmbossingFollowups ? EmbellEmbossingStampPicker.SelectedItem?.Name : null,
                IsInside      = IsInsideMode,
            };
            if (ShowEmbellEmbossingFollowups)
                entry.InkColors.AddRange(EmbellEmbossingInks.Ordered);
            AddedEmbellishments.Add(entry);
        }

        private void ClearHubEmbellishmentPickers()
        {
            EmbellishmentsPicker.SelectedItem = null;
            EmbellEmbossingStampPicker.SelectedItem = null;
            EmbellEmbossingInks.Clear();
            OnPropertyChanged(nameof(HasHubEmbellishmentPick));
            OnPropertyChanged(nameof(CurrentHubEmbellishmentPreview));
        }

        [RelayCommand]
        private void SaveHubEmbellishmentAndAddAnother()
        {
            if (EmbellishmentsPicker.SelectedItem == null) return;
            CaptureHubEmbellishment();
            EmbellishmentsSaved = true;
            ClearHubEmbellishmentPickers();
            UpdateSummaryLines();
            // stay on Embellishments page
        }

        [RelayCommand]
        private void SaveHubEmbellishmentAndReturn()
        {
            if (EmbellishmentsPicker.SelectedItem != null)
            {
                CaptureHubEmbellishment();
                EmbellishmentsSaved = true;
            }
            else if (AddedEmbellishments.Count > 0)
            {
                EmbellishmentsSaved = true;
            }
            ClearHubEmbellishmentPickers();
            UpdateSummaryLines();
            CurrentSection = "Hub";
        }

        [RelayCommand]
        private async Task ConfirmSection6()
        {
            IsAddingEmbellishment = false;
            InitAdhesiveSelections();
            InsideFocalCardstock = true;
            InsideFocalFoilCardstock = false;
            InsideFocalGlitterCardstock = false;
            InsideFocal.SelectedCardstockColor = CardstockColorOptions.FirstOrDefault();
            InsideFocal.IsSelfBlended = false;
            InsideFocal.SelfBlendDescription = string.Empty;
            InsideFocal.BlendInkColors.Clear();
            OnPropertyChanged(nameof(InsideFocalCardstockOptions));
            ResetBlendInkSelections();
        }

        // EditSection6 removed (legacy form has no Edit button anymore).

        // ── Section 7: Inside ─────────────────────────────────────────────────

        [RelayCommand]
        private async Task SetHasInside(string value)
        {
            var boolValue = bool.Parse(value);
            HasInside = boolValue;
            if (!boolValue)
            {
            }
        }

        [RelayCommand]
        private void StartAddInsideBgMat() => StartAddInsideBgMatFull();

        [RelayCommand]
        private void CancelAddInsideBgMat() { CurrentInsideMat = null; IsAddingInsideBgMat = false; ResetStencilState(); SelectedAdhesive = null; }

        [RelayCommand]
        private void ConfirmAddInsideBgMat() => ConfirmAddInsideBgMatFull();

        [RelayCommand]
        private void RemoveInsideBgMat(WizardBgMat mat)
        {
            InsideBgMats.Remove(mat);
            for (int i = 0; i < InsideBgMats.Count; i++) InsideBgMats[i].Layer = i + 1;
        }

        [RelayCommand]
        private async Task SearchInsideSentiments()
        {
            if (string.IsNullOrWhiteSpace(InsideSentimentSearchQuery)) return;
            IsInsideSentimentSearching = true;
            try
            {
                // "Show full sets" expands results to all sentiments in matching sets (matches float to top)
                var sentimentImages = InsideSentimentSearchByTheme
                    ? await _sentimentService.SearchSentimentsExpandedAsync(InsideSentimentSearchQuery)
                    : await _sentimentService.SearchSentimentsAsync(InsideSentimentSearchQuery);

                // Apply Dies-only / Stamps-only type filter
                IEnumerable<MyCraftyStash.Models.SentimentImage> filtered = sentimentImages;
                if (InsideSentimentFilterDies && !InsideSentimentFilterStamps)
                    filtered = sentimentImages.Where(s => s.Item?.Type?.Contains("Die", StringComparison.OrdinalIgnoreCase) ?? false);
                else if (InsideSentimentFilterStamps && !InsideSentimentFilterDies)
                    filtered = sentimentImages.Where(s => s.Item?.Type?.Contains("Stamp", StringComparison.OrdinalIgnoreCase) ?? false);

                // Always one row per individual clipped sentiment — checkboxes filter
                // which sentiments are returned, not the row granularity.
                var results = filtered.Select(s => new WizardSentimentSelection
                {
                    ItemId           = s.ItemId,
                    ItemName         = s.Item?.Name ?? "Unknown",
                    ItemType         = s.Item?.Type,
                    ThumbnailBase64  = s.ImageData,
                    SentimentPreview = s.ExtractedText,
                });
                InsideSentimentResults = new ObservableCollection<WizardSentimentSelection>(results);
            }
            finally { IsInsideSentimentSearching = false; }
        }

        [RelayCommand]
        private async Task ConfirmSection7()
        {
            // If user left a mat/focal/embellishment form open, ignore the in-progress data
            // (they'd need to explicitly confirm it). We just close any open form here.
            CurrentInsideMat = null;
            IsAddingInsideBgMat = false;
            CurrentInsideAdditionalMat = null;
            IsAddingInsideAdditionalMat = false;
            IsAddingInsideFocalMat = false;
            IsAddingInsideEmbellishment = false;
            NewInsideEmbellishmentItem = null;
            ResetStencilState();
            SelectedAdhesive = null;
        }

        [RelayCommand]
        private async Task EditSection7()
        {
            if (InsideFocal.HasDecoration)
                await RestoreDecorationStateForEditAsync(InsideFocal.DecorationItem,
                    item => InsideFocal.DecorationItem = item);
        }

        // Cancel button next to "Confirm Inside" - un-answers the Yes/No prompt
        // and discards any inside data the user had started entering.
        [RelayCommand]
        private void CancelSection7()
        {
            HasInside = null;
            InsideBgMats.Clear();
            InsideAdditionalMats.Clear();
            InsideAddedEmbellishments.Clear();
            ConfiguredInsideSentiments.Clear();
            CurrentInsideSentimentResult = null;
            IsConfiguringCurrentInsideSentiment = false;
            InsideFocal = new WizardFocalSection();
            HasInsideFocalMat = false;
            CurrentInsideMat = null;
            CurrentInsideAdditionalMat = null;
            IsAddingInsideBgMat = false;
            IsAddingInsideAdditionalMat = false;
            IsAddingInsideFocalMat = false;
            IsAddingInsideEmbellishment = false;
            NewInsideEmbellishmentItem = null;
            ResetDecorationForm();
            ResetStencilState();
            SelectedAdhesive = null;
            InsideSentimentSearchQuery = string.Empty;
            InsideSentimentResults.Clear();
        }

        // ── Inside Background Mat (full form) ─────────────────────────────────
        [RelayCommand]
        private void StartAddInsideBgMatFull()
        {
            if (InsideBgMats.Count >= 5) return;
            // Reset cardstock toggles so the dropdown is repopulated with cardstock colors (mirrors outside StartAddBgMat)
            InsideBgMatCardstock = true;
            InsideBgMatFoilCardstock = false;
            InsideBgMatGlitterCardstock = false;
            CurrentInsideMat = new WizardBgMat { Layer = InsideBgMats.Count + 1 };
            CurrentInsideMat.SelectedCardstockColor = CardstockColorOptions.FirstOrDefault();
            OnPropertyChanged(nameof(InsideBgMatCardstockOptions));
            InsiderSentiments = new ObservableCollection<string>();
            ResetDecorationForm();
            ResetBlendInkSelections();
            SelectedAdhesive = null;
            IsAddingInsideBgMat = true;
        }

        [RelayCommand]
        private void ConfirmAddInsideBgMatFull()
        {
            if (CurrentInsideMat == null) return;
            // Capture decoration in progress
            CommitInProgressDecorationToMat(CurrentInsideMat);
            // Capture adhesive
            CurrentInsideMat.Adhesives.Clear();
            if (SelectedAdhesive != null) CurrentInsideMat.Adhesives.Add(SelectedAdhesive);
            // Capture blend inks
            CurrentInsideMat.BlendInkColors.Clear();
            if (CurrentInsideMat.IsSelfBlended && _blendInkClickOrder.Count > 0)
                CurrentInsideMat.BlendInkColors.AddRange(_blendInkClickOrder);
            InsideBgMats.Add(CurrentInsideMat);
            CurrentInsideMat = null;
            IsAddingInsideBgMat = false;
            ResetStencilState();
            SelectedAdhesive = null;
        }

        [RelayCommand]
        private void EditInsideBgMat(WizardBgMat mat)
        {
            if (mat == null) return;
            InsideBgMats.Remove(mat);
            CurrentInsideMat = mat;
            ResetDecorationForm();
            if (mat.IsSelfBlended && mat.BlendInkColors.Count > 0)
                RestoreBlendInkSelectionsForEdit(mat.BlendInkColors);
            else
                ResetBlendInkSelections();
            SelectedAdhesive = mat.Adhesives.FirstOrDefault();
            IsAddingInsideBgMat = true;
        }

        // ── Inside Additional Mat ─────────────────────────────────────────────
        [RelayCommand]
        private void StartAddInsideAdditionalMat()
        {
            if (InsideAdditionalMats.Count >= 5) return;
            InsideAdditionalMatCardstock = true;
            InsideAdditionalMatFoilCardstock = false;
            InsideAdditionalMatGlitterCardstock = false;
            CurrentInsideAdditionalMat = new WizardBgMat { Layer = InsideAdditionalMats.Count + 1 };
            CurrentInsideAdditionalMat.SelectedCardstockColor = CardstockColorOptions.FirstOrDefault();
            OnPropertyChanged(nameof(InsideAdditionalMatCardstockOptions));
            InsiderSentiments = new ObservableCollection<string>();
            ResetDecorationForm();
            ResetBlendInkSelections();
            SelectedAdhesive = null;
            IsAddingInsideAdditionalMat = true;
        }

        [RelayCommand]
        private void ConfirmAddInsideAdditionalMat()
        {
            if (CurrentInsideAdditionalMat == null) return;
            CommitInProgressDecorationToMat(CurrentInsideAdditionalMat);
            CurrentInsideAdditionalMat.Adhesives.Clear();
            if (SelectedAdhesive != null) CurrentInsideAdditionalMat.Adhesives.Add(SelectedAdhesive);
            CurrentInsideAdditionalMat.BlendInkColors.Clear();
            if (CurrentInsideAdditionalMat.IsSelfBlended && _blendInkClickOrder.Count > 0)
                CurrentInsideAdditionalMat.BlendInkColors.AddRange(_blendInkClickOrder);
            InsideAdditionalMats.Add(CurrentInsideAdditionalMat);
            CurrentInsideAdditionalMat = null;
            IsAddingInsideAdditionalMat = false;
            ResetStencilState();
            SelectedAdhesive = null;
        }

        [RelayCommand]
        private void CancelAddInsideAdditionalMat()
        {
            CurrentInsideAdditionalMat = null;
            IsAddingInsideAdditionalMat = false;
            ResetStencilState();
            SelectedAdhesive = null;
        }

        [RelayCommand]
        private void RemoveInsideAdditionalMat(WizardBgMat mat)
        {
            InsideAdditionalMats.Remove(mat);
            for (int i = 0; i < InsideAdditionalMats.Count; i++) InsideAdditionalMats[i].Layer = i + 1;
        }

        [RelayCommand]
        private void EditInsideAdditionalMat(WizardBgMat mat)
        {
            if (mat == null) return;
            InsideAdditionalMats.Remove(mat);
            CurrentInsideAdditionalMat = mat;
            ResetDecorationForm();
            if (mat.IsSelfBlended && mat.BlendInkColors.Count > 0)
                RestoreBlendInkSelectionsForEdit(mat.BlendInkColors);
            else
                ResetBlendInkSelections();
            SelectedAdhesive = mat.Adhesives.FirstOrDefault();
            IsAddingInsideAdditionalMat = true;
        }

        // ── Inside Focal Mat (button-driven) ──────────────────────────────────
        [RelayCommand]
        private void StartAddInsideFocalMat()
        {
            InsideFocal = new WizardFocalSection();
            InsideFocal.SelectedCardstockColor = InsideFocalCardstockOptions.FirstOrDefault();
            InsideFocalCardstock = true;
            InsideFocalFoilCardstock = false;
            InsideFocalGlitterCardstock = false;
            ResetDecorationForm();
            ResetBlendInkSelections();
            SelectedAdhesive = null;
            IsAddingInsideFocalMat = true;
        }

        [RelayCommand]
        private void ConfirmAddInsideFocalMat()
        {
            CommitInProgressDecorationToFocal(InsideFocal);
            InsideFocal.StampInkColors.Clear();
            InsideFocal.EmbossingInkColors.Clear();
            InsideFocal.Adhesives.Clear();
            if (SelectedAdhesive != null) InsideFocal.Adhesives.Add(SelectedAdhesive);
            InsideFocal.BlendInkColors.Clear();
            if (InsideFocal.IsSelfBlended && _blendInkClickOrder.Count > 0)
                InsideFocal.BlendInkColors.AddRange(_blendInkClickOrder);
            HasInsideFocalMat = true;
            IsAddingInsideFocalMat = false;
            ResetStencilState();
            SelectedAdhesive = null;
        }

        [RelayCommand]
        private void CancelAddInsideFocalMat()
        {
            InsideFocal = new WizardFocalSection();
            IsAddingInsideFocalMat = false;
            ResetStencilState();
            SelectedAdhesive = null;
        }

        [RelayCommand]
        private void RemoveInsideFocalMat()
        {
            InsideFocal = new WizardFocalSection();
            HasInsideFocalMat = false;
        }

        [RelayCommand]
        private async Task EditInsideFocalMat()
        {
            HasInsideFocalMat = false;
            ResetDecorationForm();
            if (InsideFocal.IsSelfBlended && InsideFocal.BlendInkColors.Count > 0)
                RestoreBlendInkSelectionsForEdit(InsideFocal.BlendInkColors);
            else
                ResetBlendInkSelections();
            SelectedAdhesive = InsideFocal.Adhesives.FirstOrDefault();
            if (InsideFocal.HasDecoration)
                await RestoreDecorationStateForEditAsync(InsideFocal.DecorationItem,
                    item => InsideFocal.DecorationItem = item);
            IsAddingInsideFocalMat = true;
        }

        // ── Inside Embellishments ─────────────────────────────────────────────
        [RelayCommand]
        private void StartAddInsideEmbellishment()
        {
            NewInsideEmbellishmentItem = null;
            IsAddingInsideEmbellishment = true;
        }

        [RelayCommand]
        private void ConfirmAddInsideEmbellishment()
        {
            if (NewInsideEmbellishmentItem == null) return;
            InsideAddedEmbellishments.Add(new WizardEmbellishment
            {
                ItemId = NewInsideEmbellishmentItem.Id,
                ItemName = NewInsideEmbellishmentItem.Name,
                Subtype = NewInsideEmbellishmentItem.Subtype,
                StampItemId = NewInsideEmbellishmentStampItem?.Id,
                StampItemName = NewInsideEmbellishmentStampItem?.Name
            });
            NewInsideEmbellishmentItem = null;
            NewInsideEmbellishmentStampItem = null;
            IsAddingInsideEmbellishment = false;
        }

        [RelayCommand]
        private void CancelAddInsideEmbellishment()
        {
            NewInsideEmbellishmentItem = null;
            NewInsideEmbellishmentStampItem = null;
            IsAddingInsideEmbellishment = false;
        }

        [RelayCommand]
        private void RemoveInsideEmbellishment(WizardEmbellishment emb)
        {
            InsideAddedEmbellishments.Remove(emb);
        }

        // ── Inside Sentiments - per-sentiment configuration ───────────────────
        [RelayCommand]
        private void StartConfigureInsideSentiment(WizardSentimentSelection? result)
        {
            if (result == null) return;
            CurrentInsideSentimentResult = result;
            InsideSentimentCardstockOn = true;
            InsideSentimentFoilCardstockOn = false;
            InsideSentimentGlitterCardstockOn = false;
            // Force the computed options to refresh - partial-method setters above are no-ops
            // when the value is already the default, so we have to raise the notification ourselves.
            OnPropertyChanged(nameof(InsideSentimentCardstockOptions));
            // Use the master CardstockColorOptions list directly for the initial selection
            // (mirrors outside StartAddBgMat → guarantees a non-null, in-list value).
            InsideSentimentConfigCardstockColor = CardstockColorOptions.FirstOrDefault();
            InsideSentimentOtherCardstockText = string.Empty;
            SelectedAdhesive = null;
            IsConfiguringCurrentInsideSentiment = true;
        }

        [RelayCommand]
        private void ConfirmInsideSentimentConfig()
        {
            if (CurrentInsideSentimentResult == null) return;
            var part = new WizardConfiguredSentimentPart
            {
                ItemId = CurrentInsideSentimentResult.ItemId,
                ItemName = CurrentInsideSentimentResult.ItemName,
                ThumbnailBase64 = CurrentInsideSentimentResult.ThumbnailBase64,
                CardstockColor = InsideSentimentEffectiveCardstockColor
            };
            if (!string.IsNullOrEmpty(SelectedAdhesive)) part.Adhesives.Add(SelectedAdhesive);
            var configured = new WizardConfiguredSentiment();
            configured.Parts.Add(part);
            ConfiguredInsideSentiments.Add(configured);
            CurrentInsideSentimentResult = null;
            IsConfiguringCurrentInsideSentiment = false;
            SelectedAdhesive = null;
        }

        [RelayCommand]
        private void CancelInsideSentimentConfig()
        {
            CurrentInsideSentimentResult = null;
            IsConfiguringCurrentInsideSentiment = false;
            SelectedAdhesive = null;
        }

        [RelayCommand]
        private void RemoveConfiguredInsideSentiment(WizardConfiguredSentiment cs)
        {
            ConfiguredInsideSentiments.Remove(cs);
        }

        // (Legacy _sec8Expanded field + BuildConfirmationText/ConfirmationText removed
        //  — the new hub has no intermediate "Review &amp; Confirm" card.)

        /// <summary>
        /// Free-form notes the user types in the main hub textbox. Surfaced to
        /// the project page on confirm — appended to the project's Notes field
        /// alongside any auto-collected build notes.
        /// </summary>
        [ObservableProperty] private string _wizardNotes = string.Empty;

        public string BuildOtherNotes { get; private set; } = string.Empty;

        [RelayCommand]
        private void ConfirmBuild()
        {
            WasConfirmed = true;
            CardBaseType = SelectedCardBase;
            SelectedItemIds = CollectAllItemIds();
            BuildSteps = AssembleBuildSteps();
            BuildOtherNotes = CollectOtherNotes();
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private string CollectOtherNotes()
        {
            var parts = new List<string>();

            if (SelectedBaseCardstockColor == "Other" && !string.IsNullOrWhiteSpace(BaseCardstockOtherText))
                parts.Add($"Card base cardstock: {BaseCardstockOtherText}");

            foreach (var group in BgMats)
                foreach (var mat in group.Pieces)
                    if (mat.SelectedCardstockColor == "Other" && !string.IsNullOrWhiteSpace(mat.OtherCardstockText))
                        parts.Add(group.Pieces.Count == 1
                            ? $"Background Mat {group.GroupNumber} cardstock: {mat.OtherCardstockText}"
                            : $"Background Mat {group.GroupNumber} piece {mat.Layer} cardstock: {mat.OtherCardstockText}");

            foreach (var group in AdditionalMats)
                foreach (var mat in group.Pieces)
                    if (mat.SelectedCardstockColor == "Other" && !string.IsNullOrWhiteSpace(mat.OtherCardstockText))
                        parts.Add(group.Pieces.Count == 1
                            ? $"Additional Mat {group.GroupNumber} cardstock: {mat.OtherCardstockText}"
                            : $"Additional Mat {group.GroupNumber} piece {mat.Layer} cardstock: {mat.OtherCardstockText}");

            foreach (var fp in FocalParts)
            {
                if (fp.SelectedCardstockColor == "Other" && !string.IsNullOrWhiteSpace(fp.OtherCardstockText))
                    parts.Add($"Mat {BgMats.Count + AdditionalMats.Count + fp.PartNumber} (exterior focal) cardstock: {fp.OtherCardstockText}");
                if (fp.HasBacker && fp.BackerCardstockColor == "Other" && !string.IsNullOrWhiteSpace(fp.OtherBackerCardstockText))
                    parts.Add($"Mat {BgMats.Count + AdditionalMats.Count + fp.PartNumber} (exterior focal backer) cardstock: {fp.OtherBackerCardstockText}");
            }

            foreach (var mat in InsideBgMats)
                if (mat.SelectedCardstockColor == "Other" && !string.IsNullOrWhiteSpace(mat.OtherCardstockText))
                    parts.Add($"Inside background mat {mat.Layer} cardstock: {mat.OtherCardstockText}");
            foreach (var mat in InsideAdditionalMats)
                if (mat.SelectedCardstockColor == "Other" && !string.IsNullOrWhiteSpace(mat.OtherCardstockText))
                    parts.Add($"Inside additional mat {mat.Layer} cardstock: {mat.OtherCardstockText}");

            if (HasInsideFocalMat && InsideFocal.SelectedCardstockColor == "Other" && !string.IsNullOrWhiteSpace(InsideFocal.OtherCardstockText))
                parts.Add($"Inside focal cardstock: {InsideFocal.OtherCardstockText}");

            if (InsideFocal.HasBacker && InsideFocal.BackerCardstockColor == "Other" && !string.IsNullOrWhiteSpace(InsideFocal.OtherBackerCardstockText))
                parts.Add($"Inside focal backer cardstock: {InsideFocal.OtherBackerCardstockText}");

            parts.AddRange(_sentimentOtherNotes);

            var blendParts = new List<string>();

            void AddBlendNote(string label, bool isSelfBlended, string desc, List<string> inks)
            {
                if (!isSelfBlended) return;
                var note = label;
                if (!string.IsNullOrWhiteSpace(desc)) note += $": {desc}";
                if (inks.Count > 0) note += $" (inks: {string.Join(", ", inks)})";
                blendParts.Add(note);
            }

            foreach (var group in BgMats)
                foreach (var mat in group.Pieces)
                    AddBlendNote(group.Pieces.Count == 1 ? $"Background Mat {group.GroupNumber}" : $"Background Mat {group.GroupNumber} piece {mat.Layer}",
                        mat.IsSelfBlended, mat.SelfBlendDescription, mat.BlendInkColors);
            foreach (var group in AdditionalMats)
                foreach (var mat in group.Pieces)
                    AddBlendNote(group.Pieces.Count == 1 ? $"Additional Mat {group.GroupNumber}" : $"Additional Mat {group.GroupNumber} piece {mat.Layer}",
                        mat.IsSelfBlended, mat.SelfBlendDescription, mat.BlendInkColors);
            foreach (var fp in FocalParts)
                AddBlendNote($"Mat {BgMats.Count + AdditionalMats.Count + fp.PartNumber} (exterior focal)", fp.IsSelfBlended, fp.SelfBlendDescription, fp.BlendInkColors);
            foreach (var mat in InsideBgMats)
                AddBlendNote($"Inside background mat {mat.Layer}", mat.IsSelfBlended, mat.SelfBlendDescription, mat.BlendInkColors);
            foreach (var mat in InsideAdditionalMats)
                AddBlendNote($"Inside additional mat {mat.Layer}", mat.IsSelfBlended, mat.SelfBlendDescription, mat.BlendInkColors);
            if (HasInsideFocalMat)
                AddBlendNote("Inside focal", InsideFocal.IsSelfBlended, InsideFocal.SelfBlendDescription, InsideFocal.BlendInkColors);
            foreach (var s in ConfiguredSentiments)
                foreach (var p in s.Parts)
                    AddBlendNote($"Sentiment \"{p.ItemName}\"", p.IsSelfBlended, p.SelfBlendDescription, p.BlendInkColors);

            if (parts.Count == 0 && blendParts.Count == 0) return string.Empty;

            var result = new List<string>();
            if (parts.Count > 0) result.Add("Custom cardstock colors: " + string.Join("; ", parts));
            if (blendParts.Count > 0) result.Add("Self-blend notes: " + string.Join("; ", blendParts));
            return string.Join("\n", result);
        }

        [RelayCommand]
        private void CancelBuild()
        {
            WasConfirmed = false;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler? CloseRequested;

        private List<int> CollectAllItemIds()
        {
            // Items-used roll-up. Duplicates are intentional — if a stamp gets used
            // on the cardbase AND on a focal mat, it appears twice so the build log
            // accurately reflects how the card was built. Order matches the build
            // summary order so the items-used list reads as a step-by-step recipe.
            var result = new List<int>();

            void AddItem(int id) => result.Add(id);
            void AddInkItem(string colorName)
            {
                if (_inkItemIdByColor.TryGetValue(colorName, out var id)) AddItem(id);
            }
            void AddInkList(IEnumerable<string> colors) { foreach (var c in colors) AddInkItem(c); }
            void AddDecorationInks(IEnumerable<WizardMatDecoration> decorations)
            {
                foreach (var d in decorations)
                {
                    AddInkList(d.StampInkColors);
                    AddInkList(d.EmbossingInkColors);
                    foreach (var layer in d.StencilInkLayers) AddInkList(layer.InkColors);
                }
            }
            void AddCardstock(string? colorName)
            {
                if (string.IsNullOrEmpty(colorName) || colorName == "Other") return;
                if (_glitterCardstockIdByName.TryGetValue(colorName, out var csId)
                    || _foilCardstockIdByName.TryGetValue(colorName, out csId)
                    || _cardstockItemIdByName.TryGetValue(colorName, out csId))
                    result.Add(csId);
            }
            // Adhesive name lookup - adds the inventory item ID for any adhesive used.
            void AddAdhesives(IEnumerable<string> names)
            {
                foreach (var n in names)
                    if (!string.IsNullOrEmpty(n) && _adhesiveIdByName.TryGetValue(n, out var aId))
                        AddItem(aId);
            }
            // Inks rolled up from each WizardDetailEntry (stamp inks, embell embossing
            // inks, stencil inks, foil-stencil inks, plus the legacy single-pick InkColor).
            // Item IDs from AddedDetails are already covered by mat.GetItemIds().
            void AddAddedDetailsInks(IEnumerable<WizardDetailEntry> dets)
            {
                foreach (var det in dets)
                {
                    AddInkList(det.StampInkColors);
                    AddInkList(det.EmbellEmbossingInkColors);
                    AddInkList(det.StencilInkColors);
                    AddInkList(det.FoilStencilInkColors);
                    if (!string.IsNullOrEmpty(det.InkColor)) AddInkItem(det.InkColor);
                }
            }

            // Order: cardbase cardstock → each mat (cardstock first, then items used on it) →
            //        focal mat (cardstock → items → backer cardstock) → sentiments (cardstock → stamp) →
            //        embellishments → inside section (same pattern) → envelope last

            AddCardstock(EffectiveBaseCardstockColor);
            foreach (var d in CardBase.Decorations)
            {
                if (d.Item != null) AddItem(d.Item.Id);
                if (d.StampItem != null) AddItem(d.StampItem.Id);
            }
            AddDecorationInks(CardBase.Decorations);
            // Card-base detail entries: the per-pick rows the user added on the
            // Details sub-page (Stamps/Dies/Embell/Stacklets/EF/Stencils with their
            // own follow-up stencil glitter / happy medium / astro paste items, OLO
            // markers, Foils with stencil + ink + glitter follow-ups, watercolors,
            // ink colors). Each WizardDetailEntry knows how to enumerate its items.
            foreach (var det in CardBaseAddedDetails)
                foreach (var id in det.GetItemIds()) AddItem(id);
            AddAddedDetailsInks(CardBaseAddedDetails);
            // Cardbase adhesives picked from inventory (item ids already known).
            foreach (var a in CardBaseAddedAdhesives) AddItem(a.Id);

            foreach (var group in BgMats)
                foreach (var mat in group.Pieces)
                {
                    AddCardstock(mat.EffectiveCardstockColor);
                    foreach (var id in mat.GetItemIds()) AddItem(id);
                    AddInkList(mat.BlendInkColors);
                    AddInkList(mat.StampInkColors);
                    AddInkList(mat.EmbossingInkColors);
                    AddDecorationInks(mat.Decorations);
                    AddAddedDetailsInks(mat.AddedDetails);
                    AddAdhesives(mat.Adhesives);
                }

            foreach (var group in AdditionalMats)
                foreach (var mat in group.Pieces)
                {
                    AddCardstock(mat.EffectiveCardstockColor);
                    foreach (var id in mat.GetItemIds()) AddItem(id);
                    AddInkList(mat.BlendInkColors);
                    AddInkList(mat.StampInkColors);
                    AddInkList(mat.EmbossingInkColors);
                    AddDecorationInks(mat.Decorations);
                    AddAddedDetailsInks(mat.AddedDetails);
                    AddAdhesives(mat.Adhesives);
                }

            // Focal Mat hub uses WizardBgMatGroup (same shape as BG mats); its
            // per-piece detail entries hold the new Foils + glitter picks too.
            foreach (var group in FocalMatGroups)
                foreach (var mat in group.Pieces)
                {
                    AddCardstock(mat.EffectiveCardstockColor);
                    foreach (var id in mat.GetItemIds()) AddItem(id);
                    AddInkList(mat.BlendInkColors);
                    AddInkList(mat.StampInkColors);
                    AddInkList(mat.EmbossingInkColors);
                    AddDecorationInks(mat.Decorations);
                    AddAddedDetailsInks(mat.AddedDetails);
                    AddAdhesives(mat.Adhesives);
                }

            foreach (var fp in FocalParts)
            {
                AddCardstock(fp.EffectiveCardstockColor);
                foreach (var id in fp.GetItemIds()) AddItem(id);
                AddInkList(fp.BlendInkColors);
                AddInkList(fp.StampInkColors);
                AddInkList(fp.EmbossingInkColors);
                AddDecorationInks(fp.Decorations);
                AddAddedDetailsInks(fp.AddedDetails);
                AddCardstock(fp.EffectiveBackerCardstockColor);
                AddAdhesives(fp.Adhesives);
            }

            foreach (var s in ConfiguredSentiments)
                foreach (var p in s.Parts)
                {
                    if (!string.IsNullOrEmpty(p.CardstockColor) && p.CardstockColor != "Other")
                        AddCardstock(p.CardstockColor);
                    AddItem(p.ItemId);
                    if (p.IsEmbossed && p.EmbossingPowderItemId.HasValue)
                        AddItem(p.EmbossingPowderItemId.Value);
                    AddInkList(p.BlendInkColors);
                    AddInkList(p.StampInkColors);
                    // Each sentiment-part decoration's item (and its stamp item) must also be tracked
                    foreach (var d in p.Decorations)
                    {
                        AddItem(d.Item.Id);
                        if (d.StampItem != null) AddItem(d.StampItem.Id);
                    }
                    AddDecorationInks(p.Decorations);
                    AddAdhesives(p.Adhesives);
                }

            foreach (var e in AddedEmbellishments)
            {
                AddItem(e.ItemId);
                if (e.StampItemId.HasValue) AddItem(e.StampItemId.Value);
            }

            if (HasInside == true)
            {
                foreach (var mat in InsideBgMats)
                {
                    AddCardstock(mat.EffectiveCardstockColor);
                    foreach (var id in mat.GetItemIds()) AddItem(id);
                    AddInkList(mat.BlendInkColors);
                    AddInkList(mat.StampInkColors);
                    AddInkList(mat.EmbossingInkColors);
                    AddDecorationInks(mat.Decorations);
                    AddAddedDetailsInks(mat.AddedDetails);
                    AddAdhesives(mat.Adhesives);
                }
                foreach (var mat in InsideAdditionalMats)
                {
                    AddCardstock(mat.EffectiveCardstockColor);
                    foreach (var id in mat.GetItemIds()) AddItem(id);
                    AddInkList(mat.BlendInkColors);
                    AddInkList(mat.StampInkColors);
                    AddInkList(mat.EmbossingInkColors);
                    AddDecorationInks(mat.Decorations);
                    AddAddedDetailsInks(mat.AddedDetails);
                    AddAdhesives(mat.Adhesives);
                }
                if (HasInsideFocalMat)
                {
                    AddCardstock(InsideFocal.EffectiveCardstockColor);
                    foreach (var id in InsideFocal.GetItemIds()) AddItem(id);
                    AddInkList(InsideFocal.BlendInkColors);
                    AddInkList(InsideFocal.StampInkColors);
                    AddInkList(InsideFocal.EmbossingInkColors);
                    AddDecorationInks(InsideFocal.Decorations);
                    AddAddedDetailsInks(InsideFocal.AddedDetails);
                    AddCardstock(InsideFocal.EffectiveBackerCardstockColor);
                    AddAdhesives(InsideFocal.Adhesives);
                }
                foreach (var c in ConfiguredInsideSentiments)
                    foreach (var p in c.Parts)
                    {
                        AddItem(p.ItemId);
                        AddCardstock(p.CardstockColor);
                        AddAdhesives(p.Adhesives);
                    }
                foreach (var e in InsideAddedEmbellishments) { AddItem(e.ItemId); if (e.StampItemId.HasValue) AddItem(e.StampItemId.Value); }
            }

            if (SelectedEnvelopeItem != null) AddItem(SelectedEnvelopeItem.Id);
            if (SelectedStorageBagItem != null) AddItem(SelectedStorageBagItem.Id);

            return result;
        }

        private void AddCardstockId(string? colorName, List<int> ids)
        {
            if (string.IsNullOrEmpty(colorName) || colorName == "Other") return;
            if (_cardstockItemIdByName.TryGetValue(colorName, out var id))
                ids.Add(id);
        }

        // Builds the per-layer "— Layer 1: …; Layer 2: …" suffix appended to a
        // stencil build-step label so the final card summary reflects every
        // layer's inks + Glitter / Happy Medium / Astro Paste picks. Returns
        // an empty string when nothing was recorded for any layer.
        private static string StencilLayerSummarySuffix(WizardDetailEntry d)
        {
            if (d.StencilLayerEntries == null || d.StencilLayerEntries.Count == 0)
                return string.Empty;
            var nonEmpty = d.StencilLayerEntries
                .Where(le => le.InkColors.Count > 0 || le.UsedGlitter || le.UsedHappyMedium || le.UsedAstroPaste)
                .Select(le => le.DisplaySummary)
                .ToList();
            return nonEmpty.Count == 0 ? string.Empty : " — " + string.Join("; ", nonEmpty);
        }

        private List<WizardBuildStep> AssembleBuildSteps()
        {
            var steps = new List<WizardBuildStep>();

            void Add(string section, string stepType, int? matLayer, int? itemId,
                     int? dieId, string? cutting, string label)
                => steps.Add(new WizardBuildStep(section, stepType, matLayer, itemId, dieId, cutting, label));

            // Emit one row per item in a captured WizardDetailEntry. Used by every
            // context that holds AddedDetails (cardbase, BG mats, additional mats,
            // focal parts, focal-mat-groups, sentiment parts, and the inside-card
            // mirrors of all of the above) so adding a new detail field only needs
            // a single edit here. labelPrefix becomes the human-readable scope
            // ("Card base", "BG Mat 1", "Sentiment", …); stepPrefix is the build
            // step's machine-readable kind prefix ("cardbase_detail", "mat_detail",
            // "sentiment_detail", …).
            void EmitDetailEntry(string section, string stepPrefix, string labelPrefix,
                                 int? matLayer, WizardDetailEntry d)
            {
                if (d.Stamp != null)
                    Add(section, $"{stepPrefix}_stamp", matLayer, d.Stamp.Id, null, null, $"{labelPrefix} stamp: {d.Stamp.Name}");
                if (d.Die != null)
                    Add(section, $"{stepPrefix}_die", matLayer, d.Die.Id, null, null, $"{labelPrefix} die: {d.Die.Name}");
                if (d.Embellishment != null)
                    Add(section, $"{stepPrefix}_embellishment", matLayer, d.Embellishment.Id, null, null, $"{labelPrefix} embellishment: {d.Embellishment.Name}");
                if (d.Stacklet != null)
                    Add(section, $"{stepPrefix}_stacklet", matLayer, d.Stacklet.Id, null, null, $"{labelPrefix} stacklet: {d.Stacklet.Name}");
                if (d.EmbossingFolder != null)
                    Add(section, $"{stepPrefix}_embossing_folder", matLayer, d.EmbossingFolder.Id, null, null, $"{labelPrefix} embossing folder: {d.EmbossingFolder.Name}");
                if (d.Stencil != null)
                    Add(section, $"{stepPrefix}_stencil", matLayer, d.Stencil.Id, null, null,
                        $"{labelPrefix} stencil: {d.Stencil.Name}{StencilLayerSummarySuffix(d)}");
                foreach (var marker in d.OloMarkers)
                    Add(section, $"{stepPrefix}_olo_marker", matLayer, marker.Id, null, null, $"{labelPrefix} OLO marker: {marker.Name}");
                if (d.Watercolor != null)
                    Add(section, $"{stepPrefix}_watercolor", matLayer, d.Watercolor.Id, null, null, $"{labelPrefix} watercolor: {d.Watercolor.Name}");
                if (d.StampEmbossingPowder != null)
                    Add(section, $"{stepPrefix}_embossing_powder", matLayer, d.StampEmbossingPowder.Id, null, null, $"{labelPrefix} embossing powder: {d.StampEmbossingPowder.Name}");
                if (d.EmbellEmbossingStamp != null)
                    Add(section, $"{stepPrefix}_embell_embossing_stamp", matLayer, d.EmbellEmbossingStamp.Id, null, null, $"{labelPrefix} embell embossing stamp: {d.EmbellEmbossingStamp.Name}");
                foreach (var g in d.StencilGlitterItems)
                    Add(section, $"{stepPrefix}_stencil_glitter", matLayer, g.Id, null, null, $"{labelPrefix} stencil glitter: {g.Name}");
                foreach (var h in d.StencilHappyMediumItems)
                    Add(section, $"{stepPrefix}_stencil_happy_medium", matLayer, h.Id, null, null, $"{labelPrefix} stencil happy medium: {h.Name}");
                foreach (var a in d.StencilAstroPasteItems)
                    Add(section, $"{stepPrefix}_stencil_astro_paste", matLayer, a.Id, null, null, $"{labelPrefix} stencil astro paste: {a.Name}");
                if (d.Foil != null)
                    Add(section, $"{stepPrefix}_foil", matLayer, d.Foil.Id, null, null, $"{labelPrefix} foil: {d.Foil.Name} ({d.FoilApplicationMethod})");
                if (d.FoilStencil != null)
                    Add(section, $"{stepPrefix}_foil_stencil", matLayer, d.FoilStencil.Id, null, null, $"{labelPrefix} foil stencil: {d.FoilStencil.Name}");
                foreach (var g in d.FoilStencilGlitterItems)
                    Add(section, $"{stepPrefix}_foil_stencil_glitter", matLayer, g.Id, null, null, $"{labelPrefix} foil stencil glitter: {g.Name}");
                foreach (var h in d.FoilStencilHappyMediumItems)
                    Add(section, $"{stepPrefix}_foil_stencil_happy_medium", matLayer, h.Id, null, null, $"{labelPrefix} foil stencil happy medium: {h.Name}");
                foreach (var a in d.FoilStencilAstroPasteItems)
                    Add(section, $"{stepPrefix}_foil_stencil_astro_paste", matLayer, a.Id, null, null, $"{labelPrefix} foil stencil astro paste: {a.Name}");
                if (string.Equals(d.FoilApplicationMethod, "Toner", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(d.FoilTonerText))
                    Add(section, $"{stepPrefix}_foil_toner", matLayer, null, null, d.FoilTonerFont, $"{labelPrefix} foil toner: \"{d.FoilTonerText}\" in {d.FoilTonerFont}");
            }

            var cardBaseLabel = string.IsNullOrEmpty(EffectiveBaseCardstockColor)
                ? $"Cardbase: {SelectedCardBase}"
                : $"Cardbase: {SelectedCardBase} in {EffectiveBaseCardstockColor}";
            Add("exterior", "card_base", null, null, null, null, cardBaseLabel);

            // Card base decorations
            foreach (var d in CardBase.Decorations)
            {
                Add("exterior", "cardbase_decoration", null, d.Item?.Id, null, null, $"Card Base decoration: {d.Item?.Name}");
                if (d.StampItem != null)
                    Add("exterior", "cardbase_decoration_stamp", null, d.StampItem.Id, null, null, $"Card Base stamp: {d.StampItem.Name}");
            }

            // Card-base detail entries are emitted BEFORE the cardbase adhesives so
            // the order reads top-to-bottom: cardbase → its decorations & details →
            // adhesives that hold it all together.
            foreach (var d in CardBaseAddedDetails)
                EmitDetailEntry("exterior", "cardbase_detail", "Card base", null, d);

            // Cardbase adhesives picked on the Adhesives sub-page
            foreach (var a in CardBaseAddedAdhesives)
                Add("exterior", "card_base_adhesive", null, a.Id, null, null, $"Card Base Adhesive: {a.Name}");

            foreach (var group in BgMats.Where(g => !g.IsInside))
            {
                Add("exterior", "background_mat", group.GroupNumber, null, null, null, group.DisplaySummary);
                foreach (var mat in group.Pieces)
                {
                    var itemId = mat.CuttingMethod switch
                    {
                        "All Planned Out" => mat.PlannedOutItem?.Id,
                        "Frames" => mat.FramesItem?.Id,
                        "Stacklets" => mat.StackletItem?.Id,
                        "Insider" => mat.InsiderItem?.Id,
                        "Foil-It" => mat.FoilItItem?.Id,
                        _ => null
                    };
                    var pieceLabel = group.Pieces.Count == 1
                        ? $"Background Mat {group.GroupNumber}: {mat.DisplaySummary}"
                        : $"Background Mat {group.GroupNumber} piece {mat.Layer}: {mat.DisplaySummary}";
                    Add("exterior", "background_mat_piece", group.GroupNumber, itemId, null, mat.CuttingMethod, pieceLabel);
                    foreach (var d in mat.Decorations)
                    {
                        Add("exterior", "mat_decoration", group.GroupNumber, d.Item.Id, null, null, $"Background Mat {group.GroupNumber} decoration: {d.Item.Name}");
                        if (d.StampItem != null)
                            Add("exterior", "decoration_stamp", group.GroupNumber, d.StampItem.Id, null, null, $"Background Mat {group.GroupNumber} stamp: {d.StampItem.Name}");
                    }
                    // New-hub mats record decorations as WizardDetailEntry on AddedDetails
                    // instead of WizardMatDecoration. Emit per-detail steps so each picked
                    // item is tracked in the project. Per-piece adhesives are flushed
                    // after the details so the order reads "build the layer → glue it down".
                    foreach (var d in mat.AddedDetails)
                        EmitDetailEntry("exterior", "mat_detail", $"BG Mat {group.GroupNumber}", group.GroupNumber, d);
                    foreach (var aName in mat.Adhesives)
                        Add("exterior", "mat_adhesive", group.GroupNumber,
                            _adhesiveIdByName.TryGetValue(aName, out var aId) ? aId : null,
                            null, null, $"BG Mat {group.GroupNumber} adhesive: {aName}");
                }
            }

            foreach (var group in AdditionalMats.Where(g => !g.IsInside))
            {
                Add("exterior", "additional_mat", group.GroupNumber, null, null, null, group.DisplaySummary);
                foreach (var mat in group.Pieces)
                {
                    var itemId = mat.CuttingMethod switch
                    {
                        "All Planned Out" => mat.PlannedOutItem?.Id,
                        "Frames" => mat.FramesItem?.Id,
                        "Stacklets" => mat.StackletItem?.Id,
                        "Insider" => mat.InsiderItem?.Id,
                        "Foil-It" => mat.FoilItItem?.Id,
                        _ => null
                    };
                    var pieceLabel = group.Pieces.Count == 1
                        ? $"Additional Mat {group.GroupNumber}: {mat.DisplaySummary}"
                        : $"Additional Mat {group.GroupNumber} piece {mat.Layer}: {mat.DisplaySummary}";
                    Add("exterior", "additional_mat_piece", group.GroupNumber, itemId, null, mat.CuttingMethod, pieceLabel);
                    foreach (var d in mat.Decorations)
                    {
                        Add("exterior", "mat_decoration", group.GroupNumber, d.Item.Id, null, null, $"Additional Mat {group.GroupNumber} decoration: {d.Item.Name}");
                        if (d.StampItem != null)
                            Add("exterior", "decoration_stamp", group.GroupNumber, d.StampItem.Id, null, null, $"Additional Mat {group.GroupNumber} stamp: {d.StampItem.Name}");
                    }
                    foreach (var d in mat.AddedDetails)
                        EmitDetailEntry("exterior", "additional_mat_detail", $"Additional Mat {group.GroupNumber}", group.GroupNumber, d);
                    foreach (var aName in mat.Adhesives)
                        Add("exterior", "additional_mat_adhesive", group.GroupNumber,
                            _adhesiveIdByName.TryGetValue(aName, out var aId) ? aId : null,
                            null, null, $"Additional Mat {group.GroupNumber} adhesive: {aName}");
                }
            }

            // Focal-mat hub (new). Pieces share the WizardBgMat shape with BG / Additional
            // mats so the same per-detail emission applies. Inserted between additional
            // mats and the legacy FocalParts so the build order stays mats → focal.
            foreach (var group in FocalMatGroups.Where(g => !g.IsInside))
            {
                Add("exterior", "focal_mat_group", group.GroupNumber, null, null, null, group.DisplaySummary);
                foreach (var mat in group.Pieces)
                {
                    var itemId = mat.CuttingMethod switch
                    {
                        "All Planned Out" => mat.PlannedOutItem?.Id,
                        "Frames" => mat.FramesItem?.Id,
                        "Stacklets" => mat.StackletItem?.Id,
                        "Insider" => mat.InsiderItem?.Id,
                        "Foil-It" => mat.FoilItItem?.Id,
                        _ => null
                    };
                    var pieceLabel = group.Pieces.Count == 1
                        ? $"Focal Mat {group.GroupNumber}: {mat.DisplaySummary}"
                        : $"Focal Mat {group.GroupNumber} part {mat.Layer}: {mat.DisplaySummary}";
                    Add("exterior", "focal_mat_piece", group.GroupNumber, itemId, null, mat.CuttingMethod, pieceLabel);
                    foreach (var d in mat.Decorations)
                    {
                        Add("exterior", "focal_mat_decoration", group.GroupNumber, d.Item.Id, null, null, $"Focal Mat {group.GroupNumber} decoration: {d.Item.Name}");
                        if (d.StampItem != null)
                            Add("exterior", "focal_mat_decoration_stamp", group.GroupNumber, d.StampItem.Id, null, null, $"Focal Mat {group.GroupNumber} stamp: {d.StampItem.Name}");
                    }
                    foreach (var d in mat.AddedDetails)
                        EmitDetailEntry("exterior", "focal_mat_detail", $"Focal Mat {group.GroupNumber}", group.GroupNumber, d);
                    foreach (var aName in mat.Adhesives)
                        Add("exterior", "focal_mat_adhesive", group.GroupNumber,
                            _adhesiveIdByName.TryGetValue(aName, out var aId) ? aId : null,
                            null, null, $"Focal Mat {group.GroupNumber} adhesive: {aName}");
                }
            }

            // Exterior focal parts (legacy single-piece focal flow). PartNumber is
            // offset past every BG / Additional / Focal-hub group so the focal step
            // sorts after them, matching the physical assembly order.
            int focalPartBaseIndex = BgMats.Count + AdditionalMats.Count + FocalMatGroups.Count;
            foreach (var fp in FocalParts)
            {
                var focalItemId = fp.CuttingMethod switch
                {
                    "All Planned Out" => fp.PlannedOutItem?.Id,
                    "Frames"          => fp.FramesItem?.Id,
                    "Stacklet"        => fp.StackletItem?.Id,
                    "Insider"         => fp.InsiderItem?.Id,
                    "Foil-It"         => fp.FoilItItem?.Id,
                    "Dies"            => fp.SelectedDie?.Id,
                    _                 => (int?)null
                };
                bool focalHasSomething = focalItemId.HasValue
                    || !string.IsNullOrEmpty(fp.EffectiveCardstockColor)
                    || fp.Decorations.Count > 0 || fp.HasBacker
                    || fp.AddedDetails.Count > 0;
                if (!focalHasSomething) continue;
                int layerIndex = focalPartBaseIndex + fp.PartNumber;
                Add("exterior", "focal_mat", layerIndex,
                    focalItemId, null, fp.CuttingMethod,
                    $"Focal Mat Piece {fp.PartNumber}: {fp.DisplaySummary}");
                foreach (var d in fp.Decorations)
                {
                    Add("exterior", "focal_decoration", layerIndex, d.Item.Id, null, null, $"Focal Mat {fp.PartNumber} decoration: {d.Item.Name}");
                    if (d.StampItem != null)
                        Add("exterior", "focal_decoration_stamp", layerIndex, d.StampItem.Id, null, null, $"Focal Mat {fp.PartNumber} stamp: {d.StampItem.Name}");
                }
                foreach (var d in fp.AddedDetails)
                    EmitDetailEntry("exterior", "focal_detail", $"Focal Mat {fp.PartNumber}", layerIndex, d);
                foreach (var aName in fp.Adhesives)
                    Add("exterior", "focal_adhesive", layerIndex,
                        _adhesiveIdByName.TryGetValue(aName, out var aId) ? aId : null,
                        null, null, $"Focal Mat {fp.PartNumber} adhesive: {aName}");
            }

            foreach (var s in ConfiguredSentiments)
                foreach (var p in s.Parts)
                {
                    Add("exterior", "sentiment", null, p.ItemId, null, null, $"Sentiment: {p.DisplaySummary}");
                    // Per-part decorations also need their own build-steps so they show up on the project page
                    foreach (var d in p.Decorations)
                    {
                        Add("exterior", "sentiment_decoration", null, d.Item.Id, null, null, $"Sentiment decoration: {d.Item.Name}");
                        if (d.StampItem != null)
                            Add("exterior", "sentiment_decoration_stamp", null, d.StampItem.Id, null, null, $"Sentiment decoration stamp: {d.StampItem.Name}");
                    }
                    // Per-part captured detail entries (Stamps/Dies/Embell/Stacklets/EF/
                    // Stencils/OLO/Watercolor/Foils with their own follow-ups). Without
                    // this loop the user's per-sentiment Details sub-page picks were
                    // captured but never surfaced on the project page.
                    foreach (var d in p.AddedDetails)
                        EmitDetailEntry("exterior", "sentiment_detail", "Sentiment", null, d);
                    // Sentiment-piece adhesives.
                    foreach (var aName in p.Adhesives)
                        Add("exterior", "sentiment_adhesive", null,
                            _adhesiveIdByName.TryGetValue(aName, out var aId) ? aId : null,
                            null, null, $"Sentiment adhesive: {aName}");
                }
            foreach (var e in AddedEmbellishments)
            {
                Add("exterior", "embellishment", null, e.ItemId, null, null, $"Embellishment: {e.DisplaySummary}");
                if (e.StampItemId.HasValue)
                    Add("exterior", "embellishment_stamp", null, e.StampItemId.Value, null, null,
                        $"Embellishment Stamp: {e.StampItemName}");
            }

            if (HasInside == true)
            {
                // Mat groups whose IsInside flag is set were added through the new
                // hub but tagged for the inside of the card. Emit them inside the
                // inside-section so they sort with the rest of the inside content.
                foreach (var group in BgMats.Where(g => g.IsInside))
                {
                    Add("inside", "background_mat", group.GroupNumber, null, null, null, group.DisplaySummary);
                    foreach (var mat in group.Pieces)
                    {
                        var itemId = mat.CuttingMethod switch
                        {
                            "All Planned Out" => mat.PlannedOutItem?.Id,
                            "Frames" => mat.FramesItem?.Id,
                            "Stacklets" => mat.StackletItem?.Id,
                            "Insider" => mat.InsiderItem?.Id,
                            "Foil-It" => mat.FoilItItem?.Id,
                            _ => null
                        };
                        var pieceLabel = group.Pieces.Count == 1
                            ? $"Inside Background Mat {group.GroupNumber}: {mat.DisplaySummary}"
                            : $"Inside Background Mat {group.GroupNumber} piece {mat.Layer}: {mat.DisplaySummary}";
                        Add("inside", "background_mat_piece", group.GroupNumber, itemId, null, mat.CuttingMethod, pieceLabel);
                        foreach (var d in mat.Decorations)
                        {
                            Add("inside", "mat_decoration", group.GroupNumber, d.Item.Id, null, null, $"Inside Background Mat {group.GroupNumber} decoration: {d.Item.Name}");
                            if (d.StampItem != null)
                                Add("inside", "decoration_stamp", group.GroupNumber, d.StampItem.Id, null, null, $"Inside Background Mat {group.GroupNumber} stamp: {d.StampItem.Name}");
                        }
                        foreach (var d in mat.AddedDetails)
                            EmitDetailEntry("inside", "mat_detail", $"Inside BG Mat {group.GroupNumber}", group.GroupNumber, d);
                        foreach (var aName in mat.Adhesives)
                            Add("inside", "mat_adhesive", group.GroupNumber,
                                _adhesiveIdByName.TryGetValue(aName, out var aId) ? aId : null,
                                null, null, $"Inside BG Mat {group.GroupNumber} adhesive: {aName}");
                    }
                }

                foreach (var group in AdditionalMats.Where(g => g.IsInside))
                {
                    Add("inside", "additional_mat", group.GroupNumber, null, null, null, group.DisplaySummary);
                    foreach (var mat in group.Pieces)
                    {
                        var itemId = mat.CuttingMethod switch
                        {
                            "All Planned Out" => mat.PlannedOutItem?.Id,
                            "Frames" => mat.FramesItem?.Id,
                            "Stacklets" => mat.StackletItem?.Id,
                            "Insider" => mat.InsiderItem?.Id,
                            "Foil-It" => mat.FoilItItem?.Id,
                            _ => null
                        };
                        var pieceLabel = group.Pieces.Count == 1
                            ? $"Inside Additional Mat {group.GroupNumber}: {mat.DisplaySummary}"
                            : $"Inside Additional Mat {group.GroupNumber} piece {mat.Layer}: {mat.DisplaySummary}";
                        Add("inside", "additional_mat_piece", group.GroupNumber, itemId, null, mat.CuttingMethod, pieceLabel);
                        foreach (var d in mat.Decorations)
                        {
                            Add("inside", "mat_decoration", group.GroupNumber, d.Item.Id, null, null, $"Inside Additional Mat {group.GroupNumber} decoration: {d.Item.Name}");
                            if (d.StampItem != null)
                                Add("inside", "decoration_stamp", group.GroupNumber, d.StampItem.Id, null, null, $"Inside Additional Mat {group.GroupNumber} stamp: {d.StampItem.Name}");
                        }
                        foreach (var d in mat.AddedDetails)
                            EmitDetailEntry("inside", "additional_mat_detail", $"Inside Additional Mat {group.GroupNumber}", group.GroupNumber, d);
                        foreach (var aName in mat.Adhesives)
                            Add("inside", "additional_mat_adhesive", group.GroupNumber,
                                _adhesiveIdByName.TryGetValue(aName, out var aId) ? aId : null,
                                null, null, $"Inside Additional Mat {group.GroupNumber} adhesive: {aName}");
                    }
                }

                foreach (var group in FocalMatGroups.Where(g => g.IsInside))
                {
                    Add("inside", "focal_mat_group", group.GroupNumber, null, null, null, group.DisplaySummary);
                    foreach (var mat in group.Pieces)
                    {
                        var itemId = mat.CuttingMethod switch
                        {
                            "All Planned Out" => mat.PlannedOutItem?.Id,
                            "Frames" => mat.FramesItem?.Id,
                            "Stacklets" => mat.StackletItem?.Id,
                            "Insider" => mat.InsiderItem?.Id,
                            "Foil-It" => mat.FoilItItem?.Id,
                            _ => null
                        };
                        var pieceLabel = group.Pieces.Count == 1
                            ? $"Inside Focal Mat {group.GroupNumber}: {mat.DisplaySummary}"
                            : $"Inside Focal Mat {group.GroupNumber} part {mat.Layer}: {mat.DisplaySummary}";
                        Add("inside", "focal_mat_piece", group.GroupNumber, itemId, null, mat.CuttingMethod, pieceLabel);
                        foreach (var d in mat.Decorations)
                        {
                            Add("inside", "focal_mat_decoration", group.GroupNumber, d.Item.Id, null, null, $"Inside Focal Mat {group.GroupNumber} decoration: {d.Item.Name}");
                            if (d.StampItem != null)
                                Add("inside", "focal_mat_decoration_stamp", group.GroupNumber, d.StampItem.Id, null, null, $"Inside Focal Mat {group.GroupNumber} stamp: {d.StampItem.Name}");
                        }
                        foreach (var d in mat.AddedDetails)
                            EmitDetailEntry("inside", "focal_mat_detail", $"Inside Focal Mat {group.GroupNumber}", group.GroupNumber, d);
                        foreach (var aName in mat.Adhesives)
                            Add("inside", "focal_mat_adhesive", group.GroupNumber,
                                _adhesiveIdByName.TryGetValue(aName, out var aId) ? aId : null,
                                null, null, $"Inside Focal Mat {group.GroupNumber} adhesive: {aName}");
                    }
                }

                foreach (var mat in InsideBgMats)
                {
                    var itemId = mat.CuttingMethod switch
                    {
                        "All Planned Out" => mat.PlannedOutItem?.Id,
                        "Frames" => mat.FramesItem?.Id,
                        "Stacklets" => mat.StackletItem?.Id,
                        "Insider" => mat.InsiderItem?.Id,
                        "Foil-It" => mat.FoilItItem?.Id,
                        _ => null
                    };
                    Add("inside", "background_mat", mat.Layer, itemId, null, mat.CuttingMethod, $"Inside Background Mat {mat.Layer}: {mat.DisplaySummary}");
                    foreach (var d in mat.Decorations)
                    {
                        Add("inside", "mat_decoration", mat.Layer, d.Item.Id, null, null, $"Inside background mat {mat.Layer} decoration: {d.Item.Name}");
                        if (d.StampItem != null)
                            Add("inside", "decoration_stamp", mat.Layer, d.StampItem.Id, null, null, $"Inside background mat {mat.Layer} stamp: {d.StampItem.Name}");
                    }
                    foreach (var d in mat.AddedDetails)
                        EmitDetailEntry("inside", "mat_detail", $"Inside BG Mat {mat.Layer}", mat.Layer, d);
                    foreach (var aName in mat.Adhesives)
                        Add("inside", "mat_adhesive", mat.Layer,
                            _adhesiveIdByName.TryGetValue(aName, out var aId) ? aId : null,
                            null, null, $"Inside BG Mat {mat.Layer} adhesive: {aName}");
                }

                foreach (var mat in InsideAdditionalMats)
                {
                    var itemId = mat.CuttingMethod switch
                    {
                        "All Planned Out" => mat.PlannedOutItem?.Id,
                        "Frames" => mat.FramesItem?.Id,
                        "Stacklets" => mat.StackletItem?.Id,
                        "Insider" => mat.InsiderItem?.Id,
                        "Foil-It" => mat.FoilItItem?.Id,
                        _ => null
                    };
                    Add("inside", "additional_mat", mat.Layer, itemId, null, mat.CuttingMethod, $"Inside Additional Mat {mat.Layer}: {mat.DisplaySummary}");
                    foreach (var d in mat.Decorations)
                    {
                        Add("inside", "mat_decoration", mat.Layer, d.Item.Id, null, null, $"Inside additional mat {mat.Layer} decoration: {d.Item.Name}");
                        if (d.StampItem != null)
                            Add("inside", "decoration_stamp", mat.Layer, d.StampItem.Id, null, null, $"Inside additional mat {mat.Layer} stamp: {d.StampItem.Name}");
                    }
                    foreach (var d in mat.AddedDetails)
                        EmitDetailEntry("inside", "additional_mat_detail", $"Inside Additional Mat {mat.Layer}", mat.Layer, d);
                    foreach (var aName in mat.Adhesives)
                        Add("inside", "additional_mat_adhesive", mat.Layer,
                            _adhesiveIdByName.TryGetValue(aName, out var aId) ? aId : null,
                            null, null, $"Inside Additional Mat {mat.Layer} adhesive: {aName}");
                }

                // Inside focal - only persisted when user added one
                if (HasInsideFocalMat)
                {
                    var insideFocalItemId = InsideFocal.CuttingMethod switch
                    {
                        "All Planned Out" => InsideFocal.PlannedOutItem?.Id,
                        "Frames"          => InsideFocal.FramesItem?.Id,
                        "Stacklet"        => InsideFocal.StackletItem?.Id,
                        "Insider"         => InsideFocal.InsiderItem?.Id,
                        "Foil-It"         => InsideFocal.FoilItItem?.Id,
                        "Dies"            => InsideFocal.SelectedDie?.Id,
                        _                 => (int?)null
                    };
                    int insideFocalLayer = InsideBgMats.Count + InsideAdditionalMats.Count + 1;
                    Add("inside", "focal_mat", insideFocalLayer,
                        insideFocalItemId, null, InsideFocal.CuttingMethod,
                        $"Inside Focal Mat: {InsideFocal.DisplaySummary}");
                    foreach (var d in InsideFocal.Decorations)
                    {
                        Add("inside", "focal_decoration", insideFocalLayer, d.Item.Id, null, null, $"Inside focal decoration: {d.Item.Name}");
                        if (d.StampItem != null)
                            Add("inside", "focal_decoration_stamp", insideFocalLayer, d.StampItem.Id, null, null, $"Inside focal stamp: {d.StampItem.Name}");
                    }
                    foreach (var d in InsideFocal.AddedDetails)
                        EmitDetailEntry("inside", "focal_detail", "Inside Focal Mat", insideFocalLayer, d);
                    foreach (var aName in InsideFocal.Adhesives)
                        Add("inside", "focal_adhesive", insideFocalLayer,
                            _adhesiveIdByName.TryGetValue(aName, out var aId) ? aId : null,
                            null, null, $"Inside focal adhesive: {aName}");
                }

                foreach (var c in ConfiguredInsideSentiments)
                    foreach (var p in c.Parts)
                    {
                        Add("inside", "sentiment", null, p.ItemId, null, null, $"Sentiment: {p.DisplaySummary}");
                        foreach (var d in p.Decorations)
                        {
                            Add("inside", "sentiment_decoration", null, d.Item.Id, null, null, $"Inside sentiment decoration: {d.Item.Name}");
                            if (d.StampItem != null)
                                Add("inside", "sentiment_decoration_stamp", null, d.StampItem.Id, null, null, $"Inside sentiment decoration stamp: {d.StampItem.Name}");
                        }
                        foreach (var d in p.AddedDetails)
                            EmitDetailEntry("inside", "sentiment_detail", "Inside Sentiment", null, d);
                        foreach (var aName in p.Adhesives)
                            Add("inside", "sentiment_adhesive", null,
                                _adhesiveIdByName.TryGetValue(aName, out var aId) ? aId : null,
                                null, null, $"Inside sentiment adhesive: {aName}");
                    }
                foreach (var e in InsideAddedEmbellishments)
                {
                    Add("inside", "embellishment", null, e.ItemId, null, null, $"Embellishment: {e.ItemName}");
                    if (e.StampItemId.HasValue)
                        Add("inside", "embellishment_stamp", null, e.StampItemId.Value, null, null, $"Inside embellishment stamp: {e.StampItemName}");
                }
            }

            if (SelectedEnvelopeItem != null)
                Add("exterior", "envelope", null, SelectedEnvelopeItem.Id, null, null, $"Envelope: {SelectedEnvelopeItem.Name}");
            if (SelectedStorageBagItem != null)
                Add("exterior", "storage_bag", null, SelectedStorageBagItem.Id, null, null, $"Storage Bag: {SelectedStorageBagItem.Name}");

            return steps;
        }

        // ── Snapshot capture / restore ────────────────────────────────────────
        // Round-trips the wizard's full editable state (every collection, every
        // picker selection) so that re-opening the wizard on an existing build
        // populates with what the user originally chose. Output-only fields like
        // CardBaseType / BuildSteps / BuildOtherNotes are *not* snapshotted —
        // they're regenerated from the live state when Create Card runs.
        public string CaptureSnapshotJson()
        {
            var snap = new WizardBuildSnapshot
            {
                SelectedCardBase = SelectedCardBase,
                Notes = WizardNotes,

                BaseRegularCardstockItemId = SelectedBaseRegularCardstockItem?.Id,
                BaseFoilCardstockItemId    = SelectedBaseFoilCardstockItem?.Id,
                BaseGlitterCardstockItemId = SelectedBaseGlitterCardstockItem?.Id,
                BaseCardstockColor         = SelectedBaseCardstockColor,
                BaseIsSelfBlended          = BaseIsSelfBlended,
                BaseSelfBlendDescription   = BaseSelfBlendDescription,
                BaseBlendInkColors         = BaseBlendInks.Ordered.ToList(),
                CardBase                   = CardBase,
                CardBaseAddedDetails       = CardBaseAddedDetails.ToList(),
                CardBaseAddedAdhesives     = CardBaseAddedAdhesives.ToList(),

                BgMats               = BgMats.ToList(),
                AdditionalMats       = AdditionalMats.ToList(),
                FocalMatGroups       = FocalMatGroups.ToList(),
                FocalParts           = FocalParts.ToList(),
                ConfiguredSentiments = ConfiguredSentiments.ToList(),
                AddedEmbellishments  = AddedEmbellishments.ToList(),
                SelectedEnvelopeItem = SelectedEnvelopeItem,
                SelectedStorageBagItem = SelectedStorageBagItem,

                InsideBgMats               = InsideBgMats.ToList(),
                InsideAdditionalMats       = InsideAdditionalMats.ToList(),
                InsideFocal                = InsideFocal,
                ConfiguredInsideSentiments = ConfiguredInsideSentiments.ToList(),
                InsideAddedEmbellishments  = InsideAddedEmbellishments.ToList(),

                InsideLinerCardstockItemId = SelectedInsideLinerCardstockItem?.Id,
                InsideLinerCardstockColor  = SelectedInsideLinerCardstockColor,
                InsideMiscDetails          = InsideMiscDetails.ToList(),
            };
            return snap.ToJson();
        }

        public void LoadFromSnapshotJson(string? json)
        {
            var snap = WizardBuildSnapshot.FromJson(json);
            if (snap == null) return;

            // Top-level
            if (!string.IsNullOrEmpty(snap.SelectedCardBase)) SelectedCardBase = snap.SelectedCardBase;
            if (!string.IsNullOrEmpty(snap.Notes)) WizardNotes = snap.Notes;

            // Card Base
            BaseIsSelfBlended        = snap.BaseIsSelfBlended;
            BaseSelfBlendDescription = snap.BaseSelfBlendDescription;
            SelectedBaseCardstockColor = snap.BaseCardstockColor;
            // Resolve cardstock item references back to the live ObservableCollection instances
            // so the bound ComboBox SelectedItem matches by reference (not just by Id).
            if (snap.BaseRegularCardstockItemId is int rid)
                SelectedBaseRegularCardstockItem = BaseCardstockRegularItems.FirstOrDefault(i => i.Id == rid);
            if (snap.BaseFoilCardstockItemId is int fid)
                SelectedBaseFoilCardstockItem = BaseCardstockFoilItems.FirstOrDefault(i => i.Id == fid);
            if (snap.BaseGlitterCardstockItemId is int gid)
                SelectedBaseGlitterCardstockItem = BaseCardstockGlitterItems.FirstOrDefault(i => i.Id == gid);
            BaseBlendInks.Ordered.Clear();
            foreach (var c in snap.BaseBlendInkColors) BaseBlendInks.Ordered.Add(c);

            if (snap.CardBaseAddedDetails != null)
            {
                CardBaseAddedDetails.Clear();
                foreach (var d in snap.CardBaseAddedDetails) CardBaseAddedDetails.Add(d);
            }
            if (snap.CardBaseAddedAdhesives != null)
            {
                CardBaseAddedAdhesives.Clear();
                foreach (var a in snap.CardBaseAddedAdhesives) CardBaseAddedAdhesives.Add(a);
            }

            // Outside collections
            BgMats.Clear();
            foreach (var g in snap.BgMats ?? Enumerable.Empty<WizardBgMatGroup>()) BgMats.Add(g);
            AdditionalMats.Clear();
            foreach (var g in snap.AdditionalMats ?? Enumerable.Empty<WizardBgMatGroup>()) AdditionalMats.Add(g);
            FocalMatGroups.Clear();
            foreach (var g in snap.FocalMatGroups ?? Enumerable.Empty<WizardBgMatGroup>()) FocalMatGroups.Add(g);
            FocalParts.Clear();
            foreach (var p in snap.FocalParts ?? Enumerable.Empty<WizardFocalSection>()) FocalParts.Add(p);
            ConfiguredSentiments.Clear();
            foreach (var s in snap.ConfiguredSentiments ?? Enumerable.Empty<WizardConfiguredSentiment>()) ConfiguredSentiments.Add(s);
            AddedEmbellishments.Clear();
            foreach (var e in snap.AddedEmbellishments ?? Enumerable.Empty<WizardEmbellishment>()) AddedEmbellishments.Add(e);
            SelectedEnvelopeItem = snap.SelectedEnvelopeItem;
            SelectedStorageBagItem = snap.SelectedStorageBagItem;

            // Inside collections
            InsideBgMats.Clear();
            foreach (var m in snap.InsideBgMats ?? Enumerable.Empty<WizardBgMat>()) InsideBgMats.Add(m);
            InsideAdditionalMats.Clear();
            foreach (var m in snap.InsideAdditionalMats ?? Enumerable.Empty<WizardBgMat>()) InsideAdditionalMats.Add(m);
            // InsideFocal is a get-only property that we mutate in place, so copy fields.
            // For simplicity we accept that loading replaces nothing here unless the
            // wizard's InsideFocal was a property with a setter. (Currently it's get-only.)
            ConfiguredInsideSentiments.Clear();
            foreach (var s in snap.ConfiguredInsideSentiments ?? Enumerable.Empty<WizardConfiguredSentiment>()) ConfiguredInsideSentiments.Add(s);
            InsideAddedEmbellishments.Clear();
            foreach (var e in snap.InsideAddedEmbellishments ?? Enumerable.Empty<WizardEmbellishment>()) InsideAddedEmbellishments.Add(e);

            // Inside hub: liner cardstock + misc Details
            SelectedInsideLinerCardstockColor = snap.InsideLinerCardstockColor;
            if (snap.InsideLinerCardstockItemId is int liner)
            {
                // Pull from the picker's filtered set (loaded via Load() above).
                // Falls back to null if the item no longer exists in inventory.
                SelectedInsideLinerCardstockItem =
                    InsideLinerCardstockPicker.FilteredItems.FirstOrDefault(o => o.Id == liner);
                InsideLinerCardstockPicker.SelectedItem = SelectedInsideLinerCardstockItem;
            }
            InsideCardstockSaved = SelectedInsideLinerCardstockItem != null
                                   || !string.IsNullOrEmpty(SelectedInsideLinerCardstockColor);

            InsideMiscDetails.Clear();
            foreach (var d in snap.InsideMiscDetails ?? Enumerable.Empty<WizardDetailEntry>())
                InsideMiscDetails.Add(d);
            InsideDetailsSaved = InsideMiscDetails.Count > 0;

            UpdateSummaryLines();
        }
    }
}
