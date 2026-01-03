using System;
using System.Globalization;
using System.Windows.Data;
using Chappy.Wpf.Controls.ColorPicker;

namespace Chappy.Wpf.Controls.ColorPicker.Converter;

/// <summary>
/// パレット色の名前を言語に応じて取得するコンバーター
/// values[0]: PaletteColor
/// values[1]: ToolTipLanguage ("ja"/"en")
/// parameter: "primary" または "secondary"
/// </summary>
public sealed class PaletteNameByLanguageConverter : IMultiValueConverter
{
    /// <summary>
    /// パレット色の名前を言語に応じて取得する
    /// </summary>
    /// <param name="values">変換元の値の配列（PaletteColorとToolTipLanguage）</param>
    /// <param name="targetType">変換先の型</param>
    /// <param name="parameter">"primary"の場合は指定言語の名前、"secondary"の場合は反対言語の名前</param>
    /// <param name="culture">カルチャー情報</param>
    /// <returns>言語に応じた色名</returns>
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2) return "";

        var pc = values[0] as PaletteColor;
        var lang = values[1] as string;

        if (pc == null) return "";

        var mode = (parameter as string) ?? "primary";
        var isEn = string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase);

        // primary: langに合わせた名前
        // secondary: 反対言語の名前（できれば両方、を満たす）
        if (string.Equals(mode, "secondary", StringComparison.OrdinalIgnoreCase))
            return isEn ? pc.NameJa : pc.NameEn;

        return isEn ? pc.NameEn : pc.NameJa;
    }

    /// <summary>
    /// 逆変換はサポートされていません
    /// </summary>
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
