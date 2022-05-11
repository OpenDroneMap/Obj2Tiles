namespace SilentWave.Gltf
{
    /// <summary>
    /// The type of primitives to render.
    /// </summary>
    public enum MeshMode
    {
        /// Corresponds to `GL_POINTS`.
        Points = 0,

        /// Corresponds to `GL_LINES`.
        Lines,

        /// Corresponds to `GL_LINE_LOOP`.
        LineLoop,

        /// Corresponds to `GL_LINE_STRIP`.
        LineStrip,

        /// Corresponds to `GL_TRIANGLES`.
        Triangles,

        /// Corresponds to `GL_TRIANGLE_STRIP`.
        TriangleStrip,

        /// Corresponds to `GL_TRIANGLE_FAN`.
        TriangleFan,
    }
}
