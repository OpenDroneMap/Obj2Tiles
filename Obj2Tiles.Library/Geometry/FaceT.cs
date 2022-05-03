namespace Obj2Tiles.Library.Geometry;

public class Face<T> where T : class
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


    public string ToObj()
    {
        return $"f {IndexA + 1} {IndexB + 1} {IndexC + 1}";
    }
}

public class FaceT<T> where T : class
{

    public readonly int IndexA;
    public readonly int IndexB;
    public readonly int IndexC;

    public int TextureIndexA;
    public int TextureIndexB;
    public int TextureIndexC;

    public int MaterialIndex;

    public override string ToString()
    {
        return $"{IndexA} {IndexB} {IndexC}";
    }

    public FaceT(int indexA, int indexB, int indexC, int textureIndexA, int textureIndexB,
        int textureIndexC, int materialIndex)
    {
        IndexA = indexA;
        IndexB = indexB;
        IndexC = indexC;

        TextureIndexA = textureIndexA;
        TextureIndexB = textureIndexB;
        TextureIndexC = textureIndexC;

        MaterialIndex = materialIndex;
    }

    public string ToObj()
    {
        return $"f {IndexA + 1}/{TextureIndexA + 1} {IndexB + 1}/{TextureIndexB + 1} {IndexC + 1}/{TextureIndexC + 1}";
    }
}