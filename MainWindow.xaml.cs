using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace MyCraftyStash
{
    public partial class MainWindow : Window
    {
        private const string KoFiUrl = "https://ko-fi.com/mycraftystash";

        public MainWindow()
        {
            InitializeComponent();
            // Handle mouse back button (XButton1 / Browser Back)
            MouseDown += MainWindow_MouseDown;
        }

        private void KoFiButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = KoFiUrl,
                    UseShellExecute = true
                });
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(
                    $"Couldn't open Ko-fi link.\n\n{ex.Message}\n\nYou can visit:\n{KoFiUrl}",
                    "Ko-fi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
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
