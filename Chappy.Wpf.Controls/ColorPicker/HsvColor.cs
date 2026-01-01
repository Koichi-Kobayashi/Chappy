using System;
using System.Windows.Media;

namespace Chappy.Wpf.Controls.ColorPicker
{
    public readonly struct HsvColor
    {
        public double H { get; } // 0..360
        public double S { get; } // 0..1
        public double V { get; } // 0..1

        private HsvColor(double h, double s, double v)
        {
            H = NormalizeHue(h);
            S = Clamp01(s);
            V = Clamp01(v);
        }

        public static HsvColor FromHsv(double h, double s, double v) => new(h, s, v);

        public static HsvColor FromColor(Color c)
        {
            double r = c.R / 255.0;
            double g = c.G / 255.0;
            double b = c.B / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            double h;
            if (delta == 0)
                h = 0;
            else if (max == r)
                h = 60 * (((g - b) / delta) % 6);
            else if (max == g)
                h = 60 * (((b - r) / delta) + 2);
            else
                h = 60 * (((r - g) / delta) + 4);

            if (h < 0) h += 360;

            double s = max == 0 ? 0 : delta / max;
            double v = max;

            return new HsvColor(h, s, v);
        }

        public Color ToColor(double alpha)
        {
            alpha = Clamp01(alpha);

            double c = V * S;
            double x = c * (1 - Math.Abs(((H / 60) % 2) - 1));
            double m = V - c;

            double r1, g1, b1;
            if (H < 60) { r1 = c; g1 = x; b1 = 0; }
            else if (H < 120) { r1 = x; g1 = c; b1 = 0; }
            else if (H < 180) { r1 = 0; g1 = c; b1 = x; }
            else if (H < 240) { r1 = 0; g1 = x; b1 = c; }
            else if (H < 300) { r1 = x; g1 = 0; b1 = c; }
            else { r1 = c; g1 = 0; b1 = x; }

            byte A = (byte)Math.Round(alpha * 255);
            byte R = (byte)Math.Round((r1 + m) * 255);
            byte G = (byte)Math.Round((g1 + m) * 255);
            byte B = (byte)Math.Round((b1 + m) * 255);

            return Color.FromArgb(A, R, G, B);
        }

        private static double NormalizeHue(double h)
        {
            if (double.IsNaN(h) || double.IsInfinity(h)) return 0;
            h %= 360;
            if (h < 0) h += 360;
            return h;
        }

        private static double Clamp01(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return 0;
            if (v < 0) return 0;
            if (v > 1) return 1;
            return v;
        }
    }
}
