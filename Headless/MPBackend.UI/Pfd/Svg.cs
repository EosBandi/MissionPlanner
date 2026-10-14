using System.Globalization;

namespace MPBackend.UI.Pfd;

/// <summary>
/// Number formatting and geometry helpers for the SVG instruments. Every numeric SVG attribute must go
/// through <see cref="F(double)"/>: attribute values are culture-sensitive otherwise ("1,5" breaks the path).
/// </summary>
public static class Svg
{
    public const double Deg = Math.PI / 180;

    public static string F(double v) => double.IsFinite(v) ? v.ToString("0.##", CultureInfo.InvariantCulture) : "0";
    public static string F(double v, string format) => double.IsFinite(v) ? v.ToString(format, CultureInfo.InvariantCulture) : "---";

    /// <summary>Heading as three digits, 360 instead of 000.</summary>
    public static string D3(double a)
    {
        var r = (int)Math.Round(Norm(a));
        if (r == 0) r = 360;
        return r.ToString("000", CultureInfo.InvariantCulture);
    }

    public static double Norm(double a) => ((a % 360) + 360) % 360;

    /// <summary>Shortest signed difference a - b in degrees (-180..180).</summary>
    public static double Wrap180(double a) => ((a + 540) % 360 + 360) % 360 - 180;

    public static double Clamp(double v, double lo, double hi) => Math.Max(lo, Math.Min(hi, v));

    public static bool IsMult(double v, double m) => Math.Abs(v / m - Math.Round(v / m)) < 1e-6;

    /// <summary>Point on a circle; angle in degrees, 0 = up, clockwise.</summary>
    public static (double X, double Y) Polar(double cx, double cy, double r, double angleDeg)
    {
        var rad = (angleDeg - 90) * Deg;
        return (cx + r * Math.Cos(rad), cy + r * Math.Sin(rad));
    }

    /// <summary>SVG arc path from angle a1 to a2 (degrees, 0 = up, clockwise) on radius r.</summary>
    public static string Arc(double cx, double cy, double r, double a1, double a2)
    {
        var (x1, y1) = Polar(cx, cy, r, a1);
        var (x2, y2) = Polar(cx, cy, r, a2);
        var large = Math.Abs(a2 - a1) > 180 ? 1 : 0;
        var sweep = a2 > a1 ? 1 : 0;
        return $"M {F(x1)} {F(y1)} A {F(r)} {F(r)} 0 {large} {sweep} {F(x2)} {F(y2)}";
    }
}
