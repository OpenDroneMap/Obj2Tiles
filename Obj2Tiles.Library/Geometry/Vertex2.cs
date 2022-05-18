using System.Globalization;

namespace Obj2Tiles.Library.Geometry;

public class Vertex2
{
    protected bool Equals(Vertex2 other)
    {
        return X.Equals(other.X) && Y.Equals(other.Y);
    }

    public override bool Equals(object? obj)
    {
        return !ReferenceEquals(null, obj) &&
               (ReferenceEquals(this, obj) || obj.GetType() == GetType() && Equals((Vertex2)obj));
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y);
    }

    public Vertex2(double x, double y)
    {
        X = x;
        Y = y;
    }

    public readonly double X;
    public readonly double Y;

    public override string ToString()
    {
        return $"({X}; {Y})";
    }

    public static bool operator ==(Vertex2 a, Vertex2 b)
    {
        return Math.Abs(a.X - b.X) < double.Epsilon && Math.Abs(a.Y - b.Y) < double.Epsilon;
    }

    public static bool operator !=(Vertex2 a, Vertex2 b)
    {
        return Math.Abs(a.X - b.X) > double.Epsilon || Math.Abs(a.Y - b.Y) > double.Epsilon;
    }

    public double Distance(Vertex2 other)
    {
        return Math.Sqrt((X - other.X) * (X - other.X) + (Y - other.Y) * (Y - other.Y));
    }
    
    public Vertex2 CutEdgePerc(Vertex2 b, double perc)
    {
        return new Vertex2((b.X - X) * perc + X, (b.Y - Y) * perc + Y);
    }
}