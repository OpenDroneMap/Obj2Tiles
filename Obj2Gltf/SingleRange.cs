using System;

namespace SilentWave.Obj2Gltf
{
    /// <summary>
    /// a range expressed by Min and Max values
    /// </summary>
    public class SingleRange
    {
        public SingleRange()
        {
            Min = float.MaxValue;
            Max = float.MinValue;
        }
        public float Min { get; set; }

        public float Max { get; set; }

        public bool IsValid()
        {
            return Min <= Max;
        }

        public override string ToString()
        {
            return $"(Min: {Min}, Max: {Max})";
        }
    }
}
