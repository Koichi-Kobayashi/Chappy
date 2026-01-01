using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Chappy.Wpf.Controls.Converter
{
    public sealed class ColorAlphaBlendToHexConverter : IValueConverter
    {
        // parameter: 背景色 "#FFFFFFFF" など
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not Color src)
                return "";

            var bg = Colors.White;
            if (parameter is string s)
            {
                try { bg = (Color)ColorConverter.ConvertFromString(s); }
                catch { }
            }

            double a = src.A / 255.0;

            byte r = (byte)Math.Round(src.R * a + bg.R * (1 - a));
            byte g = (byte)Math.Round(src.G * a + bg.G * (1 - a));
            byte b = (byte)Math.Round(src.B * a + bg.B * (1 - a));

            return $"#FF{r:X2}{g:X2}{b:X2}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
