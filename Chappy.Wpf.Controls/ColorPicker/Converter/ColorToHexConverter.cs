using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Chappy.Wpf.Controls.ColorPicker.Converter
{
    /// <summary>
    /// Color -> "#AARRGGBB" (default) or "#RRGGBB" (ConverterParameter="RGB")
    /// </summary>
    public sealed class ColorToHexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not Color c) return "";

            var mode = parameter?.ToString()?.ToUpperInvariant();
            if (mode == "RGB")
                return $"#{c.R:X2}{c.G:X2}{c.B:X2}";

            return $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
