using System;
using Newtonsoft.Json;

namespace SilentWave.Obj2Gltf.Gltf
{
    /// <summary>
    /// A set of parameter values that are used to define the metallic-roughness
    /// material model from Physically-Based Rendering (PBR) methodology.
    /// </summary>
    public class PbrMetallicRoughness
    {
        /// <summary>
        /// The material's base color factor.
        /// </summary>
        [JsonProperty("baseColorFactor")]
        public double[] BaseColorFactor { get; set; } = new double[] { 1, 1, 1, 1 };
        /// <summary>
        /// The base color texture.
        /// </summary>
        [JsonProperty("baseColorTexture")]
        public TextureReferenceInfo BaseColorTexture { get; set; }
        /// <summary>
        /// The metalness of the material.
        /// </summary>
        [JsonProperty("metallicFactor")]
        public double MetallicFactor { get; set; } = 0.0;
        /// The roughness of the material.
        ///
        /// * A value of 1.0 means the material is completely rough.
        /// * A value of 0.0 means the material is completely smooth.
        [JsonProperty("roughnessFactor")]
        public double RoughnessFactor { get; set; } = 0.9;
    }
}
