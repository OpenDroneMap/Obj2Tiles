using System;
using System.Linq;

namespace SilentWave.Obj2Gltf.WaveFront
{
    /// <summary>
    /// <para>Another way to represent a color, instead of classical Bytes it uses Doubles and the color components are factors (0-1 range)</para>
    /// <seealso cref="http://paulbourke.net/dataformats/mtl/"/>
    /// </summary>
    /// <remarks>Not the Byte triplet/quartet</remarks>
    public class FactorColor
    {
        public FactorColor()
        {

        }
        public FactorColor(Double v)
        {
            Red = v;
            Green = v;
            Blue = v;
        }

        public FactorColor(Double r, Double g, Double b)
        {
            Red = r;
            Green = g;
            Blue = b;
        }
        public Double Red { get; set; }

        public Double Green { get; set; }

        public Double Blue { get; set; }

        public override String ToString()
        {
            return $"{Red:0.0000} {Green:0.0000} {Blue:0.0000}";
        }

        public Double[] ToArray(Double? alpha = null)
        {
            var max = new Double[] { Red, Green, Blue }.Max();
            if (max > 1)
            {
                Red /= max;
                Green /= max;
                Blue /= max;
            }
            if (alpha == null) return new Double[] { Red, Green, Blue };
            return new Double[] { Red, Green, Blue, alpha.Value };
        }
    }
}
