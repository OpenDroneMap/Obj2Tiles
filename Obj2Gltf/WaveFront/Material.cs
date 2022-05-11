using System;
using System.Collections.Generic;
using System.Text;

//http://paulbourke.net/dataformats/mtl/

namespace SilentWave.Obj2Gltf.WaveFront
{

    /// <summary>
    /// mtl material
    /// </summary>
    public class Material
    {
        /// <summary>
        ///  matname
        /// </summary>
        public String Name { get; set; }
        /// <summary>
        /// Ka: Ambient Color
        /// </summary>
        public Reflectivity Ambient { get; set; } = new Reflectivity(new FactorColor());
        /// <summary>
        /// Kd: Diffuse Color
        /// </summary>
        public Reflectivity Diffuse { get; set; } = new Reflectivity(new FactorColor(0.5f));
        /// <summary>
        /// map_Kd: Diffuse texture file path
        /// </summary>
        public String DiffuseTextureFile { get; set; }
        /// <summary>
        /// map_Ka: Ambient texture file path
        /// </summary>
        public String AmbientTextureFile { get; set; }
        /// <summary>
        /// Ks: specular reflectivity of the current material
        /// </summary>
        public Reflectivity Specular { get; set; } = new Reflectivity(new FactorColor());
        /// <summary>
        /// Tf: transmission filter: Any light passing through the object 
        /// is filtered by the transmission filter
        /// </summary>
        public Reflectivity Filter { get; set; }
        /// <summary>
        /// Ke: emissive color
        /// </summary>
        public Reflectivity Emissive { get; set; }
        /// <summary>
        /// illum: illum_# 0 ~ 10
        /// </summary>
        public Int32? Illumination { get; set; }
        /// <summary>
        /// d: the dissolve for the current material.
        /// </summary>
        public Dissolve Dissolve { get; set; }
        /// <summary>
        /// Tr: Transparency
        /// </summary>
        public Transparency Transparency { get; set; }
        /// <summary>
        /// Ns: specularShininess 0 ~ 1000
        /// </summary>
        public Double SpecularExponent { get; set; }
        /// <summary>
        /// sharpness value 0 ~ 1000, The default is 60
        /// </summary>
        public Int32? Sharpness { get; set; }
        /// <summary>
        /// 0.001 ~ 10
        /// </summary>
        public Double? OpticalDensity { get; set; }

        public Double GetAlpha()
        {
            if (Dissolve != null)
            {
                return Dissolve.Factor;
            }
            if (Transparency != null)
            {
                return (1.0 - Transparency.Factor);
            }
            return 1.0;
        }

        public override String ToString()
        {
            var sb = new StringBuilder();
            sb.Append($"newmtl {Name}" + Environment.NewLine);
            if (Ambient != null)
            {
                sb.Append($"Ka {Diffuse}" + Environment.NewLine);
            }
            if (Diffuse != null)
            {
                sb.Append($"Kd {Diffuse}" + Environment.NewLine);
            }
            if (Specular != null)
            {
                sb.Append($"Ks {Specular}" + Environment.NewLine);
            }
            if (Emissive != null)
            {
                sb.Append($"Ke {Emissive}" + Environment.NewLine);
            }
            if (Dissolve != null && Dissolve.Factor < 1)
            {
                sb.Append(Dissolve + Environment.NewLine);
            }
            if (SpecularExponent > 0)
            {
                sb.Append($"Ns {SpecularExponent}" + Environment.NewLine);
            }
            if (Sharpness != null)
            {
                sb.Append($"sharpness {Sharpness}" + Environment.NewLine);
            }
            if (Filter != null)
            {
                sb.Append($"Tf {Filter}" + Environment.NewLine);
            }
            if (!String.IsNullOrEmpty(AmbientTextureFile))
            {
                sb.Append($"map_Ka {DiffuseTextureFile}" + Environment.NewLine);
            }
            if (!String.IsNullOrEmpty(DiffuseTextureFile))
            {
                sb.Append($"map_Kd {DiffuseTextureFile}" + Environment.NewLine);
            }
            return sb.ToString();
        }
    }
}
