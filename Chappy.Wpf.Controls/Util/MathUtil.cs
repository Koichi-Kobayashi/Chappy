namespace Chappy.Wpf.Controls.Uitl
{
    /// <summary>
    /// 数学ユーティリティクラス
    /// Math.Clampの代替版（.NET4.8に対応するため）
    /// </summary>
    internal static class MathUtil
    {
        /// <summary>
        /// 値を指定された範囲内にクランプする
        /// </summary>
        /// <param name="v">クランプする値</param>
        /// <param name="min">最小値</param>
        /// <param name="max">最大値</param>
        /// <returns>クランプされた値</returns>
        internal static double Clamp(double v, double min, double max)
            => v < min ? min : (v > max ? max : v);
    }
}
