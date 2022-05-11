using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace SilentWave.Gltf
{
    /// <summary>
    /// gltf json model
    /// </summary>
    public class GltfModel
    {
        [JsonProperty("accessors")]
        public List<Accessor> Accessors { get; } = new List<Accessor>();
        [JsonProperty("asset")]
        public Asset Asset { get; set; } = new Asset { Generator = "SilentWave", Version = "2.0" };
        [JsonProperty("buffers")]
        public List<Buffer> Buffers { get; } = new List<Buffer>();
        [JsonProperty("bufferViews")]
        public List<BufferView> BufferViews { get; } = new List<BufferView>();
        [JsonProperty("images")]
        public List<Image> Images { get; } = new List<Image>();
        [JsonProperty("materials")]
        public List<Material> Materials { get; } = new List<Material>();

        [JsonProperty("meshes")]
        public List<Mesh> Meshes { get; } = new List<Mesh>();
        [JsonProperty("nodes")]
        public List<Node> Nodes { get; } = new List<Node>();
        [JsonProperty("samplers")]
        public List<TextureSampler> Samplers { get; } = new List<TextureSampler>();
        [JsonProperty("scene")]
        public Int32 Scene { get; set; }
        [JsonProperty("scenes")]
        public List<Scene> Scenes { get; } = new List<Scene>();
        [JsonProperty("textures")]
        public List<Texture> Textures { get; } = new List<Texture>();

        /// <summary>
        /// Load gltf json file
        /// </summary>
        /// <param name="filePath">gltf file path</param>
        /// <returns></returns>
        public static GltfModel LoadFromJsonFile(String filePath)
        {
            var s = System.IO.File.ReadAllText(filePath, Encoding.UTF8);
            return JsonConvert.DeserializeObject<GltfModel>(s);
        }
    }
}
