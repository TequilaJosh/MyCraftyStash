using System.Windows;
using MyCraftyStash.ViewModels;

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
