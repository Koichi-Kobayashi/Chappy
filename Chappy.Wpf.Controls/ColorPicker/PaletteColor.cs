using System.Windows.Media;

namespace Chappy.Wpf.Controls.ColorPicker
{
    // DisplayName = 人間向け色名（日本語/英語どちらでも）
    // TailwindName = tailwind名（例: amber-500）
    public sealed class PaletteColor
    {
        public string NameJa { get; }
        public string NameEn { get; }
        public string TailwindName { get; }
        public Color Color { get; }

        public PaletteColor(string nameJa, string nameEn, string tailwindName, Color color)
        {
            NameJa = nameJa;
            NameEn = nameEn;
            TailwindName = tailwindName;
            Color = color;
        }
    }
}
