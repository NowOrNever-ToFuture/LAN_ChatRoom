using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ChatClient.Converters;

/// <summary>Returns Collapsed when value is true, Visible when false (inverse of BooleanToVisibilityConverter).</summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Collapsed;
}
