using System.Globalization;
using System.Windows.Data;

namespace MyCraftyStash.Converters
{
    public class StringEqualsConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string strValue && parameter is string strParam)
            {
                return strValue == strParam;
            }
            return false;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // Used by RadioButton bindings: when the radio is checked, write the
            // parameter string back to the bound source. Unchecked just means the
            // user picked a different radio — leave the source alone (return
            // Binding.DoNothing) so the OTHER radio's ConvertBack drives the change.
            if (value is bool b && b && parameter is string s) return s;
            return System.Windows.Data.Binding.DoNothing;
        }
    }

    /// <summary>
    /// MultiBinding-friendly string equality. Returns true if values[0] and values[1]
    /// are equal as strings (case-insensitive). Used to highlight chip buttons whose
    /// label matches the active subtype.
    /// </summary>
    public class StringEqualsMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2) return false;
            var a = values[0]?.ToString();
            var b = values[1]?.ToString();
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
