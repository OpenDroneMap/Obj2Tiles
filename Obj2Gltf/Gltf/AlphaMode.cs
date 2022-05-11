namespace SilentWave.Gltf
{
    /// <summary>
    /// The alpha rendering mode of a material.
    /// </summary>
    public enum AlphaMode
    {
        /// The alpha value is ignored and the rendered output is fully opaque.
        OPAQUE = 1,

        /// The rendered output is either fully opaque or fully transparent depending on
        /// the alpha value and the specified alpha cutoff value.
        MASK,

        /// The rendered output is either fully opaque or fully transparent depending on
        /// the alpha value and the specified alpha cutoff value.
        BLEND,
    }
}
