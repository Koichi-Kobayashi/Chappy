using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Chappy.Wpf.Controls.Uitl;

namespace Chappy.Wpf.Controls.ColorPicker.Converter;

/// <summary>
/// 複数の値を組み合わせてアルファブレンドした16進数文字列に変換するコンバーター
/// values[0]: SelectedColor (Color)
/// values[1]: Alpha slider value (double 0..1)
/// parameter: 背景色 "#FFFFFFFF" など
/// </summary>
public sealed class ColorAlphaBlendToHexMultiConverter : IMultiValueConverter
{
    /// <summary>
    /// 複数の値を組み合わせてアルファブレンドした16進数文字列に変換する
    /// </summary>
    /// <param name="values">変換元の値の配列（ColorとAlpha値）</param>
    /// <param name="targetType">変換先の型</param>
    /// <param name="parameter">背景色（"#FFFFFFFF"などの文字列形式）</param>
    /// <param name="culture">カルチャー情報</param>
    /// <returns>16進数文字列（"#FFRRGGBB"形式）</returns>
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
            return "";

        if (values[0] is not Color src)
            return "";

        if (values[1] is not double alpha)
            return "";

        // 背景色（省略時は白）
        var bg = Colors.White;
        if (parameter is string s)
        {
            try { bg = (Color)ColorConverter.ConvertFromString(s); }
            catch { }
        }

        alpha = MathUtil.Clamp(alpha, 0.0, 1.0);

        byte r = (byte)Math.Round(src.R * alpha + bg.R * (1 - alpha));
        byte g = (byte)Math.Round(src.G * alpha + bg.G * (1 - alpha));
        byte b = (byte)Math.Round(src.B * alpha + bg.B * (1 - alpha));

        return $"#FF{r:X2}{g:X2}{b:X2}";
    }

    /// <summary>
    /// 逆変換はサポートされていません
    /// </summary>
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

}
