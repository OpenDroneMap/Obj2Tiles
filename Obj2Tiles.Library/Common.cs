using Obj2Tiles.Library.Geometry;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Obj2Tiles.Library;

public static class Common
{
    public static double Epsilon = double.Epsilon * 10;
    
    public static void CopyImage(Image<Rgba32> sourceImage, Image<Rgba32> dest, int sourceX, int sourceY, int sourceWidth, int sourceHeight, int destX, int destY)
    {
        var height = sourceHeight;
        
        sourceImage.ProcessPixelRows(dest, (sourceAccessor, targetAccessor) =>
        {
            for (var i = 0; i < height; i++)
            {
                var sourceRow = sourceAccessor.GetRowSpan(sourceY + i);
                var targetRow = targetAccessor.GetRowSpan(i + destY);

                for (var x = 0; x < sourceWidth; x++)
                {
                    targetRow[x + destX] = sourceRow[x + sourceX];
                }
            }
        });
    }
    
    public static double Area(Vertex2 a, Vertex2 b, Vertex2 c)
    {
        return Math.Abs(
            (a.X - c.X) * (b.Y - a.Y) -
            (a.X - b.X) * (c.Y - a.Y)
        ) / 2;
    }

    public static int NextPowerOfTwo(int x)
    {
        x--;
        x |= (x >> 1);
        x |= (x >> 2);
        x |= (x >> 4);
        x |= (x >> 8);
        x |= (x >> 16);
        return (x + 1);
    }

    /// <summary>
    /// Gets the distance of P from A (in percent) relative to segment AB
    /// </summary>
    /// <param name="a">Edge start</param>
    /// <param name="b">Edge end</param>
    /// <param name="p">Point on the segment</param>
    /// <returns></returns>
    public static double GetIntersectionPerc(Vertex3 a, Vertex3 b, Vertex3 p)
    {
        var edge1Length = a.Distance(b);
        var subEdge1Length = a.Distance(p);
        return subEdge1Length / edge1Length;
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