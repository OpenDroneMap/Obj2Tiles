using Obj2Tiles.Library.Geometry;

namespace Obj2Tiles.Stages.Model
{
    public class DecimateResult
    {
        public string[] DestFiles { get; set; } = null!;
        public Box3 Bounds { get; set; } = default!;
    }
}