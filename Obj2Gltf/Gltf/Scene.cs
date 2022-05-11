using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace SilentWave.Gltf
{
    /// <summary>
    /// The root `Node`s of a scene.
    /// </summary>
    public class Scene
    {
        /// <summary>
        /// The indices of each root node.
        /// </summary>
        [JsonProperty("nodes")]
        public List<Int32> Nodes { get; set; } = new List<Int32>();
    }
}
