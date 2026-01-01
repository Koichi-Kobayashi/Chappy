using System;
using System.Globalization;
using System.Windows.Data;
using Chappy.Wpf.Controls.ColorPicker;

namespace Chappy.Wpf.Controls.Converter
{
    /// <summary>
    /// values[0] = PaletteColor
    /// values[1] = ToolTipLanguage ("ja"/"en")
    /// parameter = "primary" または "secondary"
    /// </summary>
    public sealed class PaletteNameByLanguageConverter : IMultiValueConverter
    {
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

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
