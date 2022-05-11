using System;

namespace SilentWave.Obj2Gltf.WaveFront
{
    /// <summary>
    /// represents a triangle
    /// </summary>
    public struct FaceTriangle
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        /// <param name="v3"></param>
        public FaceTriangle(FaceVertex v1, FaceVertex v2, FaceVertex v3)
        {
            V1 = v1;
            V2 = v2;
            V3 = v3;
        }
        /// <summary>
        /// The first vertex
        /// </summary>
        public FaceVertex V1;
        /// <summary>
        /// The second vertex
        /// </summary>
        public FaceVertex V2;
        /// <summary>
        /// The third vertex
        /// </summary>
        public FaceVertex V3;

        public override String ToString()
        {
            return $"f {V1} {V2} {V3}";
        }
    }
}
