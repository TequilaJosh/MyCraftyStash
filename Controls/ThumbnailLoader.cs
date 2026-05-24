using System.Windows;
using System.Windows.Controls;
using JandH.Core.Services;
using MyCraftyStash.Services;

using JandH.Core.Models;
using JandH.Core.ViewModels;

namespace MyCraftyStash.Controls
{
    /// <summary>
    /// Attached property for setting an Image.Source asynchronously from an item id
    /// via ThumbnailCacheService. Fixes the "blank thumbnail" bug that occurs when
    /// LazyThumbnailConverter sees a cache miss: the converter returns null and the
    /// async load result never makes it back to the binding because the source value
    /// (the id) hasn't changed.
    ///
    /// Usage:
    ///   &lt;Image controls:ThumbnailLoader.ItemId="{Binding Id}" Stretch="Uniform"/&gt;
    ///
    /// Behavior:
    ///   • Cache hit → sets Source synchronously on the next layout pass.
    ///   • Cache miss → clears Source, fires LoadThumbnailAsync, sets Source when done.
    ///   • Recycled rows (ItemsControl reuses Image instances) are handled by re-
    ///     checking the attached id before assigning, so an in-flight load for an old
    ///     id never overwrites a freshly bound image.
    /// </summary>
    public static class ThumbnailLoader
    {
        public static readonly DependencyProperty ItemIdProperty =
            DependencyProperty.RegisterAttached(
                "ItemId",
                typeof(int),
                typeof(ThumbnailLoader),
                new PropertyMetadata(0, OnItemIdChanged));

        public static int GetItemId(DependencyObject obj) => (int)obj.GetValue(ItemIdProperty);
        public static void SetItemId(DependencyObject obj, int value) => obj.SetValue(ItemIdProperty, value);

        private static void OnItemIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Image img) return;
            if (e.NewValue is not int id || id <= 0)
            {
                img.Source = null;
                return;
            }

            // Synchronous cache hit: set immediately, no async churn.
            var cached = ThumbnailCacheService.GetThumbnail(id);
            if (cached != null)
            {
                img.Source = cached;
                return;
            }

            // Cache miss: clear current image (in case the row was recycled from another id),
            // then load asynchronously. Wrapped so exceptions can't escape into the
            // DP-callback void return and tear down the visual tree.
            img.Source = null;
            _ = LoadAsync(img, id);
        }

        private static async Task LoadAsync(Image img, int id)
        {
            try
            {
                var bmp = await ThumbnailCacheService.LoadThumbnailAsync(id);
                // Re-check the attached id — ItemsControl recycles Image instances
                // during scroll/refilter, so an in-flight load for an old id must
                // not overwrite a freshly bound image.
                if (GetItemId(img) == id)
                    img.Source = bmp;
            }
            catch (Exception ex)
            {
                LoggingService.LogError(ex, $"ThumbnailLoader.LoadAsync id={id}");
            }
        }
    }
}
