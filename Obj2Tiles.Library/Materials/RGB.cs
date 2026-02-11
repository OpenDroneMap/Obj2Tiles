using System.Globalization;

namespace Obj2Tiles.Library.Materials;

public class RGB
{
    public readonly double R;
    public readonly double G;
    public readonly double B;

    public RGB(double r, double g, double b)
    {
        R = r;
        G = g;
        B = b;
    }

    public override string ToString()
    {
        return string.Format(CultureInfo.InvariantCulture, "{0} {1} {2}", R, G, B);
    }

    /// <summary>
    /// Linearly interpolates this color towards <paramref name="b"/> by <paramref name="perc"/>.
    /// </summary>
    public RGB CutEdgePerc(RGB b, double perc)
    {
        return new RGB(
            (b.R - R) * perc + R,
            (b.G - G) * perc + G,
            (b.B - B) * perc + B);
    }

    protected bool Equals(RGB other)
    {
        return R.Equals(other.R) && G.Equals(other.G) && B.Equals(other.B);
    }

    public override bool Equals(object? obj)
    {
        return !ReferenceEquals(null, obj) &&
               (ReferenceEquals(this, obj) || obj.GetType() == GetType() && Equals((RGB)obj));
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(R, G, B);
    }

    #region sRGB to Linear conversion (IEC 61966-2-1)

    private const double SrgbThreshold = 0.04045;
    private const double SrgbLinearScale = 12.92;
    private const double SrgbOffset = 0.055;
    private const double SrgbScale = 1.055;
    private const double SrgbGamma = 2.4;

    private static double SrgbChannelToLinear(double c)
    {
        return c <= SrgbThreshold
            ? c / SrgbLinearScale
            : Math.Pow((c + SrgbOffset) / SrgbScale, SrgbGamma);
    }

    /// <summary>
    /// Converts sRGB color values to linear RGB (required by glTF COLOR_0 attribute).
    /// </summary>
    public static RGB SrgbToLinear(RGB color)
    {
        return new RGB(
            SrgbChannelToLinear(color.R),
            SrgbChannelToLinear(color.G),
            SrgbChannelToLinear(color.B));
    }

    #endregion
}