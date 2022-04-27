namespace Obj2Tiles.Library.Geometry
{
    public class Box3
    {
        public readonly Vertex3 Min;
        public readonly Vertex3 Max;
    
        public Box3(Vertex3 min, Vertex3 max)
        {
            Min = min;
            Max = max;
        }
    
        public Box3(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
        {
            Min = new Vertex3(minX, minY, minZ);
            Max = new Vertex3(maxX, maxY, maxZ);
        }
    
        public double Width => Max.x - Min.x;
        public double Height => Max.y - Min.y;
        public double Depth => Max.z - Min.z;
    
        public Vertex3 Center => new((Min.x + Max.x) / 2, (Min.y + Max.y) / 2, (Min.z + Max.z) / 2);
    
    }
}