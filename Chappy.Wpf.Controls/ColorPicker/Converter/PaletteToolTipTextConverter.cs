using System;
using System.Globalization;
using System.Windows.Data;
using Chappy.Wpf.Controls.ColorPicker;

namespace Chappy.Wpf.Controls.ColorPicker.Converter;

/// <summary>
/// パレット色のツールチップテキストを生成するコンバーター
/// values[0]: PaletteColor
/// values[1]: ToolTipLanguage ("ja"/"en")
/// 戻り値: "Amber\namber-500" または "アンバー\namber-500"
/// </summary>
public sealed class PaletteToolTipTextConverter : IMultiValueConverter
{
    /// <summary>
    /// パレット色のツールチップテキストを生成する
    /// </summary>
    /// <param name="values">変換元の値の配列（PaletteColorとToolTipLanguage）</param>
    /// <param name="targetType">変換先の型</param>
    /// <param name="parameter">パラメータ（未使用）</param>
    /// <param name="culture">カルチャー情報</param>
    /// <returns>色名とTailwind名を含むツールチップテキスト</returns>
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2) return "";

        var pc = values[0] as PaletteColor;
        var lang = values[1] as string;

        if (pc == null) return "";

        var isEn = string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase);
        var name = isEn ? pc.NameEn : pc.NameJa;

        return $"{name}\n{pc.TailwindName}";
    }

    /// <summary>
    /// 逆変換はサポートされていません
    /// </summary>
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
