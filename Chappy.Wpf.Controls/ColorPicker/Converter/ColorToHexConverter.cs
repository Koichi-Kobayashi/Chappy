using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Chappy.Wpf.Controls.ColorPicker.Converter;

/// <summary>
/// Colorを16進数文字列に変換するコンバーター
/// デフォルトは"#AARRGGBB"形式、parameter="RGB"の場合は"#RRGGBB"形式
/// </summary>
public sealed class ColorToHexConverter : IValueConverter
{
    /// <summary>
    /// Colorを16進数文字列に変換する
    /// </summary>
    /// <param name="value">変換元の値（Color）</param>
    /// <param name="targetType">変換先の型</param>
    /// <param name="parameter">"RGB"の場合はアルファ値を含まない形式、それ以外は"#AARRGGBB"形式</param>
    /// <param name="culture">カルチャー情報</param>
    /// <returns>16進数文字列</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not Color c) return "";

        var mode = parameter?.ToString()?.ToUpperInvariant();
        if (mode == "RGB")
            return $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        return $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
    }

    /// <summary>
    /// 逆変換はサポートされていません
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
