using System;
using Newtonsoft.Json;

namespace SilentWave.Obj2Gltf.Gltf
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
        public int Index { get; set; }
    }
}
