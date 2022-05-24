using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SilentWave.Obj2Gltf.Gltf
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
        public Dictionary<string, int> Attributes { get; set; }
        /// <summary>
        /// The index of the accessor that contains the indices.
        /// </summary>
        [JsonProperty("indices")]
        public int Indices { get; set; }
        /// <summary>
        /// The index of the material to apply to this primitive when rendering
        /// </summary>
        [JsonProperty("material")]
        public int Material { get; set; }
        /// <summary>
        /// The type of primitives to render.
        /// </summary>
        [JsonProperty("mode")]
        public MeshMode Mode { get; set; }
    }
}
