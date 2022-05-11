using System;
using System.Linq;

namespace SilentWave.Obj2Gltf
{
    /// <summary>
    /// 2-d point or vector
    /// </summary>
    public struct SVec2
    {
        public SVec2(Single u, Single v)
        {
            U = u;
            V = v;
        }

        public Single U;

        public Single V;

        public override String ToString() => $"{U}, {V}";

        public void WriteBytes(System.IO.BinaryWriter sw)
        {
            sw.Write(U);
            sw.Write(V);
        }

        public Single[] ToArray() => new[] { U, V };

        public Single GetDistance(SVec2 p) => (Single)Math.Sqrt((U - p.U) * (U - p.U) + (V - p.V) * (V - p.V));

        public Single GetLength() => (Single)Math.Sqrt(U * U + V * V);

        public SVec2 Normalize()
        {
            var len = GetLength();
            return new SVec2(U / len, V / len);
        }

        public Single Dot(SVec2 v)
        {
            return U * v.U + V * v.V;
        }
    }
}
