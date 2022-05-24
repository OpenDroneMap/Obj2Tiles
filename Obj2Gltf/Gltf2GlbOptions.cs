using System;

namespace SilentWave.Obj2Gltf
{
    public class Gltf2GlbOptions
    {
        public Gltf2GlbOptions(string inputPath, string outputPath = null)
        {
            InputPath = inputPath ?? throw new ArgumentNullException();
            OutPutPath = outputPath ?? System.IO.Path.Combine(System.IO.Path.GetDirectoryName(inputPath), System.IO.Path.GetFileNameWithoutExtension(inputPath) + ".glb");
        }

        public string InputPath { get; }
        
        /// <summary>
        /// Default is true
        /// </summary>
        public bool MinifyJson { get; set; } = true;

        // TODO: make this optional ?
        //public Boolean EmbedBuffers { get; set; } = true;
        //public Boolean EmbedImages { get; set; } = true;

        /// <summary>
        /// Default is false
        /// </summary>
        public bool DeleteOriginal { get; set; } = false;
        public string OutPutPath { get; }
    }
}
