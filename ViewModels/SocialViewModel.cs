using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using JandH.Core.Models;
using JandH.Core.Services;
using JandH.Core.ViewModels;

namespace MyCraftyStash.ViewModels
{
    public partial class SocialViewModel : BaseViewModel
    {
        public AddressBookViewModel AddressBookVM { get; }
        public CalendarViewModel CalendarVM { get; }

        [ObservableProperty]
        private string _activeSocialTab = "Calendar";

        public SocialViewModel(MainViewModel mainVm)
        {
            AddressBookVM = new AddressBookViewModel(mainVm);
            CalendarVM = new CalendarViewModel(mainVm);
        }

        [RelayCommand]
        private void ShowCalendar() => ActiveSocialTab = "Calendar";

        [RelayCommand]
        private void ShowAddressBook() => ActiveSocialTab = "AddressBook";

        public async Task LoadAllAsync()
        {
            var tasks = new List<Task>
            {
                CalendarVM.LoadCalendar(),
                AddressBookVM.LoadEntriesCommand.ExecuteAsync(null)
            };
            await Task.WhenAll(tasks);
        }
    }
}
