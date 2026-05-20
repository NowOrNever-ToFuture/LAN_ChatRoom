using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ChatClient.Converters;

/// <summary>
/// Converts IsOwnMessage (bool) to background Brush: true → teal, false → gray.
/// </summary>
public class BoolToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush OwnBrush = new(Color.FromRgb(0xE0, 0xF7, 0xFA));   // #E0F7FA teal light
    private static readonly SolidColorBrush OtherBrush = new(Color.FromRgb(0xF8, 0xFA, 0xFC));  // #F8FAFC gray light

    static BoolToBrushConverter()
    {
        OwnBrush.Freeze();
        OtherBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? OwnBrush : OtherBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
