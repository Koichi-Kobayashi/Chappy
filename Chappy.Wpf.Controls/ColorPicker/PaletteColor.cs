using System.Windows.Media;

namespace Chappy.Wpf.Controls.ColorPicker;

/// <summary>
/// パレットに表示する色情報を表すクラス
/// </summary>
public sealed class PaletteColor
{
    /// <summary>日本語の色名</summary>
    public string NameJa { get; }
    /// <summary>英語の色名</summary>
    public string NameEn { get; }
    /// <summary>Tailwind CSSの色名（例: amber-500）</summary>
    public string TailwindName { get; }
    /// <summary>色</summary>
    public Color Color { get; }

    /// <summary>
    /// PaletteColorのインスタンスを初期化する
    /// </summary>
    /// <param name="nameJa">日本語の色名</param>
    /// <param name="nameEn">英語の色名</param>
    /// <param name="tailwindName">Tailwind CSSの色名</param>
    /// <param name="color">色</param>
    public PaletteColor(string nameJa, string nameEn, string tailwindName, Color color)
    {
        NameJa = nameJa;
        NameEn = nameEn;
        TailwindName = tailwindName;
        Color = color;
    }
}
