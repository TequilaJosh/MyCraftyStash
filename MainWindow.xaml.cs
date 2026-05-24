using System.Windows;
using System.Windows.Input;

using JandH.Core.Models;
using JandH.Core.Services;
using JandH.Core.ViewModels;

namespace MyCraftyStash
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // Handle mouse back button (XButton1 / Browser Back)
            MouseDown += MainWindow_MouseDown;
        }

        private void MainWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.XButton1)
            {
                e.Handled = true;
                TryGoBack();
            }
        }

        private void TryGoBack()
        {
            // Walk the visual tree to find the active view and call GoBack
            if (DataContext is ViewModels.MainViewModel mainVm)
            {
                if (mainVm.InventoryVM?.GoBackCommand?.CanExecute(null) == true)
                    mainVm.InventoryVM.GoBackCommand.Execute(null);
            }
        }
    }
}
