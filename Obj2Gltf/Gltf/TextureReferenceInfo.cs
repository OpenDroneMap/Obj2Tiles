using System;
using Newtonsoft.Json;

namespace SilentWave.Gltf
{
    /// <summary>
    /// Reference to a `Texture`.
    /// </summary>
    public class TextureReferenceInfo
    {
        /// <summary>
        /// The index of the texture.
        /// </summary>
        [JsonProperty("index")]
        public Int32 Index { get; set; }
    }
}
