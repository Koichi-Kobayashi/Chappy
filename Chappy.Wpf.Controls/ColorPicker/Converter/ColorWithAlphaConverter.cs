using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Chappy.Wpf.Controls.ColorPicker.Converter;

/// <summary>
/// Color + alpha(byte 0..255) -> Color
/// parameter に "0" や "255" を渡す
/// </summary>
public class ColorWithAlphaConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not Color c) return Colors.Transparent;

        byte a = 255;
        if (parameter != null && byte.TryParse(parameter.ToString(), out var parsed))
            a = parsed;

        return Color.FromArgb(a, c.R, c.G, c.B);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
