namespace Obj2Tiles.Library.Geometry;

public class Face<T> where T : class
{
    public T A;
    public T B;
    public T C;

    public Vertex2D? TA;
    public Vertex2D? TB;
    public Vertex2D? TC;

    public int IndexA;
    public int IndexB;
    public int IndexC;
    
    public int? TextureIndexA;
    public int? TextureIndexB;
    public int? TextureIndexC;

    public int? MaterialIndex;
    
    public override string ToString()
    {
        return (TA is null || TB is null || TC is null) ? $"{A} {B} {C}" : $"v [{A} {B} {C}] vt [{TA} {TB} {TC}]";
    }

    public Face(int indexA, int indexB, int indexC, T A, T B, T C, int textureIndexA, int textureIndexB,
        int textureIndexC, int materialIndex, Vertex2D TA, Vertex2D TB, Vertex2D TC)
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
        return (TA is null || TB is null || TC is null)
            ? $"f {IndexA + 1} {IndexB + 1} {IndexC + 1}"
            : $"f {IndexA + 1}/{TextureIndexA + 1} {IndexB + 1}/{TextureIndexB + 1} {IndexC + 1}/{TextureIndexC + 1}";
    }

    public bool IsAdjacent(Face<T> face)
    {
        var va = (A == face.A || A == face.B || A == face.C) ? 1 : 0;
        var vb = (B == face.A || B == face.B || B == face.C) ? 1 : 0;
        var vc = (C == face.A || C == face.B || C == face.C) ? 1 : 0;

        return va + vb + vc >= 2;
    }

    public bool IsTextureAdjacent(Face<T> face)
    {
        if (TA is null || TB is null || TC is null)
            return false;
        
        if (face.TA is null || face.TB is null || face.TC is null)
            return false;
        
        var va = (TA == face.TA || TA == face.TB || TA == face.TC) ? 1 : 0;
        var vb = (TB == face.TA || TB == face.TB || TB == face.TC) ? 1 : 0;
        var vc = (TC == face.TA || TC == face.TB || TC == face.TC) ? 1 : 0;

        return va + vb + vc >= 2;

    }
}

