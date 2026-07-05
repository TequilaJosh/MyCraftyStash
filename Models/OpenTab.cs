using CommunityToolkit.Mvvm.ComponentModel;

namespace MyCraftyStash.Models
{
    /// <summary>
    /// A tab in the Inventory/Projects tab strip. Tabs are lightweight bookmarks:
    /// activating one re-runs the normal detail-view load for its item/project,
    /// so the single detail panel is reused rather than keeping per-tab state.
    /// </summary>
    public partial class OpenTab : ObservableObject
    {
        public int Id { get; init; }

        [ObservableProperty]
        private string _title = string.Empty;

        /// <summary>True when this tab's item/project is the one shown in the detail panel.</summary>
        [ObservableProperty]
        private bool _isActive;
    }
}
