using System;
using System.IO;
using System.Linq;

namespace SilentWave.Obj2Gltf
{
    /// <summary>
    /// 2-d point or vector
    /// </summary>
    public struct SVec2
    {
        public SVec2(float u, float v)
        {
            U = u;
            V = v;
        }

        public readonly float U;

        public readonly float V;

        public override string ToString() => $"{U}, {V}";

        public void WriteBytes(BinaryWriter sw)
        {
            sw.Write(U);
            sw.Write(V);
        }

        public float[] ToArray() => new[] { U, V };

        public float GetDistance(SVec2 p) => (float)Math.Sqrt((U - p.U) * (U - p.U) + (V - p.V) * (V - p.V));

        public float GetLength() => (float)Math.Sqrt(U * U + V * V);

        public SVec2 Normalize()
        {
            var len = GetLength();
            return new SVec2(U / len, V / len);
        }

        public float Dot(SVec2 v)
        {
            return U * v.U + V * v.V;
        }
    }
}
