using System.Text.Json.Serialization;

namespace Obj2Tiles.Library.Geometry
{
    public class Box3
    {
        [JsonInclude]
        public readonly Vertex3 Min;
        [JsonInclude]
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

        public double Width => Max.X - Min.X;
        public double Height => Max.Y - Min.Y;
        public double Depth => Max.Z - Min.Z;

        public Vertex3 Center => new((Min.X + Max.X) / 2, (Min.Y + Max.Y) / 2, (Min.Z + Max.Z) / 2);

    }
}