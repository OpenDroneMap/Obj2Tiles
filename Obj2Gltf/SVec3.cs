using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace SilentWave.Obj2Gltf
{
    /// <summary>
    /// 3-d point or verctor
    /// </summary>
    public struct SVec3
    {
        public static SVec3 Sum(SVec3 v1, SVec3 v2)
            => new SVec3(v1.X + v2.X, v1.Y + v2.Y, v1.Z + v2.Z);

        public static SVec3 Subtract(SVec3 left, SVec3 right)
            => new SVec3(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

        public static SVec3 Multiply(SVec3 left, SVec3 right)
            => new SVec3(left.X * right.X, left.Y * right.Y, left.Z * right.Z);

        public static SVec3 Cross(SVec3 left, SVec3 right)
        {
            var leftX = left.X;
            var leftY = left.Y;
            var leftZ = left.Z;
            var rightX = right.X;
            var rightY = right.Y;
            var rightZ = right.Z;

            var x = leftY * rightZ - leftZ * rightY;
            var y = leftZ * rightX - leftX * rightZ;
            var z = leftX * rightY - leftY * rightX;

            return new SVec3(x, y, z);
        }

        public static Single Dot(SVec3 v1, SVec3 v2)
            => v1.X * v2.X + v1.Y * v2.Y + v1.Z * v2.Z;

        public static SVec3 operator -(SVec3 left, SVec3 right) => Subtract(left, right);

        public static SVec3 operator +(SVec3 left, SVec3 right) => Sum(left, right);

        public SVec3(Single xyz) : this(xyz, xyz, xyz) { }

        public SVec3(Single x, Single y) : this(x, y, 0.0f) { }

        public SVec3(Single x, Single y, Single z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public Single X;

        public Single Y;

        public Single Z;

        public override String ToString()
            => $"{X:F}, {Y:F}, {Z:F}";

        public Single GetLength()
            => (Single)Math.Sqrt(X * X + Y * Y + Z * Z);

        public SVec3 Normalize()
        {
            var len = GetLength();
            return new SVec3(X / len, Y / len, Z / len);
        }

        public SVec3 Substract(SVec3 p) => Subtract(this, p);

        public SVec3 MultiplyBy(Single val) => new SVec3(X * val, Y * val, Z * val);

        public SVec3 DividedBy(Single val) => new SVec3(X / val, Y / val, Z / val);

        public void WriteBytes(System.IO.BinaryWriter sw)
        {
            sw.Write(X);
            sw.Write(Y);
            sw.Write(Z);
        }
    }
}
