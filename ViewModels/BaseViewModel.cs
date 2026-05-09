using CommunityToolkit.Mvvm.ComponentModel;

namespace MyCraftyStash.ViewModels
{
    public partial class BaseViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isLoading;
        
        [ObservableProperty]
        private string? _errorMessage;
    }
}
