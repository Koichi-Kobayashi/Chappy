using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Chappy.Wpf.Controls.ColorPicker.Converter;

/// <summary>
/// 色を背景色とアルファブレンドして16進数文字列に変換するコンバーター
/// </summary>
public sealed class ColorAlphaBlendToHexConverter : IValueConverter
{
    /// <summary>
    /// 色を背景色とアルファブレンドして16進数文字列に変換する
    /// </summary>
    /// <param name="value">変換元の値（Color）</param>
    /// <param name="targetType">変換先の型</param>
    /// <param name="parameter">背景色（"#FFFFFFFF"などの文字列形式）</param>
    /// <param name="culture">カルチャー情報</param>
    /// <returns>16進数文字列（"#FFRRGGBB"形式）</returns>
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

    /// <summary>
    /// 逆変換はサポートされていません
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
