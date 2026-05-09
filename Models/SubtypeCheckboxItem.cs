using CommunityToolkit.Mvvm.ComponentModel;

namespace MyCraftyStash.Models
{
    public partial class SubtypeCheckboxItem : ObservableObject
    {
        [ObservableProperty]
        private bool _isChecked;

        public string Label { get; set; } = string.Empty;

        /// <summary>Optional backing value when the display label differs from the filter key.</summary>
        public string? Tag { get; set; }
    }
}
