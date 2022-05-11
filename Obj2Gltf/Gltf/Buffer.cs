using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace SilentWave.Gltf
{
    /// <summary>
    /// A buffer points to binary data representing geometry, animations, or skins.
    /// </summary>
    public class Buffer
    {
        /// <summary>
        /// Optional user-defined name for this object.
        /// </summary>
        [JsonProperty("name")]
        public String Name { get; set; }
        /// <summary>
        /// The length of the buffer in bytes.
        /// </summary>
        [JsonProperty("byteLength")]
        public Int64 ByteLength { get; set; }
        /// <summary>
        /// The uri of the buffer.  Relative paths are relative to the .gltf file.
        /// Instead of referencing an external file, the uri can also be a data-uri.
        /// </summary>
        [JsonProperty("uri")]
        public String Uri { get; set; }
    }
}
