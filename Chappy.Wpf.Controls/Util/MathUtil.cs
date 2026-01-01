namespace Chappy.Wpf.Controls.Uitl
{
    /// <summary>
    /// Math.Clampの代替版（.NET4.8に対応するため）
    /// </summary>
    internal static class MathUtil
    {
        internal static double Clamp(double v, double min, double max)
            => v < min ? min : (v > max ? max : v);
    }
}
