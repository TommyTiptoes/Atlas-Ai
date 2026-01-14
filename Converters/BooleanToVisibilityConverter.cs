using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AtlasAI.Converters
{
    /// <summary>
    /// Converts Boolean to Visibility, with optional inversion.
    /// Parameter "Inverse" inverts the behavior.
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isVisible = value is bool b && b;
            bool inverse = parameter?.ToString()?.Equals("Inverse", StringComparison.OrdinalIgnoreCase) == true;

            if (inverse)
                isVisible = !isVisible;

            return isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isVisible = value is Visibility v && v == Visibility.Visible;
            bool inverse = parameter?.ToString()?.Equals("Inverse", StringComparison.OrdinalIgnoreCase) == true;

            if (inverse)
                isVisible = !isVisible;

            return isVisible;
        }
    }
}
