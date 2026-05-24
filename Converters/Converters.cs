using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using JandH.Core.Services;
using MyCraftyStash.Services;

using JandH.Core.Models;
using JandH.Core.ViewModels;

namespace MyCraftyStash.Converters
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool boolValue = value is bool b && b;
            bool inverse = parameter?.ToString() == "Inverse";
            
            if (inverse)
                boolValue = !boolValue;
                
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isNull = value == null || (value is string s && string.IsNullOrWhiteSpace(s));
            bool inverse = parameter?.ToString() == "Inverse";
            
            if (inverse)
                return isNull ? Visibility.Visible : Visibility.Collapsed;
                
            return isNull ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool hasItems;
            if (value is int i)
                hasItems = i > 0;
            else if (value is string s)
                hasItems = !string.IsNullOrEmpty(s);
            else
                hasItems = value != null;
            
            bool inverse = parameter?.ToString() == "Inverse";
            if (inverse) hasItems = !hasItems;
            
            return hasItems ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class GreaterThanOneToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int count = value is int i ? i : 0;
            return count > 1 ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class InverseNullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isNull = value == null || (value is string s && string.IsNullOrWhiteSpace(s));
            return isNull ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    public class StringToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !string.IsNullOrEmpty(value as string);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    public class StringEqualsVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string strValue = value?.ToString() ?? "";
            string compareValue = parameter?.ToString() ?? "";
            return strValue == compareValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    public class EqualityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
                return false;
            return values[0] == values[1];
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Returns Visible when the first input (a WizardItemOption) appears in the
    /// second input (an enumerable of WizardItemOption). The third input is
    /// expected to be the collection's Count — bound to force re-evaluation when
    /// the collection mutates (ObservableCollection reference itself doesn't change).
    /// Used by the multi-select wizard picker to show a checkmark on selected rows.
    /// </summary>
    public class ItemInCollectionToVisConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2) return System.Windows.Visibility.Collapsed;
            var item = values[0] as JandH.Core.Services.WizardItemOption;
            var coll = values[1] as System.Collections.IEnumerable;
            if (item == null || coll == null) return System.Windows.Visibility.Collapsed;
            foreach (var o in coll)
            {
                if (o is JandH.Core.Services.WizardItemOption other && other.Id == item.Id)
                    return System.Windows.Visibility.Visible;
            }
            return System.Windows.Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    public class ZeroToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int count = value is int i ? i : 0;
            bool inverse = parameter?.ToString() == "Inverse";
            
            if (inverse)
                return count == 0 ? Visibility.Collapsed : Visibility.Visible;
                
            return count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    public class NullToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    public class StringContainsVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string strValue = value?.ToString() ?? "";
            string searchValue = parameter?.ToString() ?? "";
            bool contains = strValue.IndexOf(searchValue, StringComparison.OrdinalIgnoreCase) >= 0;
            return contains ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Combines Type and Subtype into "Type - Subtype" or just "Type" if Subtype is empty.
    /// Bindings: [0] = Type, [1] = Subtype
    /// </summary>
    public class TypeWithSubtypeConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            string type = values.Length > 0 ? values[0]?.ToString() ?? "" : "";
            string subtype = values.Length > 1 ? values[1]?.ToString() ?? "" : "";
            return string.IsNullOrWhiteSpace(subtype) ? type : $"{type} - {subtype}";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Returns Visible if the bound string is an inventory-tracked item type (Cardstock, Envelopes, Foil-its, Foils).
    /// Returns Collapsed otherwise.
    /// </summary>
    public class IsTrackedTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string type && InventoryService.IsTrackedType(type))
                return Visibility.Visible;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Multi-value converter: [0]=Type (string), [1]=CurrentStock (int?)
    /// Returns a SolidColorBrush foreground color based on configured warning thresholds.
    /// Good = green, Low = yellow, Out = red, Unknown = muted grey.
    /// </summary>
    public class StockLevelForegroundConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            string type  = values.Length > 0 ? values[0] as string ?? string.Empty : string.Empty;
            int?   stock = values.Length > 1 && values[1] is int i ? i : (int?)null;

            return InventoryService.GetStockLevel(type, stock) switch
            {
                InventoryService.StockLevel.Good    => new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)),
                InventoryService.StockLevel.Low     => new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
                InventoryService.StockLevel.Out     => new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
                _                                   => new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)), // default green if no thresholds set
            };
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Multi-value converter: [0]=Type (string), [1]=CurrentStock (int?)
    /// Returns a SolidColorBrush background color (semi-transparent) for the stock badge.
    /// </summary>
    public class StockLevelBackgroundConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            string type  = values.Length > 0 ? values[0] as string ?? string.Empty : string.Empty;
            int?   stock = values.Length > 1 && values[1] is int i ? i : (int?)null;

            // parameter="solid" → full opacity (for card badge); default → semi-transparent (for detail pill)
            bool solid = parameter?.ToString() == "solid";

            return InventoryService.GetStockLevel(type, stock) switch
            {
                InventoryService.StockLevel.Good => solid
                    ? new SolidColorBrush(Color.FromArgb(0x20, 0x22, 0xC5, 0x5E))
                    : new SolidColorBrush(Color.FromArgb(0x14, 0x22, 0xC5, 0x5E)),
                InventoryService.StockLevel.Low  => solid
                    ? new SolidColorBrush(Color.FromArgb(0x30, 0xF5, 0x9E, 0x0B))
                    : new SolidColorBrush(Color.FromArgb(0x20, 0xF5, 0x9E, 0x0B)),
                InventoryService.StockLevel.Out  => solid
                    ? new SolidColorBrush(Color.FromArgb(0xCC, 0xEF, 0x44, 0x44))
                    : new SolidColorBrush(Color.FromArgb(0x20, 0xEF, 0x44, 0x44)),
                _                                => solid
                    ? new SolidColorBrush(Color.FromArgb(0x20, 0x22, 0xC5, 0x5E))
                    : new SolidColorBrush(Color.FromArgb(0x14, 0x22, 0xC5, 0x5E)),
            };
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool boolValue = value is bool b && b;
            return boolValue ? Visibility.Collapsed : Visibility.Visible;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class InverseCountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool hasItems;
            if (value is int i) hasItems = i > 0;
            else if (value is string s) hasItems = !string.IsNullOrEmpty(s);
            else hasItems = value != null;
            return hasItems ? Visibility.Collapsed : Visibility.Visible;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class HexToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var hex = value as string;
            if (string.IsNullOrWhiteSpace(hex)) hex = WishlistColorPalette.Default;
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex)!;
                return new SolidColorBrush(color);
            }
            catch
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(WishlistColorPalette.Default)!);
            }
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>Converts a URL to its display host (no scheme, no www., no path).</summary>
    public class UrlToDomainConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string s || string.IsNullOrWhiteSpace(s)) return string.Empty;
            if (!Uri.TryCreate(s, UriKind.Absolute, out var u)) return string.Empty;
            var host = u.Host;
            if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) host = host[4..];
            return host.ToLowerInvariant();
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}