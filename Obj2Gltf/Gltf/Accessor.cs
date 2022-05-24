using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace SilentWave.Obj2Gltf.Gltf
{
    /// <summary>
    /// A typed view into a buffer view.
    /// </summary>
    public class Accessor
    {
        /// <summary>
        /// Optional user-defined name for this object.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }
        /// <summary>
        /// The data type of components in the attribute.
        /// </summary>
        [JsonProperty("componentType")]
        public ComponentType ComponentType { get; set; }
        /// <summary>
        /// The number of components within the buffer view - not to be confused
        /// with the number of bytes in the buffer view.
        /// </summary>
        [JsonProperty("count")]
        public int Count { get; set; }
        /// <summary>
        /// Minimum value of each component in this attribute.
        /// </summary>
        [JsonProperty("min")]
        [JsonConverter(typeof(SingleArrayJsonConverter))]
        public float[] Min { get; set; }
        /// <summary>
        /// Maximum value of each component in this attribute.
        /// </summary>
        [JsonProperty("max")]
        [JsonConverter(typeof(SingleArrayJsonConverter))]
        public float[] Max { get; set; }
        /// <summary>
        /// Specifies if the attribute is a scalar, vector, or matrix.
        /// </summary>
        [JsonProperty("type")]
        [JsonConverter(typeof(StringEnumConverter))]
        public AccessorType Type { get;set; }
        /// <summary>
        /// The parent buffer view this accessor reads from.
        /// </summary>
        [JsonProperty("bufferView")]
        public int BufferView { get; set; }
        /// <summary>
        /// The offset relative to the start of the parent `BufferView` in bytes.
        /// </summary>
        [JsonProperty("byteOffset")]
        public long ByteOffset { get; set; }
    }
}
