using System;
using Newtonsoft.Json;

namespace SilentWave.Obj2Gltf.Gltf
{
    /// <summary>
    /// Image data used to create a texture.
    /// </summary>
    public class Image
    {
        /// <summary>
        /// Optional user-defined name for this object.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }
        /// <summary>
        /// The image's MIME type.
        /// </summary>
        [JsonProperty("mimeType")]
        public string MimeType { get; set; }
        /// <summary>
        /// The index of the buffer view that contains the image. Use this instead of 
        ///  the image's uri property.
        /// </summary>
        [JsonProperty("bufferView")]
        public int? BufferView { get; set; }
        /// <summary>
        /// The uri of the image.  Relative paths are relative to the .gltf file.
        /// Instead of referencing an external file, the uri can also be a data-uri.
        /// The image format must be jpg or png.
        /// Optional
        /// </summary>
        [JsonProperty("uri")]
        public string Uri { get; set; }
    }
}
