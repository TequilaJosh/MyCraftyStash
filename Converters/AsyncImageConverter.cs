using System.Collections.Concurrent;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MyCraftyStash.Services;

namespace MyCraftyStash.Converters
{
    /// <summary>
    /// Converter for item thumbnails. Returns the cached bitmap immediately if available,
    /// or triggers an async load and uses a DependencyObject-based trick to refresh the
    /// binding once the image arrives - so no image is ever "stuck" as blank.
    /// </summary>
    public class AsyncThumbnailConverter : IMultiValueConverter
    {
        // Tracks items currently being loaded so we never double-fire
        private static readonly ConcurrentDictionary<int, bool> _loading = new();

        // Debounce timer: instead of calling Tick() per-item (which re-evaluates every
        // binding for every item load), we batch all completions into one Tick per 150ms.
        private static readonly DispatcherTimer _debounce = CreateDebounce();

        private static DispatcherTimer CreateDebounce()
        {
            var t = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            t.Tick += (_, _) =>
            {
                t.Stop();
                BindingRefreshProxy.Instance.Tick();
            };
            return t;
        }

        public object? Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Length == 0 || values[0] is not int itemId || itemId <= 0) return null;
            // values[1] is the refresh counter - reading it registers the binding dependency

            var cached = ThumbnailCacheService.GetThumbnail(itemId);
            if (cached != null) return cached;

            // Already processed (confirmed no image) - don't trigger another load
            if (ThumbnailCacheService.IsLoaded(itemId)) return null;

            // Only fire one load per item - _loading entry is kept until AFTER Tick fires
            if (_loading.TryAdd(itemId, true))
                _ = LoadAndNotify(itemId);

            return null;
        }

        private static async Task LoadAndNotify(int itemId)
        {
            try
            {
                await ThumbnailCacheService.LoadThumbnailAsync(itemId);
            }
            finally
            {
                // Keep the _loading guard in place until after the UI refreshes.
                // The guard is removed inside the dispatcher so Convert() cannot
                // re-enter and re-queue a load during the same Tick cycle.
                Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
                {
                    _loading.TryRemove(itemId, out _);
                    // Restart debounce - fires one Tick() ~150ms after the last load completes
                    _debounce.Stop();
                    _debounce.Start();
                });
            }
        }

        public object[]? ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>Same pattern for inspiration images.</summary>
    public class AsyncInspirationThumbnailConverter : IMultiValueConverter
    {
        private static readonly ConcurrentDictionary<int, bool> _loading = new();

        private static readonly DispatcherTimer _debounce = CreateDebounce();

        private static DispatcherTimer CreateDebounce()
        {
            var t = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            t.Tick += (_, _) =>
            {
                t.Stop();
                BindingRefreshProxy.Instance.Tick();
            };
            return t;
        }

        public object? Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Length == 0 || values[0] is not int imageId || imageId <= 0) return null;

            var cached = InspirationThumbnailCacheService.GetThumbnail(imageId);
            if (cached != null) return cached;

            // Already processed (confirmed no image) - don't trigger another load
            if (InspirationThumbnailCacheService.IsLoaded(imageId)) return null;

            if (_loading.TryAdd(imageId, true))
                _ = LoadAndNotify(imageId);

            return null;
        }

        private static async Task LoadAndNotify(int imageId)
        {
            try
            {
                await InspirationThumbnailCacheService.LoadThumbnailAsync(imageId);
            }
            finally
            {
                Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
                {
                    _loading.TryRemove(imageId, out _);
                    _debounce.Stop();
                    _debounce.Start();
                });
            }
        }

        public object[]? ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// A singleton observable object whose Counter increments each time any thumbnail batch
    /// finishes loading. Items bind their image Source to a MultiBinding that includes this
    /// counter so WPF re-evaluates the converter automatically when new images arrive.
    /// </summary>
    public class BindingRefreshProxy : System.ComponentModel.INotifyPropertyChanged
    {
        public static readonly BindingRefreshProxy Instance = new();
        private BindingRefreshProxy() { }

        private int _counter;
        public int Counter => _counter;

        public void Tick()
        {
            _counter++;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Counter)));
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}
