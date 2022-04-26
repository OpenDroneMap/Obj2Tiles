namespace Obj2Tiles.Library.Geometry;

public interface IVertexUtils
{
    Vertex2D CutEdge(Vertex2D a, Vertex2D b, double q);
    Vertex3D CutEdge(Vertex3D a, Vertex3D b, double q);
    double GetDimension(Vertex2D v);
    double GetDimension(Vertex3D v);

    Vertex2D CutEdgePerc(Vertex2D a, Vertex2D b, double perc);
    Vertex3D CutEdgePerc(Vertex3D a, Vertex3D b, double perc);
}

public class VertexUtilsX : IVertexUtils
{
    public Vertex2D CutEdge(Vertex2D a, Vertex2D b, double q)
    {
        var m = (a.y - b.y) / (a.x - b.x);
        return new Vertex2D(q, m * (q - a.x) + a.y);
    }

    public Vertex3D CutEdge(Vertex3D a, Vertex3D b, double q)
    {
        var dx = a.x - b.x;
        var my = (a.y - b.y) / dx;
        var mz = (a.z - b.z) / dx;
        
        return new Vertex3D(q, my * (q - a.x) + a.y, mz * (q - a.x) + a.z);
    }

    public double GetDimension(Vertex2D v)
    {
        return v.x;
    }

    public double GetDimension(Vertex3D v)
    {
        return v.x;
    }

    public Vertex2D CutEdgePerc(Vertex2D a, Vertex2D b, double perc)
    {
        return new Vertex2D((b.x - a.x) * perc + a.x, (b.y - a.y) * perc + a.y);
    }

    public Vertex3D CutEdgePerc(Vertex3D a, Vertex3D b, double perc)
    {
        return new Vertex3D((b.x - a.x) * perc + a.x, (b.y - a.y) * perc + a.y, (b.z - a.z) * perc + a.z);
    }
}

public class VertexUtilsY : IVertexUtils
{
    public Vertex2D CutEdge(Vertex2D a, Vertex2D b, double q)
    {
        var m = (a.x - b.x) / (a.y - b.y);
        return new Vertex2D(m * (q - a.y) + a.x, q);
    }

    public Vertex3D CutEdge(Vertex3D a, Vertex3D b, double q)
    {
        var dy = a.y - b.y;
        var mx = (a.x - b.x) / dy;
        var mz = (a.z - b.z) / dy;

        return new Vertex3D(mx * (q - a.y) + a.x, q, mz * (q - a.y) + a.z);
    }

    public double GetDimension(Vertex3D v)
    {
        return v.y;
    }

    public Vertex2D CutEdgePerc(Vertex2D a, Vertex2D b, double perc)
    {
        return new Vertex2D((b.x - a.x) * perc + a.x, (b.y - a.y) * perc + a.y);
    }

    public Vertex3D CutEdgePerc(Vertex3D a, Vertex3D b, double perc)
    {
        return new Vertex3D((b.x - a.x) * perc + a.x, (b.y - a.y) * perc + a.y, (b.z - a.z) * perc + a.z);
    }

    public double GetDimension(Vertex2D v)
    {
        return v.y;
    }
}

public class VertexUtilsZ : IVertexUtils
{
    public Vertex2D CutEdge(Vertex2D a, Vertex2D b, double q)
    {
        throw new InvalidOperationException();
    }

    public Vertex3D CutEdge(Vertex3D a, Vertex3D b, double q)
    {
        var dz = a.z - b.z;
        var mx = (a.x - b.x) / dz;
        var my = (a.y - b.y) / dz;

        return new Vertex3D(mx * (q - a.z) + a.x, my * (q - a.z) + a.y, q);
    }

    public double GetDimension(Vertex3D v)
    {
        return v.z;
    }

    public Vertex2D CutEdgePerc(Vertex2D a, Vertex2D b, double perc)
    {
        return new Vertex2D((b.x - a.x) * perc + a.x, (b.y - a.y) * perc + a.y);
    }

    public Vertex3D CutEdgePerc(Vertex3D a, Vertex3D b, double perc)
    {
        return new Vertex3D((b.x - a.x) * perc + a.x, (b.y - a.y) * perc + a.y, (b.z - a.z) * perc + a.z);
    }

    public double GetDimension(Vertex2D v)
    {
        return v.y;
    }

}