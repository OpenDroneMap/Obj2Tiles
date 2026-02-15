using Obj2Tiles.Library.Geometry;

namespace Obj2Tiles.Test;

public class BoxDTO
{
    public VertexDTO Min { get; set; } = null!;
    public VertexDTO Max { get; set; } = null!;

    public double Width { get; set; }
    public double Height { get; set; }
    public double Depth { get; set; }

    public VertexDTO Center { get; set; } = null!;

    public BoxDTO()
    {
    }

    public BoxDTO(Box3 box)
    {
        Center = new VertexDTO(box.Center);
        Min = new VertexDTO(box.Min);
        Max = new VertexDTO(box.Max);
        Width = box.Width;
        Height = box.Height;
        Depth = box.Depth;
    }
}

public class VertexDTO
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }

    public VertexDTO()
    {
    }

    public VertexDTO(Vertex3 vertex)
    {
        X = vertex.X;
        Y = vertex.Y;
        Z = vertex.Z;
    }
}