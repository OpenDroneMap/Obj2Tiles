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

public class VertexUtilsX : IVertexUtils
{
    public Vertex3 CutEdge(Vertex3 a, Vertex3 b, double q)
    {
        var dx = a.x - b.x;
        var my = (a.y - b.y) / dx;
        var mz = (a.z - b.z) / dx;
        
        Debug.Assert(double.IsFinite(my));
        Debug.Assert(double.IsFinite(mz));
        
        return new Vertex3(q, my * (q - a.x) + a.y, mz * (q - a.x) + a.z);
    }
    
    public double GetDimension(Vertex3 v)
    {
        return v.x;
    }

    public Axis Axis => Axis.X;
}

public class VertexUtilsY : IVertexUtils
{

    public Vertex3 CutEdge(Vertex3 a, Vertex3 b, double q)
    {
        var dy = a.y - b.y;
        var mx = (a.x - b.x) / dy;
        var mz = (a.z - b.z) / dy;
        
        Debug.Assert(double.IsFinite(mx));
        Debug.Assert(double.IsFinite(mz));

        return new Vertex3(mx * (q - a.y) + a.x, q, mz * (q - a.y) + a.z);
    }

    public double GetDimension(Vertex3 v)
    {
        return v.y;
    }

    public Axis Axis => Axis.Y;

}

public class VertexUtilsZ : IVertexUtils
{
    public Vertex3 CutEdge(Vertex3 a, Vertex3 b, double q)
    {
        var dz = a.z - b.z;
        var mx = (a.x - b.x) / dz;
        var my = (a.y - b.y) / dz;

        Debug.Assert(double.IsFinite(mx));
        Debug.Assert(double.IsFinite(my));

        return new Vertex3(mx * (q - a.z) + a.x, my * (q - a.z) + a.y, q);
    }

    public double GetDimension(Vertex3 v)
    {
        return v.z;
    }

    public Axis Axis => Axis.Z;

}