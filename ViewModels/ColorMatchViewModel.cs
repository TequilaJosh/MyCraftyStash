using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JandH.Core.Models;
using JandH.Core.Services;
using MyCraftyStash.Models;
using JandH.Core.Models;
using JandH.Core.Services;
using MyCraftyStash.Services;

using JandH.Core.ViewModels;

namespace MyCraftyStash.ViewModels
{
    /// <summary>
    /// Top-level VM for the Color Match section. Owns one child VM per
    /// supported external system (DMC floss, OLO markers). The view is a
    /// tabbed UserControl mirroring the Social section's pattern.
    /// </summary>
    public partial class ColorMatchViewModel : BaseViewModel
    {
        public SystemColorMatchViewModel DmcVM { get; }
        public SystemColorMatchViewModel OloVM { get; }

        [ObservableProperty]
        private string _activeColorMatchTab = "DMC";

        public ColorMatchViewModel()
        {
            var service = new ColorMatchService();
            DmcVM = new SystemColorMatchViewModel(service, ColorMatchService.SystemDmc, "DMC Floss");
            OloVM = new SystemColorMatchViewModel(service, ColorMatchService.SystemOlo, "OLO Marker");
        }

        [RelayCommand] private void ShowDmc() => ActiveColorMatchTab = "DMC";
        [RelayCommand] private void ShowOlo() => ActiveColorMatchTab = "OLO";

        public void Load()
        {
            DmcVM.Reload();
            OloVM.Reload();
        }
    }

    /// <summary>
    /// Per-system color-match row that wraps a stored <see cref="ColorMatch"/>
    /// with two ownership signals — whether the user owns the matching
    /// Taylored Expressions color (TE-side) and whether they own the
    /// matching DMC floss / OLO marker (external-side). The tooltip and
    /// the row colour change based on both signals.
    /// </summary>
    public class ColorMatchRow : ObservableObject
    {
        public ColorMatch Match { get; init; } = new();

        /// <summary>True when the user has the TE color (ink, cardstock,
        /// envelope, etc.) matching this row's TeColorName.</summary>
        public bool IsOwnedTe { get; init; }

        /// <summary>True when the user has a DMC/OLO supply item whose
        /// ItemNumber or Name contains this row's ExternalCode.</summary>
        public bool IsOwnedExternal { get; init; }

        // Combined: rows you can act on (cross-stitch, color-with-marker,
        // etc.) need BOTH sides — that's "ready". Anything missing is
        // either "owned the TE color, need to order the supply" or vice
        // versa. Filter chips key off these.
        public bool IsFullyReady => IsOwnedTe && IsOwnedExternal;
        public bool IsAnyOwned   => IsOwnedTe || IsOwnedExternal;

        // Direct property surface so XAML bindings don't have to dot through
        // .Match. Identity fields are init-only; Notes is editable and
        // persists through the SaveNotes callback wired up by the VM.
        public string ExternalCode => Match.ExternalCode;
        public string TeColorName  => Match.TeColorName;
        public string  System      => Match.System;

        /// <summary>
        /// Editable per-row note. Setter pushes through to the backing
        /// <see cref="ColorMatch"/>, raises PropertyChanged for the
        /// HoverHint dependent, and fires <see cref="SaveNotes"/> so the VM
        /// can persist. Whitespace-only values are stored as null so the DB
        /// stays clean.
        /// </summary>
        public string? Notes
        {
            get => Match.Notes;
            set
            {
                var normalised = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
                if (Match.Notes == normalised) return;
                Match.Notes = normalised;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HoverHint));
                SaveNotes?.Invoke(this);
            }
        }

        /// <summary>VM-supplied callback that persists Notes to the database.
        /// Wired up after each row is constructed in Reload.</summary>
        public Action<ColorMatchRow>? SaveNotes { get; set; }

        /// <summary>TE-side glyph for the column header.</summary>
        public string TeGlyph => IsOwnedTe ? "✓" : "○";
        /// <summary>External-side glyph for the column header.</summary>
        public string ExternalGlyph => IsOwnedExternal ? "✓" : "○";

        /// <summary>Tooltip rendered on row hover. Branches on which sides
        /// the user already has so the message is always actionable.</summary>
        public string HoverHint
        {
            get
            {
                var systemLabel = Match.System switch
                {
                    "DMC" => $"DMC floss {ExternalCode}",
                    "OLO" => $"OLO marker {ExternalCode}",
                    _     => $"{Match.System} {ExternalCode}",
                };
                var nl = global::System.Environment.NewLine;
                string body = (IsOwnedTe, IsOwnedExternal) switch
                {
                    (true,  true)  => $"You own both {TeColorName} and {systemLabel}." + nl + "Set — no need to order anything.",
                    (true,  false) => $"You own {TeColorName} but not {systemLabel}." + nl + $"Order {systemLabel} to match {TeColorName}.",
                    (false, true)  => $"You own {systemLabel} but not {TeColorName}." + nl + $"Get {TeColorName} to use this {Match.System.ToLowerInvariant()} on TE projects.",
                    (false, false) => $"Missing both {TeColorName} and {systemLabel}." + nl + $"Order both to use this match.",
                };
                if (!string.IsNullOrWhiteSpace(Notes)) body += nl + $"Notes: {Notes}";
                return body;
            }
        }
    }

    /// <summary>
    /// Per-system color-match VM. Same shape for DMC and OLO, parameterized
    /// on the system identifier so adding a third system (Copic, Spectrum
    /// Noir, …) is a single line.
    /// </summary>
    public partial class SystemColorMatchViewModel : BaseViewModel
    {
        public enum FilterMode { All, Owned, Missing }

        private readonly ColorMatchService _service;
        public string System { get; }
        public string DisplayName { get; }

        /// <summary>The full row set for this system, untouched by search/filter.</summary>
        private List<ColorMatchRow> _all = new();

        public ObservableCollection<ColorMatchRow> Filtered { get; } = new();

        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private ColorMatchRow? _selected;

        // Filter state — one of {All, Owned, Missing}.
        [ObservableProperty] private FilterMode _filter = FilterMode.All;

        // Bound by the radio toggles since FilterMode enum bindings are
        // awkward in XAML.
        public bool ShowAll
        {
            get => Filter == FilterMode.All;
            set { if (value) Filter = FilterMode.All; }
        }
        public bool ShowOwned
        {
            get => Filter == FilterMode.Owned;
            set { if (value) Filter = FilterMode.Owned; }
        }
        public bool ShowMissing
        {
            get => Filter == FilterMode.Missing;
            set { if (value) Filter = FilterMode.Missing; }
        }

        // Summary stats for the header.
        [ObservableProperty] private int _ownedCount;
        [ObservableProperty] private int _totalCount;
        [ObservableProperty] private int _externalOwnedCount;

        public string SummaryText
        {
            get
            {
                if (TotalCount == 0) return string.Empty;
                var systemLabel = System switch { "DMC" => "DMC", "OLO" => "OLO", _ => System };
                var pct = (int)Math.Round(100.0 * OwnedCount / TotalCount);
                return $"You own {OwnedCount} of {TotalCount} TE colors ({pct}%) · {ExternalOwnedCount} matching {systemLabel} supplies in your inventory";
            }
        }

        // Diagnostic info shown in the empty state so we can tell whether
        // the DB is genuinely empty or the filter is hiding everything.
        [ObservableProperty] private string _diagnosticText = string.Empty;
        public bool ShowDiagnostic => Filtered.Count == 0;

        public SystemColorMatchViewModel(ColorMatchService service, string system, string displayName)
        {
            _service = service;
            System = system;
            DisplayName = displayName;
        }

        partial void OnSearchTextChanged(string value) => ApplyFilter();
        partial void OnFilterChanged(FilterMode value)
        {
            OnPropertyChanged(nameof(ShowAll));
            OnPropertyChanged(nameof(ShowOwned));
            OnPropertyChanged(nameof(ShowMissing));
            ApplyFilter();
        }

        partial void OnOwnedCountChanged(int value) => OnPropertyChanged(nameof(SummaryText));
        partial void OnTotalCountChanged(int value) => OnPropertyChanged(nameof(SummaryText));
        partial void OnExternalOwnedCountChanged(int value) => OnPropertyChanged(nameof(SummaryText));

        public void Reload()
        {
            // Re-attempt seeding in case the first try ran before migrations
            // had landed. EnsureSeeded short-circuits when it already
            // succeeded, so this is cheap on the happy path.
            _service.EnsureSeeded();
            var matches = _service.GetAll(System);
            var ownedTe = _service.GetOwnedTeColorNames();
            var ownedExternalStrings = _service.GetOwnedExternalCodeStrings(System);

            bool ExternalOwned(string code)
            {
                if (string.IsNullOrWhiteSpace(code)) return false;
                // Substring matching was too loose — chart code "K" (Oreo) would
                // light up for any owned item whose name contained a "k" (e.g.
                // "RV1.3 Pink Lotus"). Match on word boundaries instead so a
                // code only counts when it appears as a whole token. \b around
                // the escaped code still permits prefix-with-dash forms like
                // "B-RV1.3" matching chart row "RV1.3", since "-" is a non-word
                // character and creates a boundary.
                var pattern = $@"\b{Regex.Escape(code)}\b";
                return ownedExternalStrings.Any(s =>
                    Regex.IsMatch(s, pattern, RegexOptions.IgnoreCase));
            }

            _all = matches.Select(m => new ColorMatchRow
            {
                Match           = m,
                IsOwnedTe       = ownedTe.Contains(m.TeColorName),
                IsOwnedExternal = ExternalOwned(m.ExternalCode),
            }).ToList();

            // Wire each row to persist notes back through the service the
            // moment the binding pushes a change.
            foreach (var row in _all)
                row.SaveNotes = r => _service.UpdateNotes(r.Match.Id, r.Match.Notes);

            // Distinct TE color names — the owned percentage should reflect
            // colors, not chart rows (since some TE colors have multiple
            // matching codes).
            var distinct = _all
                .GroupBy(r => r.TeColorName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Any(r => r.IsOwnedTe))
                .ToList();
            TotalCount = distinct.Count;
            OwnedCount = distinct.Count(o => o);
            ExternalOwnedCount = _all.Count(r => r.IsOwnedExternal);

            DiagnosticText = $"Loaded {_all.Count} {System} mappings from {AppPaths.SettingsDbPath}";
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            Filtered.Clear();
            var q = (SearchText ?? string.Empty).Trim();
            IEnumerable<ColorMatchRow> rows = _all;

            // Owned/Missing key off "either side owned" so a match still
            // surfaces under Owned if the user has just the external supply
            // (e.g. 1 OLO) without the TE color, and vice versa.
            rows = Filter switch
            {
                FilterMode.Owned   => rows.Where(r => r.IsAnyOwned),
                FilterMode.Missing => rows.Where(r => !r.IsAnyOwned),
                _ => rows,
            };

            if (q.Length > 0)
            {
                rows = rows.Where(c =>
                    (c.ExternalCode?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (c.TeColorName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (c.Notes?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
            }
            foreach (var r in rows.OrderBy(r => r.TeColorName).ThenBy(r => r.ExternalCode))
                Filtered.Add(r);
            OnPropertyChanged(nameof(ShowDiagnostic));
        }

        [RelayCommand] private void Refresh() => Reload();
        [RelayCommand] private void FilterAll()     => Filter = FilterMode.All;
        [RelayCommand] private void FilterOwned()   => Filter = FilterMode.Owned;
        [RelayCommand] private void FilterMissing() => Filter = FilterMode.Missing;
    }
}
