using System.Diagnostics;
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

        public override string ToString()
        {
            return $"{Min:0.00} - {Max:0.00} ({Width:0.00}x{Height:0.00}x{Depth:0.00}) c: {Center:0.00}";
        }
        
        // Override equals operator
        public override bool Equals(object obj)
        {
            if (obj is Box3 box)
            {
                return Min == box.Min && Max == box.Max;
            }
            return false;
        }
        
        public override int GetHashCode()
        {
            return Min.GetHashCode() ^ Max.GetHashCode();
        }
        
        public static bool operator ==(Box3 left, Box3 right)
        {
            return left.Equals(right);
        }
        
        public static bool operator !=(Box3 left, Box3 right)
        {
            return !(left == right);
        }
        
        // Split box into two along the given axis
        public Box3[] Split(Axis axis, double position)
        {
            return axis switch
            {
                Axis.X =>
                [
                    new Box3(Min, new Vertex3(position, Max.Y, Max.Z)),
                    new Box3(new Vertex3(position, Min.Y, Min.Z), Max)
                ],
                Axis.Y =>
                [
                    new Box3(Min, new Vertex3(Max.X, position, Max.Z)),
                    new Box3(new Vertex3(Min.X, position, Min.Z), Max)
                ],
                Axis.Z =>
                [
                    new Box3(Min, new Vertex3(Max.X, Max.Y, position)),
                    new Box3(new Vertex3(Min.X, Min.Y, position), Max)
                ],
                _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null)
            };
        }

        public Box3[] Split(Axis axis)
        {
            return axis switch
            {
                Axis.X =>
                [
                    new Box3(Min, new Vertex3(Min.X + Width / 2, Max.Y, Max.Z)),
                    new Box3(new Vertex3(Min.X + Width / 2, Min.Y, Min.Z), Max)
                ],
                Axis.Y =>
                [
                    new Box3(Min, new Vertex3(Max.X, Min.Y + Height / 2, Max.Z)),
                    new Box3(new Vertex3(Min.X, Min.Y + Height / 2, Min.Z), Max)
                ],
                Axis.Z =>
                [
                    new Box3(Min, new Vertex3(Max.X, Max.Y, Min.Z + Depth / 2)),
                    new Box3(new Vertex3(Min.X, Min.Y, Min.Z + Depth / 2), Max)
                ],
                _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null)
            };
        }

    }

}