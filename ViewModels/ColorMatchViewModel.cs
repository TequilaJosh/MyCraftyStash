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
    /// Per-system color-match VM. Same shape for DMC and OLO, parameterized
    /// on the system identifier so adding a third system (Copic, Spectrum
    /// Noir, …) is a single line.
    /// </summary>
    public partial class SystemColorMatchViewModel : BaseViewModel
    {
        private readonly ColorMatchService _service;
        public string System { get; }
        public string DisplayName { get; }

        /// <summary>The full row set for this system, untouched by search.</summary>
        private List<ColorMatch> _all = new();

        public ObservableCollection<ColorMatch> Filtered { get; } = new();

        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private ColorMatch? _selected;

        public SystemColorMatchViewModel(ColorMatchService service, string system, string displayName)
        {
            _service = service;
            System = system;
            DisplayName = displayName;
        }

        partial void OnSearchTextChanged(string value) => ApplyFilter();

        public void Reload()
        {
            _all = _service.GetAll(System);
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            Filtered.Clear();
            var q = (SearchText ?? string.Empty).Trim();
            IEnumerable<ColorMatch> rows = _all;
            if (q.Length > 0)
            {
                rows = _all.Where(c =>
                    (c.ExternalCode?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (c.TeColorName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (c.Notes?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
            }
            foreach (var r in rows.OrderBy(r => r.TeColorName).ThenBy(r => r.ExternalCode))
                Filtered.Add(r);
        }

        [RelayCommand]
        private void Refresh() => Reload();
    }
}
