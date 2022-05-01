namespace Obj2Tiles.Library.Geometry;

public class Face<T> where T : class
{
    public readonly T A;
    public readonly T B;
    public readonly T C;

    public readonly int IndexA;
    public readonly int IndexB;
    public readonly int IndexC;

    public override string ToString()
    {
        return $"{A} {B} {C}";
    }

    public Face(int indexA, int indexB, int indexC, T A, T B, T C)
    {
        IndexA = indexA;
        IndexB = indexB;
        IndexC = indexC;
        this.A = A;
        this.B = B;
        this.C = C;
    }


    public string ToObj()
    {
        return $"f {IndexA + 1} {IndexB + 1} {IndexC + 1}";
    }

    public bool IsAdjacent(FaceT<T> faceT)
    {
        var va = (A == faceT.A || A == faceT.B || A == faceT.C) ? 1 : 0;
        var vb = (B == faceT.A || B == faceT.B || B == faceT.C) ? 1 : 0;
        var vc = (C == faceT.A || C == faceT.B || C == faceT.C) ? 1 : 0;

        return va + vb + vc >= 2;
    }
}

public class FaceT<T> where T : class
{
    public readonly T A;
    public readonly T B;
    public readonly T C;

    public readonly Vertex2 TA;
    public readonly Vertex2 TB;
    public readonly Vertex2 TC;

    public readonly int IndexA;
    public readonly int IndexB;
    public readonly int IndexC;

    public readonly int TextureIndexA;
    public readonly int TextureIndexB;
    public readonly int TextureIndexC;

    public readonly int MaterialIndex;

    public override string ToString()
    {
        return $"v [{A} {B} {C}] vt [{TA} {TB} {TC}]";
    }

    public FaceT(int indexA, int indexB, int indexC, T A, T B, T C, int textureIndexA, int textureIndexB,
        int textureIndexC, int materialIndex, Vertex2 TA, Vertex2 TB, Vertex2 TC)
    {
        IndexA = indexA;
        IndexB = indexB;
        IndexC = indexC;

        TextureIndexA = textureIndexA;
        TextureIndexB = textureIndexB;
        TextureIndexC = textureIndexC;

        MaterialIndex = materialIndex;

        this.A = A;
        this.B = B;
        this.C = C;

        this.TA = TA;
        this.TB = TB;
        this.TC = TC;
    }

    public string ToObj()
    {
        return $"f {IndexA + 1}/{TextureIndexA + 1} {IndexB + 1}/{TextureIndexB + 1} {IndexC + 1}/{TextureIndexC + 1}";
    }

    public bool IsAdjacent(FaceT<T> faceT)
    {
        var va = (A == faceT.A || A == faceT.B || A == faceT.C) ? 1 : 0;
        var vb = (B == faceT.A || B == faceT.B || B == faceT.C) ? 1 : 0;
        var vc = (C == faceT.A || C == faceT.B || C == faceT.C) ? 1 : 0;

        return va + vb + vc >= 2;
    }

    public bool IsTextureAdjacent(FaceT<T> f)
    {
        
        if (TextureIndexA != f.TextureIndexA && TextureIndexA != f.TextureIndexB && TextureIndexA != f.TextureIndexC)
        {
            if (TextureIndexB != f.TextureIndexA && TextureIndexB != f.TextureIndexB && TextureIndexB != f.TextureIndexC)
            {
                return false;
            }
            
            if (TextureIndexC != f.TextureIndexA && TextureIndexC != f.TextureIndexB && TextureIndexC != f.TextureIndexC)
            {
                return false;
            }
        }
        
        

        return true;
    }
}

public static class FaceExtenders
{
    public static double Area(this FaceT<Vertex2> faceT)
    {
        return Math.Abs(
            (faceT.A.X - faceT.C.X) * (faceT.B.Y - faceT.A.Y) -
            (faceT.A.X - faceT.B.X) * (faceT.C.Y - faceT.A.Y)
        ) / 2;
    }

    public static double TextureArea(this FaceT<Vertex3> faceT)
    {
        return Math.Abs(
            (faceT.TA.X - faceT.TC.X) * (faceT.TB.Y - faceT.TA.Y) -
            (faceT.TA.X - faceT.TB.X) * (faceT.TC.Y - faceT.TA.Y)
        ) / 2;
    }
}