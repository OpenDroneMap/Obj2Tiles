using System;

namespace SilentWave.Obj2Gltf.WaveFront
{
    /// <summary>
    /// <seealso cref="http://paulbourke.net/dataformats/mtl/"/>
    /// </summary>
    public class Reflectivity
    {
        public FactorColor Color { get; }

        public Spectral Spectral { get; }

        public CieXyzColor XYZ { get; }

        public ReflectivityType AmbientType { get; }

        public Reflectivity(FactorColor color)
        {
            AmbientType = ReflectivityType.Color;
            Color = color;
        }

        public Reflectivity(Spectral spectral)
        {
            AmbientType = ReflectivityType.Spectral;
            Spectral = spectral;
        }

        public Reflectivity(CieXyzColor xyz)
        {
            AmbientType = ReflectivityType.XYZ;
            XYZ = xyz;
        }

        public override String ToString()
        {
            switch (AmbientType)
            {
                case ReflectivityType.Color:
                    return Color.ToString();
                default: // TODO:
                    return String.Empty;
            }
        }
    }
}
