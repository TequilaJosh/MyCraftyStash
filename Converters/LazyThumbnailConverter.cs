using System.Globalization;
using System.Windows.Data;
using JandH.Core.Services;
using MyCraftyStash.Services;

using JandH.Core.Models;
using JandH.Core.ViewModels;

namespace MyCraftyStash.Converters
{
    public class LazyThumbnailConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not int itemId || itemId <= 0)
                return null;

            return ThumbnailCacheService.LoadThumbnailSync(itemId);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
