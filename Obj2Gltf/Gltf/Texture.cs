using System;
using Newtonsoft.Json;

namespace SilentWave.Obj2Gltf.Gltf
{
    /// <summary>
    /// A texture and its sampler.
    /// </summary>
    public class Texture
    {
        /// <summary>
        /// Optional user-defined name for this object.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }
        /// <summary>
        /// The index of the sampler used by this texture.
        /// </summary>
        [JsonProperty("sampler")]
        public int Sampler { get; set; }
        /// <summary>
        /// The index of the image used by this texture. May be omitted when the image is provided
        /// exclusively through an extension (e.g. EXT_texture_webp with no fallback).
        /// </summary>
        [JsonProperty("source")]
        public int? Source { get; set; }
        /// <summary>
        /// Optional texture-level extensions (e.g. EXT_texture_webp).
        /// </summary>
        [JsonProperty("extensions")]
        public TextureExtensions Extensions { get; set; }
    }

    /// <summary>
    /// Texture-level glTF extensions container.
    /// </summary>
    public class TextureExtensions
    {
        /// <summary>
        /// The EXT_texture_webp extension, when the texture image is a WebP.
        /// </summary>
        [JsonProperty("EXT_texture_webp")]
        public ExtTextureWebp EXT_texture_webp { get; set; }
    }

    /// <summary>
    /// EXT_texture_webp extension payload: the index of the WebP image source.
    /// </summary>
    public class ExtTextureWebp
    {
        /// <summary>The index of the WebP image used by this texture.</summary>
        [JsonProperty("source")]
        public int Source { get; set; }
    }
}
