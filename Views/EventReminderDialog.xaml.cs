using System.Windows;
using JandH.Core.ViewModels;
using MyCraftyStash.ViewModels;

using JandH.Core.Models;
using JandH.Core.Services;

namespace MyCraftyStash.Views
{
    public partial class EventReminderDialog : Window
    {
        public EventReminderDialog(EventReminderViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            vm.CloseRequested += () => Close();
        }
    }
}
