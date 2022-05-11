namespace SilentWave.Gltf
{
    /// <summary>
    /// Minification filter.
    /// </summary>
    public enum MinificationFilterKind
    {
        /// Corresponds to `GL_NEAREST`.
        Nearest = 9728,

        /// Corresponds to `GL_LINEAR`.
        Linear,

        /// Corresponds to `GL_NEAREST_MIPMAP_NEAREST`.
        NearestMipmapNearest = 9984,

        /// Corresponds to `GL_LINEAR_MIPMAP_NEAREST`.
        LinearMipmapNearest,

        /// Corresponds to `GL_NEAREST_MIPMAP_LINEAR`.
        NearestMipmapLinear,

        /// Corresponds to `GL_LINEAR_MIPMAP_LINEAR`.
        LinearMipmapLinear,
    }
}
