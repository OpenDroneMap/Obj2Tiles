using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace SilentWave.Obj2Gltf.WaveFront
{
    /// <summary>
    /// face with material
    /// </summary>
    public class Face
    {
        public Face(String materialName = "default")
        {
            MatName = materialName;
        }
        /// <summary>
        /// face used material name
        /// </summary>
        public String MatName { get; }
        /// <summary>
        /// face meshes
        /// </summary>
        public List<FaceTriangle> Triangles { get; set; } = new List<FaceTriangle>();
        /// <summary>
        /// write face info into obj file writer
        /// </summary>
        /// <param name="writer"></param>
        public void Write(StreamWriter writer)
        {
            writer.WriteLine($"usemtl {MatName}");

            var contents = String.Join(Environment.NewLine, Triangles);
            writer.WriteLine(contents);
        }
    }
}
