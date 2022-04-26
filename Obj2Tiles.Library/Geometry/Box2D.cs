namespace Obj2Tiles.Library.Geometry;

public class Box2D
{
    public readonly Vertex2D Min;
    public readonly Vertex2D Max;
    
    public Box2D(Vertex2D min, Vertex2D max)
    {
        Min = min;
        Max = max;
    }
    
    public Box2D(double minX, double minY, double maxX, double maxY)
    {
        Min = new Vertex2D(minX, minY);
        Max = new Vertex2D(maxX, maxY);
    }
    
    public double Width => Max.x - Min.x;
    public double Height => Max.y - Min.y;
    
    public double Area => Width * Height;
    
    public Vertex2D Center => new((Min.x + Max.x) / 2, (Min.y + Max.y) / 2);
    
}