using System;
using Newtonsoft.Json;

namespace SilentWave.Gltf
{
    /// <summary>
    /// Texture sampler properties for filtering and wrapping modes.
    /// </summary>
    public class TextureSampler
    {
        /// <summary>
        /// Optional user-defined name for this object.
        /// </summary>
        [JsonProperty("name")]
        public String Name { get; set; }
        /// <summary>
        /// Magnification filter.
        /// </summary>
        [JsonProperty("magFilter")]
        public MagnificationFilterKind MagFilter { get; set; }
        /// <summary>
        /// Minification filter.
        /// </summary>
        [JsonProperty("minFilter")]
        public MinificationFilterKind MinFilter { get; set; }
        /// <summary>
        /// `s` wrapping mode.
        /// </summary>
        [JsonProperty("wrapS")]
        public TextureWrappingMode WrapS { get; set; }
        /// <summary>
        /// `t` wrapping mode.
        /// </summary>
        [JsonProperty("wrapT")]
        public TextureWrappingMode WrapT { get; set; }
    }
}
