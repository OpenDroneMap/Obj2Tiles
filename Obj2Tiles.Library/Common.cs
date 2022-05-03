using Obj2Tiles.Library.Geometry;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;

namespace Obj2Tiles.Library;

public static class Common
{
    public static double Epsilon = double.Epsilon;
    
    public static void CopyImage(Image source, Image dest, int sx, int sy, int sw, int sh, int dx, int dy)
    {
        dest.Mutate(x =>
        {
            x.Clip(new RectangularPolygon(dx, dy, sw, sh),
                ctx =>
                    ctx.DrawImage(source, new Point(dx - sx, dy - sy),
                        1f));
        });
    }
    
    public static double Area(Vertex2 a, Vertex2 b, Vertex2 c)
    {
        return Math.Abs(
            (a.X - c.X) * (b.Y - a.Y) -
            (a.X - b.X) * (c.Y - a.Y)
        ) / 2;
    }
}

public class FormattingStreamWriter : StreamWriter
{
    public FormattingStreamWriter(string path, IFormatProvider formatProvider)
        : base(path)
    {
        FormatProvider = formatProvider;
    }
    public override IFormatProvider FormatProvider { get; }
}