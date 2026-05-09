using System.Globalization;
using System.Windows.Data;
using MyCraftyStash.Services;

namespace MyCraftyStash.Converters
{
    public class LazyInspirationThumbnailConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not int imageId || imageId <= 0)
                return null;

            return InspirationThumbnailCacheService.LoadThumbnailSync(imageId);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
