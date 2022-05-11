using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SilentWave.Gltf
{
    /// <summary>
    /// Geometry to be rendered with the given material.
    /// </summary>
    public class Primitive
    {
        /// <summary>
        /// Maps attribute semantic names to the `Accessor`s containing the
        /// corresponding attribute data.
        /// </summary>
        [JsonProperty("attributes")]
        public Dictionary<String, Int32> Attributes { get; set; }
        /// <summary>
        /// The index of the accessor that contains the indices.
        /// </summary>
        [JsonProperty("indices")]
        public Int32 Indices { get; set; }
        /// <summary>
        /// The index of the material to apply to this primitive when rendering
        /// </summary>
        [JsonProperty("material")]
        public Int32 Material { get; set; }
        /// <summary>
        /// The type of primitives to render.
        /// </summary>
        [JsonProperty("mode")]
        public MeshMode Mode { get; set; }
    }
}
