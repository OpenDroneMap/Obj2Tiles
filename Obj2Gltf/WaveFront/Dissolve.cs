using System;

//http://paulbourke.net/dataformats/mtl/

namespace SilentWave.Obj2Gltf.WaveFront
{
    /// <summary>
    /// 1- transparency
    /// "factor" is the amount this material dissolves into the background.  A 
    ///  factor of 1.0 is fully opaque.This is the default when a new material 
    /// is created.A factor of 0.0 is fully dissolved(completely
    /// transparent).
    /// </summary>
    public class Dissolve
    {
        /// <summary>
        /// A factor of 1.0 is fully opaque.
        /// </summary>
        public Double Factor { get; set; }
        /// <summary>
        /// d -halo 0.0, will be fully dissolved at its center and will 
        /// appear gradually more opaque toward its edge.
        /// </summary>
        public Boolean Halo { get; set; }

        public override String ToString()
        {
            if (!Halo)
            {
                return $"d {Factor}";
            }
            return $"d -halo {Factor}";
        }
    }
}
