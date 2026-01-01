using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Chappy.Wpf.Controls.Converter
{
    /// <summary>
    /// SelectedColor を指定した背景色と Alpha Blend した結果の Color を返す
    /// </summary>
    public sealed class ColorAlphaBlendConverter : IValueConverter
    {
        // parameter に "#FFFFFFFF" などの背景色を渡す
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not Color src)
                return Colors.Transparent;

            var bg = Colors.White;
            if (parameter is string s)
            {
                try
                {
                    bg = (Color)ColorConverter.ConvertFromString(s);
                }
                catch { }
            }

            double a = src.A / 255.0;

            byte r = (byte)Math.Round(src.R * a + bg.R * (1 - a));
            byte g = (byte)Math.Round(src.G * a + bg.G * (1 - a));
            byte b = (byte)Math.Round(src.B * a + bg.B * (1 - a));

            return Color.FromArgb(255, r, g, b); // 結果は常に不透明
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
