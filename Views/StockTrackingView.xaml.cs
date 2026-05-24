using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using JandH.Core.ViewModels;
using MyCraftyStash.ViewModels;

using JandH.Core.Models;
using JandH.Core.Services;

namespace MyCraftyStash.Views
{
    public partial class StockTrackingView : UserControl
    {
        public StockTrackingView()
        {
            InitializeComponent();
        }

        // ── Supply Stock handlers ──────────────────────────────────────────────

        private void EditStock_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is StockRowViewModel row)
                if (DataContext is StockTrackingViewModel vm)
                    vm.StartEditStockCommand.Execute(row);
        }

        private async void SaveStock_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is StockRowViewModel row)
                if (DataContext is StockTrackingViewModel vm)
                    await vm.SaveEditStockCommand.ExecuteAsync(row);
        }

        private void CancelStock_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is StockRowViewModel row)
                if (DataContext is StockTrackingViewModel vm)
                    vm.CancelEditStockCommand.Execute(row);
        }

        private async void ItemName_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock tb && tb.Tag is StockRowViewModel row)
                if (DataContext is StockTrackingViewModel vm)
                    await vm.GoToItemCommand.ExecuteAsync(row);
        }

        private async void StockTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox tb && tb.Tag is StockRowViewModel row && DataContext is StockTrackingViewModel vm)
            {
                if (e.Key == Key.Enter) await vm.SaveEditStockCommand.ExecuteAsync(row);
                else if (e.Key == Key.Escape) vm.CancelEditStockCommand.Execute(row);
            }
        }

        // ── Project Inventory handlers ─────────────────────────────────────────

        private void EditProjectStock_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ProjectInventoryRowViewModel row)
                if (DataContext is StockTrackingViewModel vm)
                    vm.StartEditProjectStockCommand.Execute(row);
        }

        private async void SaveProjectStock_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ProjectInventoryRowViewModel row)
                if (DataContext is StockTrackingViewModel vm)
                    await vm.SaveEditProjectStockCommand.ExecuteAsync(row);
        }

        private void CancelProjectStock_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ProjectInventoryRowViewModel row)
                if (DataContext is StockTrackingViewModel vm)
                    vm.CancelEditProjectStockCommand.Execute(row);
        }

        private async void ProjectStockTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox tb && tb.Tag is ProjectInventoryRowViewModel row && DataContext is StockTrackingViewModel vm)
            {
                if (e.Key == Key.Enter) await vm.SaveEditProjectStockCommand.ExecuteAsync(row);
                else if (e.Key == Key.Escape) vm.CancelEditProjectStockCommand.Execute(row);
            }
        }
    }
}
