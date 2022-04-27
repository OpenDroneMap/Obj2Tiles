using System.Globalization;

namespace Obj2Tiles.Library.Geometry;

public class Vertex2
{
    protected bool Equals(Vertex2 other)
    {
        return x.Equals(other.x) && y.Equals(other.y);
    }

    public override bool Equals(object? obj)
    {
        return !ReferenceEquals(null, obj) &&
               (ReferenceEquals(this, obj) || obj.GetType() == GetType() && Equals((Vertex2)obj));
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(x, y);
    }

    public Vertex2(double x, double y)
    {
        this.x = x;
        this.y = y;
    }

    public readonly double x;
    public readonly double y;

    public override string ToString()
    {
        return $"({x}; {y})";
    }

    private static readonly CultureInfo culture = new("en-US");

    public static bool operator ==(Vertex2 a, Vertex2 b)
    {
        return Math.Abs(a.x - b.x) < double.Epsilon && Math.Abs(a.y - b.y) < double.Epsilon;
    }

    public static bool operator !=(Vertex2 a, Vertex2 b)
    {
        return Math.Abs(a.x - b.x) > double.Epsilon || Math.Abs(a.y - b.y) > double.Epsilon;
    }

    public double Distance(Vertex2 other)
    {
        return Math.Sqrt((x - other.x) * (x - other.x) + (y - other.y) * (y - other.y));
    }
    
    public Vertex2 CutEdgePerc(Vertex2 b, double perc)
    {
        return new Vertex2((b.x - x) * perc + x, (b.y - y) * perc + y);
    }
}