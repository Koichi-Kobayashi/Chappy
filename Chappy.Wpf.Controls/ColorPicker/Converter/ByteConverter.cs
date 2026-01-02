using System.Globalization;
using System.Windows.Data;

namespace Chappy.Wpf.Controls.ColorPicker.Converter;

public sealed class ByteConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value?.ToString() ?? "0";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (byte.TryParse(value as string, out var b))
            return b;

        return Binding.DoNothing;
    }
}
