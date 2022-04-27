namespace Obj2Tiles.Library.Geometry;

public class Box2D
{
    public readonly Vertex2 Min;
    public readonly Vertex2 Max;
    
    public Box2D(Vertex2 min, Vertex2 max)
    {
        Min = min;
        Max = max;
    }
    
    public Box2D(double minX, double minY, double maxX, double maxY)
    {
        Min = new Vertex2(minX, minY);
        Max = new Vertex2(maxX, maxY);
    }
    
    public double Width => Max.x - Min.x;
    public double Height => Max.y - Min.y;
    
    public double Area => Width * Height;
    
    public Vertex2 Center => new((Min.x + Max.x) / 2, (Min.y + Max.y) / 2);
    
}