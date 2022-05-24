using System;

namespace SilentWave.Obj2Gltf.Geom
{
    public class BoundingBox2
    {
        public SVec2 Min { get; set; }

        public SVec2 Max { get; set; }

        public bool IsValid()
        {
            return Min.U < Max.U && Min.V < Max.V;
        }

        public bool IsIn(SVec2 p)
        {
            return p.U > Min.U && p.U < Max.U && p.V > Min.V && p.V < Max.V;
        }

        public static BoundingBox2 New()
        {
            return new BoundingBox2
            {
                Min = new SVec2(float.MaxValue, float.MaxValue),
                Max = new SVec2(float.MinValue, float.MinValue)
            };

        }
    }
}
