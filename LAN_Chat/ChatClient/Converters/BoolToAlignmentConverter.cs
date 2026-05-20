using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ChatClient.Converters;

/// <summary>
/// Converts IsOwnMessage (bool) to HorizontalAlignment: true → Right, false → Left.
/// </summary>
public class BoolToAlignmentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? HorizontalAlignment.Right : HorizontalAlignment.Left;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
