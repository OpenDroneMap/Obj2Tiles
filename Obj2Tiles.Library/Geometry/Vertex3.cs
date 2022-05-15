using System.Globalization;
using System.Text.Json.Serialization;

namespace Obj2Tiles.Library.Geometry;

public class Vertex3
{

    [JsonInclude]
    public readonly double X;
    [JsonInclude]
    public readonly double Y;
    [JsonInclude]
    public readonly double Z;

    public Vertex3(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z; 
    }

    public override string ToString()
    {
        return  $"[({X}; {Y}; {Z})";
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
    
    public static Vertex3 operator +(Vertex3 a, Vertex3 b)
    {
        return new Vertex3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    }
    
    public static Vertex3 operator -(Vertex3 a, Vertex3 b)
    {
        return new Vertex3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    }
    
    public static Vertex3 operator *(Vertex3 a, double b)
    {
        return new Vertex3(a.X * b, a.Y * b, a.Z * b);
    }
    
    public static Vertex3 operator /(Vertex3 a, double b)
    {
        return new Vertex3(a.X / b, a.Y / b, a.Z / b);
    }
    
    public static Vertex3 operator *(double a, Vertex3 b)
    {
        return new Vertex3(a * b.X, a * b.Y, a * b.Z);
    }
    
    public Vertex3 Cross(Vertex3 other)
    {
        return new Vertex3(Y * other.Z - Z * other.Y, Z * other.X - X * other.Z, X * other.Y - Y * other.X);
    }
    
    public Vertex3 CutEdgePerc(Vertex3 b, double perc)
    {
        return new Vertex3((b.X - X) * perc + X, (b.Y - Y) * perc + Y, (b.Z - Z) * perc + Z);
    }
}