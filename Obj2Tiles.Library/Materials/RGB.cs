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
}