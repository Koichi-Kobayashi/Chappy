using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Chappy.Wpf.Controls.ColorPicker.Converter;

/// <summary>
/// SelectedColorを指定した背景色とAlpha Blendした結果のColorを返すコンバーター
/// </summary>
public sealed class ColorAlphaBlendConverter : IValueConverter
{
    /// <summary>
    /// 色を背景色とアルファブレンドする
    /// </summary>
    /// <param name="value">変換元の値（Color）</param>
    /// <param name="targetType">変換先の型</param>
    /// <param name="parameter">背景色（"#FFFFFFFF"などの文字列形式）</param>
    /// <param name="culture">カルチャー情報</param>
    /// <returns>アルファブレンドされたColor（不透明）</returns>
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

    /// <summary>
    /// 逆変換はサポートされていません
    /// </summary>
    /// <param name="value">変換元の値</param>
    /// <param name="targetType">変換先の型</param>
    /// <param name="parameter">パラメータ</param>
    /// <param name="culture">カルチャー情報</param>
    /// <returns>常にNotSupportedExceptionをスロー</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
