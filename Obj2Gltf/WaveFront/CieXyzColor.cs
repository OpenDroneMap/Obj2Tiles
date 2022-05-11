using System;

namespace SilentWave.Obj2Gltf.WaveFront
{
    /// <summary>
    /// CIE 1931 XYZ color space 
    /// <list type="bullet">
    /// <item><see cref="https://en.wikipedia.org/wiki/CIE_1931_color_space"/></item>
    /// <item><seealso cref="http://paulbourke.net/dataformats/mtl/"/></item>
    /// </list>
    /// </summary>
    public class CieXyzColor
    {
        public Single X { get; set; }
        public Single Y { get; set; }
        public Single Z { get; set; }
    }
}
