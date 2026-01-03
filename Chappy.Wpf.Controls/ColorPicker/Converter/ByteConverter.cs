using System.Globalization;
using System.Windows.Data;

namespace Chappy.Wpf.Controls.ColorPicker.Converter;

/// <summary>
/// byte値を文字列に変換するコンバーター
/// </summary>
public sealed class ByteConverter : IValueConverter
{
    /// <summary>
    /// byte値を文字列に変換する
    /// </summary>
    /// <param name="value">変換元の値（byte）</param>
    /// <param name="targetType">変換先の型</param>
    /// <param name="parameter">パラメータ（未使用）</param>
    /// <param name="culture">カルチャー情報</param>
    /// <returns>文字列に変換された値</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value?.ToString() ?? "0";

    /// <summary>
    /// 文字列をbyte値に変換する
    /// </summary>
    /// <param name="value">変換元の値（文字列）</param>
    /// <param name="targetType">変換先の型</param>
    /// <param name="parameter">パラメータ（未使用）</param>
    /// <param name="culture">カルチャー情報</param>
    /// <returns>byte値に変換された値、変換できない場合はBinding.DoNothing</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (byte.TryParse(value as string, out var b))
            return b;

        return Binding.DoNothing;
    }
}
