using System;
using Newtonsoft.Json;

namespace SilentWave.Gltf
{
    /// <summary>
    /// A view into a buffer generally representing a subset of the buffer.
    /// https://github.com/KhronosGroup/glTF/tree/master/specification/2.0#reference-bufferview
    /// </summary>
    public class BufferView
    {
        /// <summary>
        /// Optional user-defined name for this object.
        /// </summary>
        [JsonProperty("name")]
        public String Name { get; set; }
        /// <summary>
        /// The parent `Buffer`.
        /// </summary>
        [JsonProperty("buffer")]
        public Int32? Buffer { get; set; }
        /// <summary>
        /// The length of the `BufferView` in bytes.
        /// </summary>
        [JsonProperty("byteLength")]
        public Int64 ByteLength { get; set; }
        /// <summary>
        /// Offset into the parent buffer in bytes.
        /// </summary>
        [JsonProperty("byteOffset")]
        public Int64 ByteOffset { get; set; }
        /// <summary>
        /// The stride in bytes between vertex attributes or other interleavable data.
        /// When zero, data is assumed to be tightly packed.
        /// </summary>
        [JsonProperty("byteStride")]
        public Int32? ByteStride { get; set; }
        /// <summary>
        /// Optional target the buffer should be bound to.
        /// </summary>
        [JsonProperty("target")]
        public BufferViewTarget? Target { get; set; }
    }
}
