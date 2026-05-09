using CommunityToolkit.Mvvm.ComponentModel;

namespace MyCraftyStash.Models
{
    public partial class ThemeCheckboxItem : ObservableObject
    {
        private readonly Action? _onSelectionChanged;
        
        [ObservableProperty]
        private string _name = string.Empty;
        
        [ObservableProperty]
        private bool _isSelected;
        
        public ThemeCheckboxItem(string name, Action? onSelectionChanged = null)
        {
            Name = name;
            _onSelectionChanged = onSelectionChanged;
        }
        
        partial void OnIsSelectedChanged(bool value)
        {
            _onSelectionChanged?.Invoke();
        }
    }
}
