using Obj2Tiles.Library.Geometry;

namespace Obj2Tiles.Model;

public class DecimateResult
{
    public string[] DestFiles { get; set; }
    public Box3 Bounds { get; set; }
}