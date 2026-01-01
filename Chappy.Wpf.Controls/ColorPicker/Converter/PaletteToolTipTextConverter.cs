using System;
using System.Globalization;
using System.Windows.Data;
using Chappy.Wpf.Controls.ColorPicker;

namespace Chappy.Wpf.Controls.ColorPicker.Converter
{
    // values[0] = PaletteColor
    // values[1] = ToolTipLanguage ("ja"/"en")
    // return: "Amber\namber-500" or "アンバー\namber-500"
    public sealed class PaletteToolTipTextConverter : IMultiValueConverter
    {
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

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
