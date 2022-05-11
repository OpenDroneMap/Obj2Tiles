using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace SilentWave.Gltf
{
    /// <summary>
    /// The material appearance of a primitive.
    /// </summary>
    public class Material
    {
        /// <summary>
        /// Optional user-defined name for this object.
        /// </summary>
        [JsonProperty("name")]
        public String Name { get; set; }
        /// <summary>
        /// A set of parameter values that are used to define the metallic-roughness
        /// material model from Physically-Based Rendering (PBR) methodology. When not
        /// specified, all the default values of `pbrMetallicRoughness` apply.
        /// </summary>
        [JsonProperty("pbrMetallicRoughness")]
        public PbrMetallicRoughness PbrMetallicRoughness { get; set; }
        /// <summary>
        /// The emissive color of the material.
        /// </summary>
        [JsonProperty("emissiveFactor")]
        public Double[] EmissiveFactor { get; set; }
        /// <summary>
        /// The alpha rendering mode of the material.
        ///
        /// The material's alpha rendering mode enumeration specifying the
        /// interpretation of the alpha value of the main factor and texture.
        ///
        /// * In `Opaque` mode (default) the alpha value is ignored and the rendered
        ///   output is fully opaque.
        ///
        /// * In `Mask` mode, the rendered output is either fully opaque or fully
        ///   transparent depending on the alpha value and the specified alpha cutoff
        ///   value.
        ///
        /// * In `Blend` mode, the alpha value is used to composite the source and
        ///   destination areas and the rendered output is combined with the
        ///   background using the normal painting operation (i.e. the Porter and
        ///   Duff over operator).
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("alphaMode")]
        public AlphaMode AlphaMode { get; set; }
        /// <summary>
        /// Specifies whether the material is double-sided.
        ///
        /// * When this value is false, back-face culling is enabled.
        ///
        /// * When this value is true, back-face culling is disabled and double sided
        ///   lighting is enabled.
        ///
        /// The back-face must have its normals reversed before the lighting
        /// equation is evaluated.
        /// </summary>
        [JsonProperty("doubleSided")]
        public Boolean DoubleSided { get; set; }

        public override String ToString()
            => $"AM:{AlphaMode} DS:{(DoubleSided ? 1 : 0)} MRB:[{PbrMetallicRoughness.BaseColorFactor[0]}, {PbrMetallicRoughness.BaseColorFactor[1]}, {PbrMetallicRoughness.BaseColorFactor[2]}, {PbrMetallicRoughness.BaseColorFactor[3]}] E:[{EmissiveFactor[0]}, {EmissiveFactor[1]}, {EmissiveFactor[2]}] M:{PbrMetallicRoughness.MetallicFactor} R:{PbrMetallicRoughness.RoughnessFactor} T:{PbrMetallicRoughness.BaseColorTexture?.Index.ToString() ?? "<null>"} {Name}";
    }
}
