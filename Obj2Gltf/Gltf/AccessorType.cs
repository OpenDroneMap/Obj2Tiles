namespace SilentWave.Gltf
{
    /// <summary>
    /// Specifies whether an attribute, vector, or matrix.
    /// </summary>
    public enum AccessorType
    {
        /// Scalar quantity.
        SCALAR = 1,

        /// 2D vector.
        VEC2,

        /// 3D vector.
        VEC3,

        /// 4D vector.
        VEC4,

        /// 2x2 matrix.
        MAT2,

        /// 3x3 matrix.
        MAT3,

        /// 4x4 matrix.
        MAT4
    }
}
