using System;
using System.Text;

namespace SilentWave.Obj2Gltf
{
    public class GltfConverterOptions
    {
        /// <summary>
        /// obj and mtl files' text encoding
        /// </summary>
        public Encoding ObjEncoding { get; set; }

        /// <summary>
        /// Default is false
        /// </summary>
        public Boolean RemoveDegenerateFaces { get; set; } = false;
        
        /// <summary>
        /// Default is false
        /// </summary>
        public Boolean DeleteOriginals { get; set; } = false;
    }
}
