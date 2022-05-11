using System;
using System.Collections.Generic;
using System.IO;

namespace SilentWave.Obj2Gltf.WaveFront
{
    /// <summary>
    /// geometry with face meshes
    /// http://paulbourke.net/dataformats/obj/
    /// http://www.fileformat.info/format/wavefrontobj/egff.htm
    /// </summary>
    public class Geometry
    {
        public Geometry(String id)
        {
            if (String.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException(nameof(id));
            Id = id;
        }
        /// <summary>
        /// group name
        /// </summary>
        public String Id { get; }
        /// <summary>
        /// meshes
        /// </summary>
        public List<Face> Faces { get; } = new List<Face>();
        /// <summary>
        /// write geometry
        /// </summary>
        /// <param name="writer"></param>
        public void Write(StreamWriter writer)
        {
            writer.WriteLine($"g {Id}");
            writer.WriteLine($"s off");
            foreach (var f in Faces) f.Write(writer);
        }
    }
}
