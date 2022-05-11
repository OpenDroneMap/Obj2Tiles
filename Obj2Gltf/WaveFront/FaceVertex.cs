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
        public FaceVertex(Int32 v) : this(v, 0, 0) { }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="v">vertex coordinates index</param>
        /// <param name="n">vertex normal index</param>
        public FaceVertex(Int32 v, Int32 n) : this(v, 0, n) { }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="v">vertex coordinates index</param>
        /// <param name="t">vertex texture coordinates index</param>
        /// <param name="n">vertex normal index</param>
        public FaceVertex(Int32 v, Int32 t, Int32 n)
        {
            V = v;
            N = n;
            T = t;
        }
        /// <summary>
        /// vertex coordinates index
        /// </summary>
        public Int32 V;
        /// <summary>
        /// vertex texture coordinates index
        /// </summary>
        public Int32 T;
        /// <summary>
        /// vertex normal index
        /// </summary>
        public Int32 N;

        public override String ToString()
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

        public Boolean Equals(FaceVertex other)
        {
            return V == other.V && T == other.T && N == other.N;
        }

        public override Boolean Equals(Object obj)
        {
            if (obj == null) return false;
            if (obj.GetType() != typeof(FaceVertex)) return false;
            return Equals((FaceVertex)obj);
        }

        public override Int32 GetHashCode()
        {
            return V ^ T ^ N;
        }
    }
}
