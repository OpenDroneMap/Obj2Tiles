namespace Obj2Tiles.Library.Geometry;

public class Face
{

    public readonly int IndexA;
    public readonly int IndexB;
    public readonly int IndexC;

    public override string ToString()
    {
        return $"{IndexA} {IndexB} {IndexC}";
    }

    public Face(int indexA, int indexB, int indexC)
    {
        IndexA = indexA;
        IndexB = indexB;
        IndexC = indexC;
    }

    public virtual string ToObj()
    {
        return $"f {IndexA + 1} {IndexB + 1} {IndexC + 1}";
    }
}