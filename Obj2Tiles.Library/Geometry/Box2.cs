namespace Obj2Tiles.Library.Geometry;

public class Box2
{
    public readonly Vertex2 Min;
    public readonly Vertex2 Max;

    public Box2(Vertex2 min, Vertex2 max)
    {
        Min = min;
        Max = max;
    }

    public Box2(double minX, double minY, double maxX, double maxY)
    {
        Min = new Vertex2(minX, minY);
        Max = new Vertex2(maxX, maxY);
    }

    public Box2 Scale(double xscale, double yscale)
    {
        return new Box2(Min.X * xscale, Min.Y * yscale, Max.X * xscale, Max.Y * yscale);
    }

    public double Width => Max.X - Min.X;
    public double Height => Max.Y - Min.Y;

    public double Area => Width * Height;

    public Vertex2 Center => new((Min.X + Max.X) / 2, (Min.Y + Max.Y) / 2);
}