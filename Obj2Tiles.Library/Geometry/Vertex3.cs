using System.Globalization;

namespace Obj2Tiles.Library.Geometry;

public class Vertex3
{
    
    private static int Index = 0;
    private readonly int index;
    
    public readonly double X;
    public readonly double Y;
    public readonly double Z;

    public Vertex3(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z; 
        index = Index++;
    }

    public override string ToString()
    {
        return  $"[{index}] ({X}; {Y}; {Z})";
    }

    private static readonly CultureInfo culture = new("en-US");
    
    public string ToObj()
    {
        return string.Format(culture, "v {0} {1} {2}", X, Y, Z);
    }

    protected bool Equals(Vertex3 other)
    {
        return X.Equals(other.X) && Y.Equals(other.Y) && Y.Equals(other.Z);
    }
    
    public override bool Equals(object? obj)
    {
        return !ReferenceEquals(null, obj) &&
               (ReferenceEquals(this, obj) || obj.GetType() == GetType() && Equals((Vertex3)obj));
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y, Z);
    }
    
    public static bool operator ==(Vertex3 a, Vertex3 b)
    {
        return Math.Abs(a.X - b.X) < double.Epsilon && Math.Abs(a.Y - b.Y) < double.Epsilon && Math.Abs(a.Z - b.Z) < double.Epsilon;
    }
    
    public static bool operator !=(Vertex3 a, Vertex3 b)
    {
        return Math.Abs(a.X - b.X) > double.Epsilon || Math.Abs(a.Y - b.Y) > double.Epsilon && Math.Abs(a.Z - b.Z) > double.Epsilon;
    }

    public double Distance(Vertex3 other)
    {
        return Math.Sqrt((X - other.X) * (X - other.X) + (Y - other.Y) * (Y - other.Y) + (Z - other.Z) * (Z - other.Z));
    }
    
    public Vertex3 CutEdgePerc(Vertex3 b, double perc)
    {
        return new Vertex3((b.X - X) * perc + X, (b.Y - Y) * perc + Y, (b.Z - Z) * perc + Z);
    }
}