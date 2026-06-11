using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ChatClient.Converters;

public class OwnMessageForegroundConverter : IValueConverter
{
    private readonly SolidColorBrush _ownBrush = new SolidColorBrush(Colors.White);
    private readonly SolidColorBrush _otherBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF1E293B"));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isOwnMessage && isOwnMessage)
            return _ownBrush;
        return _otherBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
