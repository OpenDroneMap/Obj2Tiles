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
            Min = Single.MaxValue;
            Max = Single.MinValue;
        }
        public Single Min { get; set; }

        public Single Max { get; set; }

        public Boolean IsValid()
        {
            return Min <= Max;
        }

        public override String ToString()
        {
            return $"(Min: {Min}, Max: {Max})";
        }
    }
}
