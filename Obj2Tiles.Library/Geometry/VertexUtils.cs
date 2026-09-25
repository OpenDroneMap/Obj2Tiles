using System.Diagnostics;

namespace Obj2Tiles.Library.Geometry;

public enum Axis
{
    X,
    Y,
    Z
}

public interface IVertexUtils
{
    Vertex3 CutEdge(Vertex3 a, Vertex3 b, double q);
    double GetDimension(Vertex3 v);

    Axis Axis { get; }
}

internal static class VertexUtilsCommon
{
    // Canonicalizes edge endpoint order so CutEdge(a, b, q) == CutEdge(b, a, q) bit-for-bit.
    // An edge shared by two adjacent triangles gets intersected independently by each one, and
    // depending on which vertex a triangle treats as "the lone one on its side", the pair can be
    // passed in either order. Floating-point rounding then makes the two triangles compute
    // slightly different positions for what should be the same shared boundary vertex - a gap
    // that grows with edge length and that exact-equality vertex deduplication won't close.
    public static void Canonicalize(ref Vertex3 a, ref Vertex3 b)
    {
        var swap = a.X != b.X ? a.X > b.X
            : a.Y != b.Y ? a.Y > b.Y
            : a.Z > b.Z;

        if (swap)
            (a, b) = (b, a);
    }
}

public class VertexUtilsX : IVertexUtils
{
    public Vertex3 CutEdge(Vertex3 a, Vertex3 b, double q)
    {
        VertexUtilsCommon.Canonicalize(ref a, ref b);

        var dx = a.X - b.X;
        var my = (a.Y - b.Y) / dx;
        var mz = (a.Z - b.Z) / dx;
        
        Debug.Assert(double.IsFinite(my));
        Debug.Assert(double.IsFinite(mz));
        
        return new Vertex3(q, my * (q - a.X) + a.Y, mz * (q - a.X) + a.Z);
    }
    
    public double GetDimension(Vertex3 v)
    {
        return v.X;
    }

    public Axis Axis => Axis.X;
}

public class VertexUtilsY : IVertexUtils
{

    public Vertex3 CutEdge(Vertex3 a, Vertex3 b, double q)
    {
        VertexUtilsCommon.Canonicalize(ref a, ref b);

        var dy = a.Y - b.Y;
        var mx = (a.X - b.X) / dy;
        var mz = (a.Z - b.Z) / dy;
        
        Debug.Assert(double.IsFinite(mx));
        Debug.Assert(double.IsFinite(mz));

        return new Vertex3(mx * (q - a.Y) + a.X, q, mz * (q - a.Y) + a.Z);
    }

    public double GetDimension(Vertex3 v)
    {
        return v.Y;
    }

    public Axis Axis => Axis.Y;

}

public class VertexUtilsZ : IVertexUtils
{
    public Vertex3 CutEdge(Vertex3 a, Vertex3 b, double q)
    {
        VertexUtilsCommon.Canonicalize(ref a, ref b);

        var dz = a.Z - b.Z;
        var mx = (a.X - b.X) / dz;
        var my = (a.Y - b.Y) / dz;

        Debug.Assert(double.IsFinite(mx));
        Debug.Assert(double.IsFinite(my));

        return new Vertex3(mx * (q - a.Z) + a.X, my * (q - a.Z) + a.Y, q);
    }

    public double GetDimension(Vertex3 v)
    {
        return v.Z;
    }

    public Axis Axis => Axis.Z;

}