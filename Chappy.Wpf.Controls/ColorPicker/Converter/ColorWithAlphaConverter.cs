using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Chappy.Wpf.Controls.ColorPicker.Converter;

/// <summary>
/// Colorに指定されたアルファ値を設定するコンバーター
/// parameterに"0"や"255"などのアルファ値（byte 0-255）を渡す
/// </summary>
public class ColorWithAlphaConverter : IValueConverter
{
    /// <summary>
    /// Colorに指定されたアルファ値を設定する
    /// </summary>
    /// <param name="value">変換元の値（Color）</param>
    /// <param name="targetType">変換先の型</param>
    /// <param name="parameter">アルファ値（"0"や"255"などの文字列形式）</param>
    /// <param name="culture">カルチャー情報</param>
    /// <returns>アルファ値が設定されたColor</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not Color c) return Colors.Transparent;

        byte a = 255;
        if (parameter != null && byte.TryParse(parameter.ToString(), out var parsed))
            a = parsed;

        return Color.FromArgb(a, c.R, c.G, c.B);
    }

    /// <summary>
    /// 逆変換はサポートされていません
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
