using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyCraftyStash.Models;
using MyCraftyStash.Services;

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
    /// with the user-facing IsOwned flag (computed against inventory) and
    /// the action tooltip shown on hover.
    /// </summary>
    public class ColorMatchRow
    {
        public ColorMatch Match { get; init; } = new();
        public bool IsOwned { get; init; }

        // Direct property surface so XAML bindings don't have to dot through
        // .Match. Read-only — edits go through the service.
        public string ExternalCode => Match.ExternalCode;
        public string TeColorName  => Match.TeColorName;
        public string? Notes       => Match.Notes;
        public string  System      => Match.System;

        /// <summary>Glyph for the Owned column. ✓ when in inventory, ○ otherwise.</summary>
        public string OwnedGlyph => IsOwned ? "✓" : "○";

        /// <summary>Tooltip rendered on row hover. Reads e.g. "Order DMC 902
        /// to match Mulled Wine — already in your inventory."</summary>
        public string HoverHint
        {
            get
            {
                var systemLabel = Match.System switch
                {
                    "DMC" => "DMC floss",
                    "OLO" => "OLO marker",
                    _     => Match.System,
                };
                var head = IsOwned
                    ? $"You own {TeColorName}."
                    : $"Missing from your stash: {TeColorName}.";
                var order = $"Order {systemLabel} {ExternalCode} to match {TeColorName}.";
                if (!string.IsNullOrWhiteSpace(Notes)) order += $" ({Notes})";
                return head + global::System.Environment.NewLine + order;
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
        public string SummaryText => TotalCount == 0
            ? string.Empty
            : $"You own {OwnedCount} of {TotalCount} colors ({(int)Math.Round(100.0 * OwnedCount / TotalCount)}%)";

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

        public void Reload()
        {
            var matches = _service.GetAll(System);
            var owned = _service.GetOwnedTeColorNames();
            _all = matches.Select(m => new ColorMatchRow
            {
                Match   = m,
                IsOwned = owned.Contains(m.TeColorName),
            }).ToList();

            // Distinct TE color names — the owned percentage should reflect
            // colors, not chart rows (since some TE colors have multiple
            // matching codes).
            var distinct = _all
                .GroupBy(r => r.TeColorName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Any(r => r.IsOwned))
                .ToList();
            TotalCount = distinct.Count;
            OwnedCount = distinct.Count(o => o);

            ApplyFilter();
        }

        private void ApplyFilter()
        {
            Filtered.Clear();
            var q = (SearchText ?? string.Empty).Trim();
            IEnumerable<ColorMatchRow> rows = _all;

            rows = Filter switch
            {
                FilterMode.Owned   => rows.Where(r => r.IsOwned),
                FilterMode.Missing => rows.Where(r => !r.IsOwned),
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
        }

        [RelayCommand] private void Refresh() => Reload();
        [RelayCommand] private void FilterAll()     => Filter = FilterMode.All;
        [RelayCommand] private void FilterOwned()   => Filter = FilterMode.Owned;
        [RelayCommand] private void FilterMissing() => Filter = FilterMode.Missing;
    }
}
