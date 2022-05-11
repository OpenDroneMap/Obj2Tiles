using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace SilentWave.Gltf
{
    /// <summary>
    /// A set of primitives to be rendered.
    ///
    /// A node can contain one or more meshes and its transform places the meshes in
    /// the scene.
    /// </summary>
    public class Mesh
    {
        /// <summary>
        /// Optional user-defined name for this object.
        /// </summary>
        [JsonProperty("name")]
        public String Name { get; set; }
        /// <summary>
        /// Defines the geometry to be renderered with a material.
        /// </summary>
        [JsonProperty("primitives")]
        public List<Primitive> Primitives { get; set; } = new List<Primitive>();
    }
}
