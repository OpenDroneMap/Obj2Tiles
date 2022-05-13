using Obj2Tiles.Library.Geometry;

namespace Obj2Tiles.Stages.Model;

public class BoxDTO
{
    public VertexDTO Min { get; set; }
    public VertexDTO Max { get; set; }

    public double Width { get; set; }
    public double Height { get; set; }
    public double Depth { get; set; }

    public VertexDTO Center { get; set; }

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