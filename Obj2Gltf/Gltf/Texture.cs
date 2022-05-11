using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace SilentWave.Gltf
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
        public String Name { get; set; }
        /// <summary>
        /// The index of the sampler used by this texture.
        /// </summary>
        [JsonProperty("sampler")]
        public Int32 Sampler { get; set; }
        /// <summary>
        /// The index of the image used by this texture.
        /// </summary>
        [JsonProperty("source")]
        public Int32 Source { get; set; }
    }
}
