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
        public FactorColor(double v)
        {
            Red = v;
            Green = v;
            Blue = v;
        }

        public FactorColor(double r, double g, double b)
        {
            Red = r;
            Green = g;
            Blue = b;
        }
        public double Red { get; set; }

        public double Green { get; set; }

        public double Blue { get; set; }

        public override string ToString()
        {
            return $"{Red:0.0000} {Green:0.0000} {Blue:0.0000}";
        }

        public double[] ToArray(double? alpha = null)
        {
            var max = new double[] { Red, Green, Blue }.Max();
            if (max > 1)
            {
                Red /= max;
                Green /= max;
                Blue /= max;
            }
            if (alpha == null) return new double[] { Red, Green, Blue };
            return new double[] { Red, Green, Blue, alpha.Value };
        }
    }
}
