namespace Obj2Tiles.Library.Geometry;

public interface IMesh
{
    string Name { get; set; }
    Box3 Bounds { get; }
    IReadOnlyList<Vertex3> Vertices { get; }

    int Split(IVertexUtils utils, double q, out IMesh left,
        out IMesh right);

    Vertex3 GetVertexBaricenter();
    Vertex3 GetVertexMedian();
    void WriteObj(string path, bool removeUnused = true);

    int FacesCount { get; }
    int VertexCount { get; }
}