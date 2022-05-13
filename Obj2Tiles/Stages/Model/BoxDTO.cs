namespace Obj2Tiles.Stages.Model;

public class BoxDTO
{
    public VertexDTO Min { get; set; }
    public VertexDTO Max { get; set; }
    
    public double Width { get; set; }
    public double Height { get; set; }
    public double Depth { get; set; }
    
    public VertexDTO Center { get; set; }
}

public class VertexDTO
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}