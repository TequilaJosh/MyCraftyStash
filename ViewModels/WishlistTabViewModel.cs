using CommunityToolkit.Mvvm.ComponentModel;
using MyCraftyStash.Models;

namespace MyCraftyStash.ViewModels
{
    public partial class WishlistTabViewModel : ObservableObject
    {
        public Wishlist Source { get; }

        [ObservableProperty] private string _name;
        [ObservableProperty] private string? _color;
        [ObservableProperty] private string? _description;
        [ObservableProperty] private int _itemCount;
        [ObservableProperty] private decimal _estimatedTotal;
        [ObservableProperty] private bool _isActive;

        public int Id => Source.Id;

        public WishlistTabViewModel(Wishlist src)
        {
            Source      = src;
            _name       = src.Name;
            _color      = src.Color;
            _description = src.Description;
        }

        public void SyncFromSource()
        {
            Name        = Source.Name;
            Color       = Source.Color;
            Description = Source.Description;
        }
    }
}
