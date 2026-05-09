using System.Windows;
using System.Windows.Controls;

namespace MyCraftyStash.Views
{
    public partial class WishlistView : UserControl
    {
        // Pixels of horizontal travel per chevron click — roughly one tab's width
        // so each click pages through ~one list. Smooth-scroll isn't worth the
        // animation infrastructure for a single button press.
        private const double TabScrollStep = 180;

        public WishlistView()
        {
            InitializeComponent();
        }

        private void TabScrollLeft_Click(object sender, RoutedEventArgs e)
        {
            if (TabScrollViewer == null) return;
            var target = System.Math.Max(0, TabScrollViewer.HorizontalOffset - TabScrollStep);
            TabScrollViewer.ScrollToHorizontalOffset(target);
        }

        private void TabScrollRight_Click(object sender, RoutedEventArgs e)
        {
            if (TabScrollViewer == null) return;
            var max = TabScrollViewer.ScrollableWidth;
            var target = System.Math.Min(max, TabScrollViewer.HorizontalOffset + TabScrollStep);
            TabScrollViewer.ScrollToHorizontalOffset(target);
        }

        // Keep the chevrons hidden when there's nothing to scroll to in that
        // direction — avoids decoy buttons when the user only has 2-3 lists.
        private void TabScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (TabScrollViewer == null) return;
            var canLeft  = TabScrollViewer.HorizontalOffset > 0.5;
            var canRight = TabScrollViewer.HorizontalOffset < TabScrollViewer.ScrollableWidth - 0.5;
            if (TabScrollLeftButton != null)
                TabScrollLeftButton.Visibility = canLeft ? Visibility.Visible : Visibility.Collapsed;
            if (TabScrollRightButton != null)
                TabScrollRightButton.Visibility = canRight ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
