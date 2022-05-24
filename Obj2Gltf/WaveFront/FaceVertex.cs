using System;

namespace SilentWave.Obj2Gltf.WaveFront
{
    /// <summary>
    /// represents a vertex on a face
    /// </summary>
    public struct FaceVertex : IEquatable<FaceVertex>
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="v">vertex coordinates index</param>
        public FaceVertex(int v) : this(v, 0, 0) { }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="v">vertex coordinates index</param>
        /// <param name="n">vertex normal index</param>
        public FaceVertex(int v, int n) : this(v, 0, n) { }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="v">vertex coordinates index</param>
        /// <param name="t">vertex texture coordinates index</param>
        /// <param name="n">vertex normal index</param>
        public FaceVertex(int v, int t, int n)
        {
            V = v;
            N = n;
            T = t;
        }
        /// <summary>
        /// vertex coordinates index
        /// </summary>
        public int V;
        /// <summary>
        /// vertex texture coordinates index
        /// </summary>
        public int T;
        /// <summary>
        /// vertex normal index
        /// </summary>
        public int N;

        public override string ToString()
        {
            if (N > 0)
            {
                if (T > 0)
                {
                    return $"{V}/{T}/{N}";
                }
                else
                {
                    return $"{V}//{N}";
                }
            }
            if (T > 0)
            {
                return $"{V}/{T}";
            }
            return $"{V}";
        }

        public bool Equals(FaceVertex other)
        {
            return V == other.V && T == other.T && N == other.N;
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (obj.GetType() != typeof(FaceVertex)) return false;
            return Equals((FaceVertex)obj);
        }

        public override int GetHashCode()
        {
            return V ^ T ^ N;
        }
    }
}
